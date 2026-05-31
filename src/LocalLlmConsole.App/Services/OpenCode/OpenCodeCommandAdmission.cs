namespace LocalLlmConsole.Services;

public sealed record OpenCodeCommandConfirmation(
    string Title,
    string Message);

public sealed record OpenCodeModelCommandAdmission(
    OpenCodeModelEntry? Model,
    string StatusMessage,
    OpenCodeCommandConfirmation? Confirmation)
{
    public bool CanRun => Model is not null && string.IsNullOrWhiteSpace(StatusMessage);
}

public sealed record OpenCodeAgentCommandAdmission(
    OpenCodeAgentEntry? Agent,
    string StatusMessage,
    OpenCodeCommandConfirmation? Confirmation)
{
    public bool CanRun => Agent is not null && string.IsNullOrWhiteSpace(StatusMessage);
}

public sealed record OpenCodeAgentCreateAdmission(
    OpenCodeNewAgentDraft Draft,
    string StatusMessage,
    OpenCodeCommandConfirmation? Confirmation)
{
    public bool CanRun => Draft.IsValid && string.IsNullOrWhiteSpace(StatusMessage);
}
