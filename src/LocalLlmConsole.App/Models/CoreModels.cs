
namespace LocalLlmConsole.Models;

public enum OwnershipKind
{
    AppOwned,
    External,
    RegistryOnly
}

public enum JobStatus
{
    Queued,
    Running,
    Paused,
    Cancelled,
    Failed,
    Completed,
    Interrupted
}

public enum RuntimeMode
{
    Native,
    Wsl
}

public enum RuntimeBackend
{
    Cpu,
    Cuda,
    Vulkan,
    Metal
}

public sealed record ModelRecord(
    string Id,
    string Name,
    string ModelPath,
    OwnershipKind Ownership,
    string MetadataJson,
    DateTimeOffset UpdatedAt);

public sealed record ActiveRuntimeSession(
    string ModelId,
    string RuntimeId,
    AppSettings LaunchSettings,
    string LogPath,
    DateTimeOffset StartedAt,
    string ProcessMarker = "",
    int ProcessId = 0);

public sealed record TokenUsageRecord(
    string ModelId,
    string ModelName,
    long PromptTokens,
    long GeneratedTokens,
    DateTimeOffset UpdatedAt);
