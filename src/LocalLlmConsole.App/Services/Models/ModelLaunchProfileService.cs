namespace LocalLlmConsole.Services;

public sealed class ModelLaunchProfileService
{
    private readonly StateStore _stateStore;
    private readonly LoadedModelSessionManager _sessions;

    public ModelLaunchProfileService(StateStore stateStore, LoadedModelSessionManager sessions)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public Task<ModelLaunchSettings?> ReadAsync(ModelRecord model)
        => _stateStore.GetModelLaunchSettingsAsync(model.Id);

    public async Task<ModelLaunchSettings> DraftAsync(ModelRecord model, AppSettings defaults)
    {
        var profile = await ReadAsync(model);
        if (profile is not null) return profile;

        var port = await NextAvailablePortAsync(model.Id, defaults);
        return ModelLaunchSettings.FromAppSettings(defaults) with { Port = port };
    }

    public async Task<ModelLaunchSettings?> EnsureAsync(ModelRecord model, AppSettings defaults)
    {
        var profile = await ReadAsync(model);
        if (profile is { Port: >= 1 and <= 65535 }
            && await IsPortAvailableAsync(model.Id, profile.Port, defaults))
            return profile;

        var next = (profile ?? await DraftAsync(model, defaults)) with { Port = await NextAvailablePortAsync(model.Id, defaults) };
        await SaveAsync(model, next);
        return next;
    }

    public Task SaveAsync(ModelRecord model, ModelLaunchSettings settings)
        => _stateStore.SaveModelLaunchSettingsAsync(model.Id, settings);

    public async Task<bool> IsPortAvailableAsync(string modelId, int port, AppSettings settings)
    {
        if (port is < 1 or > 65535) return false;
        if (settings.AutoLoadGatewayEnabled && port == settings.AutoLoadGatewayPort) return false;

        foreach (var session in _sessions.Snapshots())
        {
            if (string.Equals(session.ModelId, modelId, StringComparison.OrdinalIgnoreCase)) continue;
            if (session.LaunchSettings.Port == port) return false;
        }

        foreach (var model in await _stateStore.ListModelsAsync())
        {
            if (string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)) continue;
            var profile = await _stateStore.GetModelLaunchSettingsAsync(model.Id);
            if (profile?.Port == port) return false;
        }

        return true;
    }

    public async Task<int> NextAvailablePortAsync(string modelId, AppSettings settings)
    {
        var used = new List<int>();
        if (settings.AutoLoadGatewayEnabled)
            used.Add(settings.AutoLoadGatewayPort);

        foreach (var session in _sessions.Snapshots())
        {
            if (!string.Equals(session.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
                used.Add(session.LaunchSettings.Port);
        }

        foreach (var model in await _stateStore.ListModelsAsync())
        {
            if (string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)) continue;
            var profile = await _stateStore.GetModelLaunchSettingsAsync(model.Id);
            if (profile is not null)
                used.Add(profile.Port);
        }

        return ModelPortAllocator.NextAvailable(settings.Port, used);
    }
}
