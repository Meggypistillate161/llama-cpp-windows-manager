namespace LocalLlmConsole.Services;

public sealed record SelectedModelCapabilityState(
    ModelCapabilitySummary Capabilities,
    string DisplayText,
    bool VisionLaunchSettingsAvailable);

public sealed class SelectedModelCapabilityController
{
    public const string NoModelText = "No model selected";

    public ModelCapabilitySummary Capabilities { get; private set; } = ModelCapabilityService.Empty();

    public string DisplayText { get; private set; } = NoModelText;

    public bool VisionLaunchSettingsAvailable => Capabilities.LikelyVision;

    public SelectedModelCapabilityState Apply(ModelRecord? model, ModelCapabilitySummary capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        Capabilities = capabilities;
        DisplayText = model is null
            ? NoModelText
            : ModelCapabilityService.SummaryText(capabilities);
        return Snapshot();
    }

    public SelectedModelCapabilityState Snapshot()
        => new(Capabilities, DisplayText, VisionLaunchSettingsAvailable);
}
