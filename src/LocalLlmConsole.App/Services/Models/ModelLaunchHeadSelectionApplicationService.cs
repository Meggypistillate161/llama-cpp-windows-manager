namespace LocalLlmConsole.Services;

public sealed record LaunchHeadSelectionRequest(ModelRecord? SelectedModel, string ModelsRoot);

public sealed record LaunchHeadSelectionActions(
    Func<OpenFilePickerRequest, string?> PickOpenFile,
    Action<string> ApplySelectedPath);

public enum LaunchHeadSelectionOutcome
{
    Cancelled,
    Applied
}

public sealed class ModelLaunchHeadSelectionApplicationService
{
    private const string GgufFilter = "GGUF files|*.gguf|All files|*.*";

    public LaunchHeadSelectionOutcome ChooseVisionProjector(
        LaunchHeadSelectionRequest request,
        LaunchHeadSelectionActions actions)
        => Choose(
            request,
            actions,
            "Choose vision head/projector GGUF",
            selected => NormalizeVisionProjectorSelection(request.SelectedModel, selected));

    public LaunchHeadSelectionOutcome ChooseMtpHead(
        LaunchHeadSelectionRequest request,
        LaunchHeadSelectionActions actions)
        => Choose(
            request,
            actions,
            "Choose MTP head GGUF",
            selected => selected);

    public OpenFilePickerRequest BuildPickerRequest(LaunchHeadSelectionRequest request, string title)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new OpenFilePickerRequest(
            title,
            GgufFilter,
            CheckFileExists: true,
            AddExtension: false,
            DefaultExt: ".gguf",
            FileName: "",
            InitialDirectory: FileSystemDialogService.ExistingDirectoryOrEmpty(ModelFolder(request)));
    }

    public static string NormalizeVisionProjectorSelection(ModelRecord? selectedModel, string selectedPath)
    {
        if (selectedModel is not null
            && VisionProjectorSelection.IsEmbeddedOrMainModel(selectedModel.ModelPath, selectedPath))
            return VisionProjectorSelection.EmbeddedToken;

        return selectedPath;
    }

    private LaunchHeadSelectionOutcome Choose(
        LaunchHeadSelectionRequest request,
        LaunchHeadSelectionActions actions,
        string title,
        Func<string, string> normalize)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(actions);
        ArgumentNullException.ThrowIfNull(normalize);

        var selected = actions.PickOpenFile(BuildPickerRequest(request, title));
        if (string.IsNullOrWhiteSpace(selected))
            return LaunchHeadSelectionOutcome.Cancelled;

        actions.ApplySelectedPath(normalize(selected));
        return LaunchHeadSelectionOutcome.Applied;
    }

    private static string ModelFolder(LaunchHeadSelectionRequest request)
    {
        if (request.SelectedModel is { } model)
            return Path.GetDirectoryName(model.ModelPath) ?? request.ModelsRoot;

        return request.ModelsRoot;
    }

    private static void Validate(LaunchHeadSelectionActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.PickOpenFile);
        ArgumentNullException.ThrowIfNull(actions.ApplySelectedPath);
    }
}
