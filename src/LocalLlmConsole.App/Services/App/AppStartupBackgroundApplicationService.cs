namespace LocalLlmConsole.Services;

public delegate Task<int> SuggestedLaunchProfileSeeder(AppSettings settings, CancellationToken cancellationToken);

public sealed record AppStartupSuggestedLaunchProfileSeedRequest(
    AppSettings Settings,
    SuggestedLaunchProfileSeeder? SeedAsync,
    TimeSpan? Timeout = null);

public sealed record AppStartupSuggestedLaunchProfileSeedResult(
    int SeededCount,
    string StatusMessage)
{
    public bool ShouldRefreshLaunchSettings => SeededCount > 0;

    public static AppStartupSuggestedLaunchProfileSeedResult None { get; } = new(0, "");
}

public sealed class AppStartupBackgroundApplicationService
{
    private static readonly TimeSpan DefaultSeedTimeout = TimeSpan.FromSeconds(20);

    public async Task<AppStartupSuggestedLaunchProfileSeedResult> SeedSuggestedLaunchProfilesAsync(
        AppStartupSuggestedLaunchProfileSeedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Settings);

        if (request.SeedAsync is null)
            return AppStartupSuggestedLaunchProfileSeedResult.None;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout is { } configured && configured > TimeSpan.Zero
            ? configured
            : DefaultSeedTimeout);

        try
        {
            var seeded = await request.SeedAsync(request.Settings, timeout.Token);
            if (seeded <= 0)
                return AppStartupSuggestedLaunchProfileSeedResult.None;

            return new AppStartupSuggestedLaunchProfileSeedResult(
                seeded,
                $"Applied Hugging Face suggested launch defaults for {seeded} model{(seeded == 1 ? "" : "s")}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AppStartupSuggestedLaunchProfileSeedResult.None;
        }
        catch
        {
            return AppStartupSuggestedLaunchProfileSeedResult.None;
        }
    }
}
