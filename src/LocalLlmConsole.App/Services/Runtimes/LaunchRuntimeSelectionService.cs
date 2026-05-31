namespace LocalLlmConsole.Services;

public sealed record LaunchRuntimeSelectorState(
    IReadOnlyList<RuntimeRecord> Runtimes,
    string? MissingRuntimeId,
    string? SelectedRuntimeId);

public sealed class LaunchRuntimeSelectionService
{
    public LaunchRuntimeSelectorState BuildSelectorState(
        IReadOnlyList<RuntimeRecord> runtimes,
        string? selectedRuntimeId)
    {
        ArgumentNullException.ThrowIfNull(runtimes);

        var requestedRuntimeId = (selectedRuntimeId ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(requestedRuntimeId))
        {
            var match = runtimes.FirstOrDefault(runtime => string.Equals(runtime.Id, requestedRuntimeId, StringComparison.OrdinalIgnoreCase));
            return match is null
                ? new LaunchRuntimeSelectorState(runtimes, requestedRuntimeId, requestedRuntimeId)
                : new LaunchRuntimeSelectorState(runtimes, null, match.Id);
        }

        return new LaunchRuntimeSelectorState(runtimes, null, runtimes.FirstOrDefault()?.Id);
    }

    public RuntimeRecord? Resolve(
        IReadOnlyList<RuntimeRecord> runtimes,
        string? runtimeId,
        RuntimeRecord? fallbackRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(runtimes);

        if (!string.IsNullOrWhiteSpace(runtimeId))
            return runtimes.FirstOrDefault(runtime => string.Equals(runtime.Id, runtimeId, StringComparison.OrdinalIgnoreCase));

        return fallbackRuntime ?? runtimes.FirstOrDefault();
    }

    public string MissingRuntimeStatus(IReadOnlyList<RuntimeRecord> runtimes, string? runtimeId)
    {
        ArgumentNullException.ThrowIfNull(runtimes);

        if (runtimes.Count == 0)
            return "Register a llama.cpp runtime first.";

        if (!string.IsNullOrWhiteSpace(runtimeId))
            return $"Saved runtime '{runtimeId}' is missing. Choose another runtime and save the model profile.";

        return "Choose a llama.cpp runtime before loading the model.";
    }
}
