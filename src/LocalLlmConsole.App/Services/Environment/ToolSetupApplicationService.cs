namespace LocalLlmConsole.Services;

public enum ToolSetupApplicationOutcome
{
    MissingRequiredDistro,
    Cancelled,
    Started
}

public sealed record WindowsToolSetupApplicationActions(
    Func<WindowsToolSetupPlan, bool> Confirm,
    Action<string> SetStatus);

public sealed record WindowsToolRefreshApplicationActions(
    Func<string, Func<Task>, Task> RunBusyAsync,
    Func<Task<WindowsToolSnapshot>> RefreshToolsAsync,
    Action<WindowsToolSnapshot> StoreTools,
    Action<WindowsToolSnapshot> PopulatePage,
    Action<string> SetStatus);

public sealed class WindowsToolSetupApplicationService
{
    private readonly Func<WindowsToolSetupAction, WindowsToolSetupPlan> _plan;
    private readonly Action<WindowsToolSetupPlan> _execute;

    public WindowsToolSetupApplicationService(WindowsToolSetupWorkflowService workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        _plan = workflow.Plan;
        _execute = workflow.Execute;
    }

    public WindowsToolSetupApplicationService(
        Func<WindowsToolSetupAction, WindowsToolSetupPlan> plan,
        Action<WindowsToolSetupPlan> execute)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public ToolSetupApplicationOutcome Run(
        WindowsToolSetupAction action,
        WindowsToolSetupApplicationActions actions)
    {
        Validate(actions);
        var plan = _plan(action);
        if (!actions.Confirm(plan))
            return ToolSetupApplicationOutcome.Cancelled;

        _execute(plan);
        actions.SetStatus(plan.StartedStatus);
        return ToolSetupApplicationOutcome.Started;
    }

    public async Task<WindowsToolSnapshot> RefreshAsync(WindowsToolRefreshApplicationActions actions)
    {
        Validate(actions);
        WindowsToolSnapshot? result = null;
        await actions.RunBusyAsync("Detecting Windows build tools...", async () =>
        {
            var tools = await actions.RefreshToolsAsync();
            actions.StoreTools(tools);
            actions.PopulatePage(tools);
            actions.SetStatus(WindowsEnvironmentService.Status(tools));
            result = tools;
        });

        return result ?? throw new InvalidOperationException("Windows tool refresh did not produce a snapshot.");
    }

    private static void Validate(WindowsToolSetupApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.Confirm);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }

    private static void Validate(WindowsToolRefreshApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunBusyAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshToolsAsync);
        ArgumentNullException.ThrowIfNull(actions.StoreTools);
        ArgumentNullException.ThrowIfNull(actions.PopulatePage);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}

public sealed record WslToolSetupApplicationActions(
    Func<WslToolSetupPlan, bool> Confirm,
    Action<string> SetStatus);

public sealed class WslToolSetupApplicationService
{
    private readonly Func<WslToolSetupAction, bool> _requiresUbuntuDistro;
    private readonly Func<WslToolSetupAction, string, string, WslToolSetupPlan> _plan;
    private readonly Action<WslToolSetupPlan> _execute;

    public WslToolSetupApplicationService(WslToolSetupWorkflowService workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        _requiresUbuntuDistro = workflow.RequiresUbuntuDistro;
        _plan = workflow.Plan;
        _execute = workflow.Execute;
    }

    public WslToolSetupApplicationService(
        Func<WslToolSetupAction, bool> requiresUbuntuDistro,
        Func<WslToolSetupAction, string, string, WslToolSetupPlan> plan,
        Action<WslToolSetupPlan> execute)
    {
        _requiresUbuntuDistro = requiresUbuntuDistro ?? throw new ArgumentNullException(nameof(requiresUbuntuDistro));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public ToolSetupApplicationOutcome Run(
        WslToolSetupAction action,
        string distroName,
        string appDisplayName,
        WslToolSetupApplicationActions actions)
    {
        Validate(actions);
        if (_requiresUbuntuDistro(action) && string.IsNullOrWhiteSpace(distroName))
        {
            actions.SetStatus("Install or select an Ubuntu distro first.");
            return ToolSetupApplicationOutcome.MissingRequiredDistro;
        }

        var plan = _plan(action, distroName, appDisplayName);
        if (!actions.Confirm(plan))
            return ToolSetupApplicationOutcome.Cancelled;

        _execute(plan);
        actions.SetStatus(plan.StartedStatus);
        return ToolSetupApplicationOutcome.Started;
    }

    private static void Validate(WslToolSetupApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.Confirm);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
