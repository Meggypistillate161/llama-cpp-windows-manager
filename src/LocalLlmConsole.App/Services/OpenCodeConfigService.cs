using System.Text.Json.Serialization.Metadata;

namespace LocalLlmConsole.Services;

public sealed record OpenCodeFileSet(string ConfigPath, string AgentsDirectory);

public sealed record OpenCodeModelEntry(
    string FullId,
    string ProviderId,
    string ModelId,
    string Label,
    bool IsAddNew = false)
{
    public override string ToString() => Label;
}

public sealed record OpenCodeLocalModelDraft(
    string FullId,
    string ProviderId,
    string ModelId,
    string Label,
    string Snippet);

public sealed record OpenCodeModelAddAnalysis(
    bool SnippetValid,
    string Error,
    string FullId,
    bool SameIdExists,
    bool SameConfig,
    IReadOnlyList<string> SimilarMatches);

public enum OpenCodeAgentKind
{
    Config,
    Markdown
}

public sealed record OpenCodeAgentEntry(
    string Id,
    string Name,
    OpenCodeAgentKind Kind,
    string Path,
    string Label,
    bool IsAddNew = false)
{
    public override string ToString() => Label;
}

public sealed partial class OpenCodeConfigService
{
    public const string LocalProviderId = "local-llm-console";

    private const string SchemaUrl = "https://opencode.ai/config.json";
    private const string LocalProviderName = "llama.cpp Console";
    private readonly string _workspaceRoot;

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    private static readonly JsonDocumentOptions JsoncOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public OpenCodeConfigService(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
    }

    public OpenCodeFileSet LoadOrDetectFileSet()
    {
        var detected = DetectFileSet();
        var settingsPath = SettingsPath();
        if (!File.Exists(settingsPath)) return detected;

        try
        {
            var stored = JsonSerializer.Deserialize<OpenCodeFileSet>(File.ReadAllText(settingsPath));
            if (stored is null) return detected;
            return new OpenCodeFileSet(
                string.IsNullOrWhiteSpace(stored.ConfigPath) ? detected.ConfigPath : Path.GetFullPath(stored.ConfigPath),
                string.IsNullOrWhiteSpace(stored.AgentsDirectory) ? detected.AgentsDirectory : Path.GetFullPath(stored.AgentsDirectory));
        }
        catch
        {
            return detected;
        }
    }

    public void SaveFileSet(OpenCodeFileSet fileSet)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath())!);
        File.WriteAllText(SettingsPath(), JsonSerializer.Serialize(fileSet, PrettyJson) + Environment.NewLine, Encoding.UTF8);
    }

    public OpenCodeFileSet DetectFileSet()
    {
        var configPath = CandidateConfigPaths().FirstOrDefault(File.Exists) ?? DefaultConfigPath();
        var agentsDirectory = CandidateAgentDirectories()
            .FirstOrDefault(Directory.Exists)
            ?? Path.Combine(Path.GetDirectoryName(configPath) ?? DefaultOpenCodeDirectory(), "agent");
        return new OpenCodeFileSet(Path.GetFullPath(configPath), Path.GetFullPath(agentsDirectory));
    }

    public void EnsureFiles(OpenCodeFileSet fileSet)
    {
        EnsureConfigFile(fileSet.ConfigPath);
        Directory.CreateDirectory(fileSet.AgentsDirectory);
    }

}
