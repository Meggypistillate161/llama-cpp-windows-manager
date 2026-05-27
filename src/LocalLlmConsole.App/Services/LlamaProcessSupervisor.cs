
namespace LocalLlmConsole.Services;

public enum LlamaRuntimeState
{
    Stopped,
    Loading,
    Loaded,
    Failed
}
public sealed partial class LlamaProcessSupervisor : IDisposable
{
    private Process? _process;
    private BoundedLogWriter? _log;
    private bool _attached;
    private bool _recovered;
    private RuntimeMode _lastRuntimeMode;

    public bool IsRunning => _process is { HasExited: false } || _attached;
    public bool IsRecovered => _recovered;
    public string ActiveModelId { get; private set; } = "";
    public string ActiveRuntimeId { get; private set; } = "";
    public string LogPath { get; private set; } = "";
    public LlamaRuntimeState State { get; private set; } = LlamaRuntimeState.Stopped;
    public int? LastExitCode { get; private set; }
    public int ProcessId
    {
        get
        {
            try { return _process?.Id ?? 0; }
            catch { return 0; }
        }
    }
    public string WslProcessMarker => _lastWslProcessMarker;
    private AppSettings? _lastSettings;
    private string _lastRuntimeExecutablePath = "";
    private string _lastWslProcessMarker = "";
    private string _lastApiKey = "";

    public Task StartAsync(RuntimeRecord runtime, ModelRecord model, AppSettings settings, string logRoot)
    {
        Stop();
        Directory.CreateDirectory(logRoot);
        LogPath = Path.Combine(logRoot, $"llama-server-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");
        _log = new BoundedLogWriter(LogPath, BoundedLogFile.MegabytesToBytes(settings.MaxLogFileSizeMb));
        State = LlamaRuntimeState.Loading;
        LastExitCode = null;
        _attached = false;
        _recovered = false;
        _lastRuntimeMode = runtime.Mode;

        var executable = runtime.Mode == RuntimeMode.Wsl ? ToWslPath(runtime.ExecutablePath) : runtime.ExecutablePath;
        _lastRuntimeExecutablePath = executable;
        _lastWslProcessMarker = runtime.Mode == RuntimeMode.Wsl ? $"local-llm-console-llama-{Guid.NewGuid():N}" : "";
        var modelPath = runtime.Mode == RuntimeMode.Wsl ? ToWslPath(model.ModelPath) : model.ModelPath;
        var mmprojPath = ModelCatalogService.FindVisionProjector(model.ModelPath);
        var visionProjectorPath = string.IsNullOrWhiteSpace(mmprojPath)
            ? null
            : runtime.Mode == RuntimeMode.Wsl ? ToWslPath(mmprojPath) : mmprojPath;
        var draftModelPath = ResolveDraftModelPath(model.ModelPath, settings.SpecDraftModelPath, settings.SpeculativeType);
        var launchDraftModelPath = string.IsNullOrWhiteSpace(draftModelPath)
            ? null
            : runtime.Mode == RuntimeMode.Wsl ? ToWslPath(draftModelPath) : draftModelPath;
        var request = new RuntimeLaunchRequest
        {
            Mode = runtime.Mode,
            Backend = runtime.Backend,
            ExecutablePath = executable,
            ModelPath = modelPath,
            WslDistro = runtime.Mode == RuntimeMode.Wsl ? settings.WslDistro : "",
            Host = settings.Host,
            AllowNetworkAccess = string.Equals(settings.ModelAccessMode, "lan", StringComparison.OrdinalIgnoreCase),
            ApiKey = settings.ModelApiKey,
            Port = settings.Port,
            ContextSize = settings.ContextSize,
            GpuLayers = settings.GpuLayers,
            ParallelSlots = settings.ParallelSlots,
            BatchSize = settings.BatchSize,
            MicroBatchSize = settings.MicroBatchSize,
            Threads = settings.Threads,
            FlashAttention = settings.FlashAttention,
            CacheTypeK = settings.CacheTypeK,
            CacheTypeV = settings.CacheTypeV,
            KvOffload = settings.KvOffload,
            KvUnified = settings.KvUnified,
            ContinuousBatching = settings.ContinuousBatching,
            ReasoningMode = settings.ReasoningMode,
            ReasoningFormat = settings.ReasoningFormat,
            ReasoningBudget = settings.ReasoningBudget,
            VisionMode = settings.VisionMode,
            VisionProjectorPath = visionProjectorPath,
            VisionImageMinTokens = settings.VisionImageMinTokens,
            VisionImageMaxTokens = settings.VisionImageMaxTokens,
            JinjaMode = settings.JinjaMode,
            MmapMode = settings.MmapMode,
            MlockMode = settings.MlockMode,
            Temperature = settings.Temperature,
            TopK = settings.TopK,
            TopP = settings.TopP,
            MinP = settings.MinP,
            MaxTokens = settings.MaxTokens,
            Seed = settings.Seed,
            RepeatLastN = settings.RepeatLastN,
            RepeatPenalty = settings.RepeatPenalty,
            PresencePenalty = settings.PresencePenalty,
            FrequencyPenalty = settings.FrequencyPenalty,
            RopeScaling = settings.RopeScaling,
            RopeScale = settings.RopeScale,
            RopeFreqBase = settings.RopeFreqBase,
            RopeFreqScale = settings.RopeFreqScale,
            SpeculativeType = settings.SpeculativeType,
            SpecDraftModelPath = launchDraftModelPath,
            SpecDraftGpuLayers = settings.SpecDraftGpuLayers,
            SpecDraftMinTokens = settings.SpecDraftMinTokens,
            SpecDraftMaxTokens = settings.SpecDraftMaxTokens,
            SpecDraftPSplit = settings.SpecDraftPSplit,
            SpecDraftPMin = settings.SpecDraftPMin,
            SpecDraftCacheTypeK = settings.SpecDraftCacheTypeK,
            SpecDraftCacheTypeV = settings.SpecDraftCacheTypeV,
            ExtraArgs = settings.EnableMetrics ? new[] { "--metrics" } : Array.Empty<string>()
        };
        _lastApiKey = settings.ModelApiKey ?? "";
        var args = RuntimeAdapter.BuildArgs(request);
        var psi = runtime.Mode == RuntimeMode.Wsl
            ? new ProcessStartInfo(HostExecutableResolver.WslExe())
            : new ProcessStartInfo(runtime.ExecutablePath);
        if (runtime.Mode == RuntimeMode.Wsl)
        {
            var executableDir = WslDirectoryName(executable);
            var runtimeLibDir = WslSiblingDirectory(executableDir, "lib");
            var libraryPath = string.IsNullOrWhiteSpace(executableDir)
                ? "$LD_LIBRARY_PATH"
                : $"{BashQuote(executableDir)}:{BashQuote(runtimeLibDir)}:${{LD_LIBRARY_PATH:-}}";
            var argv0 = string.IsNullOrWhiteSpace(_lastWslProcessMarker) ? "" : $" -a {BashQuote(_lastWslProcessMarker)}";
            var syclEnv = WslSyclEnvironmentPrefix(runtime.Backend);
            var command = $"{syclEnv}export LD_LIBRARY_PATH={libraryPath}; cd {BashQuote(string.IsNullOrWhiteSpace(executableDir) ? "/" : executableDir)}; exec{argv0} {BashQuote(executable)} {string.Join(" ", args.Select(BashQuote))}";
            psi.ArgumentList.Add("-d");
            psi.ArgumentList.Add(settings.WslDistro);
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add("bash");
            psi.ArgumentList.Add("-lc");
            psi.ArgumentList.Add(command);
        }
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        if (runtime.Mode == RuntimeMode.Native)
        {
            psi.WorkingDirectory = Path.GetDirectoryName(runtime.ExecutablePath) ?? Environment.CurrentDirectory;
            if (runtime.Backend == RuntimeBackend.Sycl)
                ApplyNativeSyclEnvironment(psi);
        }
        if (runtime.Mode == RuntimeMode.Native)
        {
            foreach (var arg in args) psi.ArgumentList.Add(arg);
        }
        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => ObserveOutput(e.Data);
        _process.ErrorDataReceived += (_, e) => ObserveOutput(e.Data);
        _process.Exited += (_, _) =>
        {
            try { LastExitCode = _process?.ExitCode; } catch { }
            if (State != LlamaRuntimeState.Stopped)
                State = LlamaRuntimeState.Failed;
        };
        try
        {
            if (!_process.Start())
            {
                State = LlamaRuntimeState.Failed;
                throw new InvalidOperationException("Failed to start llama-server.");
            }
        }
        catch
        {
            State = LlamaRuntimeState.Failed;
            throw;
        }
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        ActiveModelId = model.Id;
        ActiveRuntimeId = runtime.Id;
        _lastSettings = settings;
        return Task.CompletedTask;
    }

