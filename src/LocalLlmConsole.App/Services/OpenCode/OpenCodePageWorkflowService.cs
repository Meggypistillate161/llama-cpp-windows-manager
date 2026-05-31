namespace LocalLlmConsole.Services;

public sealed record OpenCodePageChoices(
    IReadOnlyList<ModelRecord> LocalModels,
    IReadOnlyList<OpenCodeModelEntry> Models,
    IReadOnlyList<OpenCodeAgentEntry> Agents,
    OpenCodeModelEntry? SelectedModel,
    OpenCodeAgentEntry? SelectedAgent);

public sealed record OpenCodeGatewayHealthState(
    string Summary,
    string Detail,
    bool IsWarning);

public sealed class OpenCodePageWorkflowService
{
    private static readonly OpenCodeModelEntry AddNewModel = new("", "", "", "Add New...", IsAddNew: true);
    private static readonly OpenCodeAgentEntry AddNewAgent = new("", "", OpenCodeAgentKind.Config, "", "Add New...", IsAddNew: true);

    private readonly OpenCodeConfigService _openCode;
    private readonly OpenCodeModelSyncService _sync;

    public OpenCodePageWorkflowService(OpenCodeConfigService openCode, OpenCodeModelSyncService sync)
    {
        _openCode = openCode ?? throw new ArgumentNullException(nameof(openCode));
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
    }

    public OpenCodeFileSet LoadOrDetectFileSet()
        => _openCode.LoadOrDetectFileSet();

    public OpenCodeFileSet DetectAndSaveFileSet()
        => SaveFileSet(_openCode.DetectFileSet());

    public OpenCodeFileSet SaveConfigPath(OpenCodeFileSet current, string configPath)
        => SaveFileSet(current with { ConfigPath = Path.GetFullPath(configPath) });

    public OpenCodeFileSet SaveAgentsDirectory(OpenCodeFileSet current, string agentsDirectory)
        => SaveFileSet(current with { AgentsDirectory = Path.GetFullPath(agentsDirectory) });

    public OpenCodeFileSet EnsureAndSaveFileSet(OpenCodeFileSet fileSet)
    {
        _openCode.EnsureFiles(fileSet);
        return SaveFileSet(fileSet);
    }

    public static string ConfigDirectory(OpenCodeFileSet fileSet)
        => Path.GetDirectoryName(fileSet.ConfigPath) ?? "";

    public OpenCodePageChoices BuildChoices(
        OpenCodeFileSet files,
        IReadOnlyList<ModelRecord> localModels,
        string preferredModelId,
        string preferredAgentId)
    {
        var localChoices = localModels
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var modelChoices = _openCode.ListModels(files.ConfigPath).Append(AddNewModel).ToList();
        var selectedModel = modelChoices.FirstOrDefault(model => string.Equals(model.FullId, preferredModelId, StringComparison.OrdinalIgnoreCase))
            ?? modelChoices.FirstOrDefault(model => !model.IsAddNew)
            ?? modelChoices.FirstOrDefault();

        var agentChoices = _openCode.ListAgents(files.ConfigPath, files.AgentsDirectory).Append(AddNewAgent).ToList();
        var selectedAgent = agentChoices.FirstOrDefault(agent => string.Equals(agent.Id, preferredAgentId, StringComparison.OrdinalIgnoreCase))
            ?? agentChoices.FirstOrDefault(agent => !agent.IsAddNew)
            ?? agentChoices.FirstOrDefault();

        return new OpenCodePageChoices(localChoices, modelChoices, agentChoices, selectedModel, selectedAgent);
    }

    public OpenCodeGatewayHealthState GatewayHealth(
        string configPath,
        IReadOnlyList<ModelRecord> expectedModels,
        AppSettings settings)
    {
        if (!settings.AutoLoadGatewayEnabled)
        {
            return new OpenCodeGatewayHealthState(
                "OpenCode sync: auto-load gateway is disabled. Direct per-model provider entries are preserved.",
                "Turn on Auto-load gateway in Settings to sync one shared OpenCode provider for every registered model.",
                IsWarning: false);
        }

        try
        {
            var health = _sync.InspectGatewayProvider(configPath, expectedModels, settings);
            return new OpenCodeGatewayHealthState(health.Summary, health.Detail, IsWarning: !health.Ok);
        }
        catch (Exception ex)
        {
            return new OpenCodeGatewayHealthState(
                "OpenCode sync: config cannot be read.",
                ex.Message,
                IsWarning: true);
        }
    }

    private OpenCodeFileSet SaveFileSet(OpenCodeFileSet fileSet)
    {
        var normalized = new OpenCodeFileSet(Path.GetFullPath(fileSet.ConfigPath), Path.GetFullPath(fileSet.AgentsDirectory));
        _openCode.SaveFileSet(normalized);
        return normalized;
    }
}
