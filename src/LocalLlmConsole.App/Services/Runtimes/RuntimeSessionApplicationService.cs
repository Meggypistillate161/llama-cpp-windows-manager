namespace LocalLlmConsole.Services;

public sealed record RuntimeSessionStopSelectedApplicationRequest(
    LoadedModelSessionSnapshot? SelectedSession,
    bool SelectedModelIsLoading);

public sealed record RuntimeSessionStopModelApplicationRequest(
    ModelRecord Model,
    LoadedModelSessionSnapshot? StoppedSession,
    bool ModelIsActive,
    bool ModelIsLoading);

public sealed class RuntimeSessionApplicationService
{
    private readonly RuntimeSessionCommandService _commands;
    private readonly RuntimeSessionFollowupApplicationService _followupApplication;

    public RuntimeSessionApplicationService(
        RuntimeSessionCommandService commands,
        RuntimeSessionFollowupApplicationService followupApplication)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _followupApplication = followupApplication ?? throw new ArgumentNullException(nameof(followupApplication));
    }

    public Task StopSelectedAsync(
        RuntimeSessionStopSelectedApplicationRequest request,
        RuntimeStopApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var decision = _commands.PlanStopSelected(
            request.SelectedSession,
            request.SelectedModelIsLoading);
        return _followupApplication.ApplyStopAsync(
            new RuntimeStopApplicationRequest(
                decision,
                request.SelectedSession,
                ResetMetricCountersBeforeStop: false,
                _commands.StopSelectedAsync),
            actions);
    }

    public Task StopModelAsync(
        RuntimeSessionStopModelApplicationRequest request,
        RuntimeStopApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.Model);

        var decision = _commands.PlanStopModel(
            request.Model,
            request.ModelIsActive,
            request.ModelIsLoading);
        return _followupApplication.ApplyStopAsync(
            new RuntimeStopApplicationRequest(
                decision,
                request.StoppedSession,
                ResetMetricCountersBeforeStop: true,
                () => _commands.StopModelAsync(request.Model.Id)),
            actions);
    }

    public Task SwitchToModelAsync(
        ModelRecord model,
        RuntimeSwitchApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(actions);

        return _followupApplication.ApplySwitchAsync(
            _commands.SwitchToModel(model),
            actions);
    }
}
