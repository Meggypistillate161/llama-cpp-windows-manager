namespace LocalLlmConsole.Services;

public sealed partial class OpenCodeConfigService
{
    public IReadOnlyList<OpenCodeAgentEntry> ListAgents(string configPath, string agentsDirectory)
    {
        var entries = new List<OpenCodeAgentEntry>();
        var config = ReadConfigObject(configPath, createIfMissing: false);
        if (config["agent"] is JsonObject agents)
        {
            foreach (var (name, _) in agents)
            {
                entries.Add(new OpenCodeAgentEntry(
                    $"config:{name}",
                    name,
                    OpenCodeAgentKind.Config,
                    configPath,
                    $"{name} (config)"));
            }
        }

        if (Directory.Exists(agentsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(agentsDirectory, "*.md", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                entries.Add(new OpenCodeAgentEntry(
                    $"markdown:{Path.GetFullPath(file)}",
                    name,
                    OpenCodeAgentKind.Markdown,
                    Path.GetFullPath(file),
                    $"{name} (markdown)"));
            }
        }

        return entries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Kind)
            .ToArray();
    }

    public string ReadAgentSnippet(string configPath, OpenCodeAgentEntry entry)
    {
        if (entry.Kind == OpenCodeAgentKind.Markdown)
            return File.Exists(entry.Path) ? File.ReadAllText(entry.Path) : "";

        var config = ReadConfigObject(configPath, createIfMissing: false);
        return FormatNode(config["agent"]?[entry.Name] as JsonObject ?? new JsonObject());
    }

    public void SaveAgentSnippet(string configPath, OpenCodeAgentEntry entry, string snippet)
    {
        if (entry.Kind == OpenCodeAgentKind.Markdown)
        {
            ConfigFileSafetyService.WriteTextWithBackup(entry.Path, snippet, Encoding.UTF8, "OpenCode agent file");
            return;
        }

        EnsureConfigFile(configPath);
        var config = ReadConfigObject(configPath, createIfMissing: true);
        var agentObject = ParseObject(snippet, "Agent snippet");
        EnsureObject(config, "agent")[entry.Name] = agentObject;
        SaveConfigObject(configPath, config);
    }

    public OpenCodeAgentEntry CreateAgent(string configPath, string agentsDirectory, string requestedName, bool markdown, string modelFullId)
    {
        var name = SafeOpenCodeId(requestedName);
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Agent name must include at least one letter or number.");

        return markdown
            ? CreateMarkdownAgent(agentsDirectory, name, modelFullId)
            : CreateConfigAgent(configPath, name, modelFullId);
    }

    public void DeleteAgent(string configPath, string agentsDirectory, OpenCodeAgentEntry entry)
    {
        if (entry.Kind == OpenCodeAgentKind.Markdown)
        {
            if (!IsPathInside(agentsDirectory, entry.Path))
                throw new InvalidOperationException("The selected agent file is outside the configured agents folder.");
            ConfigFileSafetyService.BackupBeforeDelete(entry.Path, "OpenCode agent file");
            if (File.Exists(entry.Path)) File.Delete(entry.Path);
            return;
        }

        var config = ReadConfigObject(configPath, createIfMissing: false);
        if (config["agent"] is JsonObject agents)
        {
            agents.Remove(entry.Name);
            SaveConfigObject(configPath, config);
        }
    }

    private OpenCodeAgentEntry CreateConfigAgent(string configPath, string name, string modelFullId)
    {
        EnsureConfigFile(configPath);
        var config = ReadConfigObject(configPath, createIfMissing: true);
        var agent = new JsonObject
        {
            ["description"] = "Custom OpenCode agent",
            ["mode"] = "subagent",
            ["prompt"] = "Describe how this agent should behave."
        };
        if (!string.IsNullOrWhiteSpace(modelFullId))
            agent["model"] = modelFullId;

        EnsureObject(config, "agent")[name] = agent;
        SaveConfigObject(configPath, config);
        return new OpenCodeAgentEntry($"config:{name}", name, OpenCodeAgentKind.Config, configPath, $"{name} (config)");
    }

    private static OpenCodeAgentEntry CreateMarkdownAgent(string agentsDirectory, string name, string modelFullId)
    {
        Directory.CreateDirectory(agentsDirectory);
        var path = Path.Combine(agentsDirectory, $"{name}.md");
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("description: Custom OpenCode agent");
        builder.AppendLine("mode: subagent");
        if (!string.IsNullOrWhiteSpace(modelFullId))
            builder.AppendLine($"model: {modelFullId}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("Describe how this agent should behave.");
        ConfigFileSafetyService.WriteTextWithBackup(path, builder.ToString(), Encoding.UTF8, "OpenCode agent file");
        return new OpenCodeAgentEntry($"markdown:{Path.GetFullPath(path)}", name, OpenCodeAgentKind.Markdown, Path.GetFullPath(path), $"{name} (markdown)");
    }
}