    public bool MarkLoadedIfRunning()
    {
        if (!IsRunning || State != LlamaRuntimeState.Loading) return false;
        State = LlamaRuntimeState.Loaded;
        LastExitCode = null;
        return true;
    }

    private void ObserveOutput(string? line)
    {
        if (line is null) return;
        try { _log?.WriteLine(RedactRuntimeLogLine(line)); } catch { }
        if (State == LlamaRuntimeState.Loading && LooksLoaded(line))
            State = LlamaRuntimeState.Loaded;
    }

    private string RedactRuntimeLogLine(string line)
        => LogFileService.RedactSensitiveText(line, _lastApiKey);

    private static bool LooksLoaded(string line)
        => line.Contains("HTTP server", StringComparison.OrdinalIgnoreCase) && line.Contains("listening", StringComparison.OrdinalIgnoreCase)
            || line.Contains("server is listening", StringComparison.OrdinalIgnoreCase)
            || line.Contains("listening on", StringComparison.OrdinalIgnoreCase)
            || line.Contains("server started", StringComparison.OrdinalIgnoreCase)
            || line.Contains("server listening", StringComparison.OrdinalIgnoreCase)
            || line.Contains("model loaded", StringComparison.OrdinalIgnoreCase);

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch { }
        if (_lastSettings is not null && _lastRuntimeMode == RuntimeMode.Wsl)
        {
            TryStopWslLlama(_lastSettings, _lastRuntimeExecutablePath, _lastWslProcessMarker);
        }
        try { _process?.Dispose(); } catch { }
        try { _log?.Dispose(); } catch { }
        _process = null;
        _log = null;
        ActiveModelId = "";
        ActiveRuntimeId = "";
        State = LlamaRuntimeState.Stopped;
        LastExitCode = null;
        _lastSettings = null;
        _lastRuntimeExecutablePath = "";
        _lastWslProcessMarker = "";
        _lastApiKey = "";
        _attached = false;
        _recovered = false;
    }

    public void Dispose() => Stop();
}
