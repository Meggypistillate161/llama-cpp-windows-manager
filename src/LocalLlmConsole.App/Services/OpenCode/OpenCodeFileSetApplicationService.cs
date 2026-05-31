namespace LocalLlmConsole.Services;

public enum OpenCodeFileSetTransitionOutcome
{
    Applied
}

public enum OpenCodeConfigFolderOpenOutcome
{
    Ignored,
    Opened
}

public enum OpenCodeFileSetPickerOutcome
{
    Cancelled,
    Applied
}

public sealed record OpenCodeConfigFilePickerPlan(
    string Title,
    string Filter,
    bool CheckFileExists,
    bool AddExtension,
    string DefaultExt,
    string FileName,
    string InitialDirectory);

public sealed record OpenCodeConfigFolderOpenActions(
    Action<string> OpenFolder);

public sealed record OpenCodeFileSetTransitionActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    OpenCodeFileSetApplicationActions FileSetActions);

public sealed record OpenCodeFileSetPickerActions(
    Func<OpenCodeConfigFilePickerPlan, string?> PickConfigFile,
    Func<string, string?> PickAgentsDirectory,
    OpenCodeFileSetTransitionActions TransitionActions);

public sealed class OpenCodeFileSetApplicationService
{
    private readonly OpenCodePageWorkflowService _workflow;
    private readonly OpenCodePageApplicationService _pageApplication;

    public OpenCodeFileSetApplicationService(
        OpenCodePageWorkflowService workflow,
        OpenCodePageApplicationService pageApplication)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _pageApplication = pageApplication ?? throw new ArgumentNullException(nameof(pageApplication));
    }

    public OpenCodeFileSet LoadOrDetect()
        => _workflow.LoadOrDetectFileSet();

    public OpenCodeConfigFilePickerPlan BuildConfigFilePicker(OpenCodeFileSet current)
    {
        ArgumentNullException.ThrowIfNull(current);

        var initialDirectory = Path.GetDirectoryName(current.ConfigPath);
        return new OpenCodeConfigFilePickerPlan(
            "Choose OpenCode config",
            "OpenCode config|opencode.json;opencode.jsonc|JSON files|*.json;*.jsonc|All files|*.*",
            CheckFileExists: false,
            AddExtension: true,
            ".jsonc",
            Path.GetFileName(current.ConfigPath),
            !string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory)
                ? initialDirectory
                : "");
    }

    public OpenCodeConfigFolderOpenOutcome OpenConfigFolder(
        OpenCodeFileSet current,
        OpenCodeConfigFolderOpenActions actions)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.OpenFolder);

        var folder = OpenCodePageWorkflowService.ConfigDirectory(current);
        if (string.IsNullOrWhiteSpace(folder))
            return OpenCodeConfigFolderOpenOutcome.Ignored;

        actions.OpenFolder(folder);
        return OpenCodeConfigFolderOpenOutcome.Opened;
    }

    public async Task<OpenCodeFileSetTransitionOutcome> DetectAsync(
        OpenCodeFileSetTransitionActions actions)
    {
        Validate(actions);

        await actions.RunBusyAsync("Detecting OpenCode files...", async () =>
        {
            await _pageApplication.ApplyDetectedFileSetAsync(
                _workflow.DetectAndSaveFileSet(),
                actions.FileSetActions);
        });
        return OpenCodeFileSetTransitionOutcome.Applied;
    }

    public async Task<OpenCodeFileSetTransitionOutcome> SaveConfigPathAsync(
        OpenCodeFileSet current,
        string configPath,
        OpenCodeFileSetTransitionActions actions)
    {
        ArgumentNullException.ThrowIfNull(current);
        Validate(actions);

        await actions.RunBusyAsync("Setting OpenCode config...", async () =>
        {
            await _pageApplication.ApplyConfigFileSetAsync(
                _workflow.SaveConfigPath(current, configPath),
                actions.FileSetActions);
        });
        return OpenCodeFileSetTransitionOutcome.Applied;
    }

    public async Task<OpenCodeFileSetPickerOutcome> ChooseConfigPathAsync(
        OpenCodeFileSet current,
        OpenCodeFileSetPickerActions actions)
    {
        ArgumentNullException.ThrowIfNull(current);
        ValidatePicker(actions);
        ArgumentNullException.ThrowIfNull(actions.PickConfigFile);

        var configPath = actions.PickConfigFile(BuildConfigFilePicker(current));
        if (string.IsNullOrWhiteSpace(configPath))
            return OpenCodeFileSetPickerOutcome.Cancelled;

        await SaveConfigPathAsync(current, configPath, actions.TransitionActions);
        return OpenCodeFileSetPickerOutcome.Applied;
    }

    public async Task<OpenCodeFileSetTransitionOutcome> SaveAgentsDirectoryAsync(
        OpenCodeFileSet current,
        string agentsDirectory,
        OpenCodeFileSetTransitionActions actions)
    {
        ArgumentNullException.ThrowIfNull(current);
        Validate(actions);

        await actions.RunBusyAsync("Setting OpenCode agents folder...", async () =>
        {
            await _pageApplication.ApplyAgentsDirectoryFileSetAsync(
                _workflow.SaveAgentsDirectory(current, agentsDirectory),
                actions.FileSetActions);
        });
        return OpenCodeFileSetTransitionOutcome.Applied;
    }

    public async Task<OpenCodeFileSetPickerOutcome> ChooseAgentsDirectoryAsync(
        OpenCodeFileSet current,
        OpenCodeFileSetPickerActions actions)
    {
        ArgumentNullException.ThrowIfNull(current);
        ValidatePicker(actions);
        ArgumentNullException.ThrowIfNull(actions.PickAgentsDirectory);

        var agentsDirectory = actions.PickAgentsDirectory(current.AgentsDirectory);
        if (string.IsNullOrWhiteSpace(agentsDirectory))
            return OpenCodeFileSetPickerOutcome.Cancelled;

        await SaveAgentsDirectoryAsync(current, agentsDirectory, actions.TransitionActions);
        return OpenCodeFileSetPickerOutcome.Applied;
    }

    public async Task<OpenCodeFileSetTransitionOutcome> EnsureAsync(
        OpenCodeFileSet current,
        OpenCodeFileSetTransitionActions actions)
    {
        ArgumentNullException.ThrowIfNull(current);
        Validate(actions);

        await actions.RunBusyAsync("Creating OpenCode files...", async () =>
        {
            await _pageApplication.ApplyEnsuredFileSetAsync(
                _workflow.EnsureAndSaveFileSet(current),
                actions.FileSetActions);
        });
        return OpenCodeFileSetTransitionOutcome.Applied;
    }

    private static void Validate(OpenCodeFileSetTransitionActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.FileSetActions);
        ArgumentNullException.ThrowIfNull(actions.FileSetActions.SetFileSet);
        ArgumentNullException.ThrowIfNull(actions.FileSetActions.RefreshOpenCodeAsync);
        ArgumentNullException.ThrowIfNull(actions.FileSetActions.SetStatus);
    }

    private static void ValidatePicker(OpenCodeFileSetPickerActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Validate(actions.TransitionActions);
    }
}
