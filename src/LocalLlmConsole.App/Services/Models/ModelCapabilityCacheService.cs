using System.Collections.Concurrent;

namespace LocalLlmConsole.Services;

public sealed class ModelCapabilityCacheService
{
    private readonly ConcurrentDictionary<string, ModelCapabilitySummary> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<ModelRecord, string> _cacheKeyReader;
    private readonly Func<ModelRecord, ModelCapabilitySummary> _inspector;

    public ModelCapabilityCacheService()
        : this(ModelCapabilityService.CacheKey, ModelCapabilityService.Inspect)
    {
    }

    public ModelCapabilityCacheService(
        Func<ModelRecord, string> cacheKeyReader,
        Func<ModelRecord, ModelCapabilitySummary> inspector)
    {
        _cacheKeyReader = cacheKeyReader ?? throw new ArgumentNullException(nameof(cacheKeyReader));
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
    }

    public async Task<ModelCapabilitySummary> ReadAsync(
        ModelRecord model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var cacheKey = await Task.Run(() => _cacheKeyReader(model), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var inspected = await Task.Run(() => _inspector(model), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.GetOrAdd(cacheKey, inspected);
    }

    public void Clear() => _cache.Clear();
}
