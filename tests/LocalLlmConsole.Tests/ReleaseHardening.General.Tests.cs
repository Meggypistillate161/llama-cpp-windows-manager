using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public async Task CorruptSettingsAreBackedUpAndDefaulted()
    {
        var root = CreateTempRoot();
        var databasePath = Path.Combine(root, "state", "local-llm-console.db");
        await using var store = new StateStore(databasePath);
        await store.InitializeAsync();

        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
INSERT INTO settings (key, value_json, updated_at)
VALUES ('port', '"not-a-port"', $updated_at)
ON CONFLICT(key) DO UPDATE SET value_json = excluded.value_json, updated_at = excluded.updated_at;
""";
            command.Parameters.AddWithValue("$updated_at", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var settings = await store.GetAppSettingsAsync(root);

        Assert.Equal(AppSettings.CreateDefault(root).Port, settings.Port);
        Assert.True(Directory.EnumerateFiles(Path.Combine(root, "state", "corrupt-settings"), "*.json").Any());
    }


    [Fact]
    public void LargeServicesStaySplitByResponsibility()
    {
        var servicesRoot = Path.GetDirectoryName(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "StateStore.cs"))!;
        var appRoot = Directory.GetParent(servicesRoot)!.FullName;

        AssertServicePartials(appRoot, "Services", "HuggingFaceService", 350,
            "HuggingFaceService.Downloads.cs",
            "HuggingFaceService.Search.cs",
            "HuggingFaceService.Safety.cs",
            "HuggingFaceService.LaunchProfiles.cs",
            "HuggingFaceService.Projectors.cs");
        AssertServicePartials(appRoot, "Services", "StateStore", 380,
            "StateStore.Catalog.cs",
            "StateStore.Settings.cs",
            "StateStore.Jobs.cs");
        AssertServicePartials(appRoot, "Services", "OpenCodeConfigService", 380,
            "OpenCodeConfigService.Models.cs",
            "OpenCodeConfigService.Agents.cs",
            "OpenCodeConfigService.Json.cs",
            "OpenCodeConfigService.ModelEnvelopes.cs",
            "OpenCodeConfigService.Providers.cs",
            "OpenCodeConfigService.Discovery.cs");
        AssertServicePartials(appRoot, "Services", "ModelCatalogService", 380,
            "ModelCatalogService.Legacy.cs");
        AssertServicePartials(appRoot, "Services", "LlamaProcessSupervisor", 260,
            "LlamaProcessSupervisor.Launch.cs",
            "LlamaProcessSupervisor.Wsl.cs");
        AssertServicePartials(appRoot, "Services", "HuggingFaceLaunchSettingsSuggester", 260,
            "HuggingFaceLaunchSettingsSuggester.Config.cs",
            "HuggingFaceLaunchSettingsSuggester.CommandExtraction.cs",
            "HuggingFaceLaunchSettingsSuggester.ShellParsing.cs");

        var modelsRoot = Path.Combine(appRoot, "Models");
        var modelFiles = Directory.EnumerateFiles(modelsRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Name = Path.GetFileName(path), Lines = File.ReadAllLines(path).Length })
            .ToArray();
        var oversizedModels = modelFiles
            .Where(file => file.Lines > 250)
            .Select(file => $"{file.Name}:{file.Lines}")
            .ToArray();
        var modelFileNames = modelFiles.Select(file => file.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Empty(oversizedModels);
        Assert.Contains("AppSettings.cs", modelFileNames);
        Assert.Contains("ModelLaunchSettings.cs", modelFileNames);
        Assert.Contains("RuntimeModels.cs", modelFileNames);
        Assert.Contains("CoreModels.cs", modelFileNames);
    }


    [Fact]
    public void GlobalUsingsDoNotLeakWpfIntoServices()
    {
        var globalUsings = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "GlobalUsings.cs"));

        Assert.DoesNotContain("global using System.Windows;", globalUsings, StringComparison.Ordinal);
        Assert.DoesNotContain("global using System.Windows.Controls;", globalUsings, StringComparison.Ordinal);
        Assert.DoesNotContain("global using Forms =", globalUsings, StringComparison.Ordinal);
        Assert.DoesNotContain("global using Wpf", globalUsings, StringComparison.Ordinal);
    }


    [Fact]
    public void LocalAppServiceObservesRequestHandlerTasks()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LocalAppService.cs"));

        Assert.Contains("QueueRequest(context, cancellationToken)", source, StringComparison.Ordinal);
        Assert.Contains("_requestHandlers", source, StringComparison.Ordinal);
        Assert.Contains("ObserveCompletionAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = Task.Run(() => HandleAsync", source, StringComparison.Ordinal);
    }


    [Fact]
    public void ModelLaunchDefaultsUseHighContextGpuAndQ8Cache()
    {
        var root = CreateTempRoot();

        var settings = AppSettings.CreateDefault(root);
        var modelSettings = ModelLaunchSettings.FromAppSettings(settings);

        Assert.Equal(131_072, settings.ContextSize);
        Assert.Equal(999, settings.GpuLayers);
        Assert.Equal(4096, settings.BatchSize);
        Assert.Equal("q8_0", settings.CacheTypeK);
        Assert.Equal("q8_0", settings.CacheTypeV);
        Assert.Equal(0.65, settings.Temperature);
        Assert.Equal(settings.ContextSize, modelSettings.ContextSize);
        Assert.Equal(settings.GpuLayers, modelSettings.GpuLayers);
        Assert.Equal(settings.BatchSize, modelSettings.BatchSize);
        Assert.Equal(settings.CacheTypeK, modelSettings.CacheTypeK);
        Assert.Equal(settings.CacheTypeV, modelSettings.CacheTypeV);
        Assert.Equal(settings.Temperature, modelSettings.Temperature);
        Assert.Equal("none", settings.SpeculativeType);
        Assert.Equal("q8_0", settings.SpecDraftCacheTypeK);
        Assert.Equal("q8_0", settings.SpecDraftCacheTypeV);
        Assert.Equal(-1, settings.Seed);
        Assert.Equal(-1, settings.MaxTokens);
        Assert.Equal(0, settings.VisionImageMinTokens);
        Assert.Equal(0, settings.VisionImageMaxTokens);
        Assert.Equal(settings.VisionImageMinTokens, modelSettings.VisionImageMinTokens);
        Assert.Equal(settings.VisionImageMaxTokens, modelSettings.VisionImageMaxTokens);
        Assert.Equal("auto", settings.RopeScaling);
    }


    [Fact]
    public void ModelSettingsDefaultToSimpleWithRequestedAdvancedGroups()
    {
        var source = ReadMainWindowSources();

        Assert.Contains("private bool _showAdvancedLaunchSettings;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private bool _showAdvancedLaunchSettings = true", source, StringComparison.Ordinal);
        Assert.Contains("panel.Children.Add(LaunchSection(\"Basic Launch\", basicGrid));", source, StringComparison.Ordinal);
        Assert.Contains("panel.Children.Add(LaunchSection(\"Performance & Memory\", memoryGrid));", source, StringComparison.Ordinal);
        Assert.Contains("panel.Children.Add(LaunchSection(\"Speculative / MTP\", speculativeGrid));", source, StringComparison.Ordinal);
        Assert.Contains("panel.Children.Add(LaunchSection(\"Chat & Model Capabilities\", chatGrid));", source, StringComparison.Ordinal);
        Assert.Contains("panel.Children.Add(LaunchSection(\"Generation Defaults\", generationGrid));", source, StringComparison.Ordinal);
        Assert.Contains("AddLaunchSetting(chatGrid, \"Image min\", _visionImageMinTokensBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddLaunchSetting(chatGrid, \"Image max\", _visionImageMaxTokensBox);", source, StringComparison.Ordinal);
        Assert.Contains("SetLaunchSettingVisible(\"Image min\", _selectedModelCapabilities.LikelyVision);", source, StringComparison.Ordinal);
        Assert.Contains("SetLaunchSettingVisible(\"Image max\", _selectedModelCapabilities.LikelyVision);", source, StringComparison.Ordinal);

        Assert.Contains("var ropeSection = LaunchSection(\"Context Extension\", ropeGrid);", source, StringComparison.Ordinal);
        Assert.Contains("var serverSection = LaunchSection(\"Server\", serverGrid);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(memoryGrid, \"KV offload\", _kvOffloadCombo);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(memoryGrid, \"Unified KV\", _kvUnifiedCombo);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(memoryGrid, \"Memory map\", _mmapCombo);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(memoryGrid, \"Memory lock\", _mlockCombo);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(speculativeGrid, \"Draft GPU\", _specDraftGpuLayersBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(speculativeGrid, \"Split prob\", _specDraftPSplitBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(speculativeGrid, \"Min prob\", _specDraftPMinBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(generationGrid, \"Max tokens\", _maxTokensBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(generationGrid, \"Seed\", _seedBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(generationGrid, \"Repeat window\", _repeatLastNBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(generationGrid, \"Repeat pen\", _repeatPenaltyBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(generationGrid, \"Presence\", _presencePenaltyBox);", source, StringComparison.Ordinal);
        Assert.Contains("AddAdvancedLaunchSetting(generationGrid, \"Frequency\", _frequencyPenaltyBox);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Performance & Memory - Advanced", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Speculative / MTP - Advanced", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Generation Defaults - Advanced", source, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("196", 200704)]
    [InlineData("196k", 200704)]
    [InlineData("196K", 200704)]
    [InlineData("196.5k", 201728)]
    [InlineData("196000", 195584)]
    [InlineData("128,000", 128000)]
    [InlineData("1m", 1048576)]
    [InlineData("0", 0)]
    public void ContextSizeParserNormalizesShorthandToLlamaFriendlySteps(string text, int expected)
    {
        var ok = LaunchSettingParser.TryNormalizeContextSize(text, out var value);

        Assert.True(ok);
        Assert.Equal(expected, value);
    }


    [Fact]
    public void LaunchSettingParserValidatesNumericInputs()
    {
        Assert.Equal(200704, LaunchSettingParser.ReadContextSize("196k"));
        Assert.Equal(4, LaunchSettingParser.ReadInt("4", "Threads", 0, 8));
        Assert.Equal(0.75, LaunchSettingParser.ReadDouble("0.75", "Top P", 0, 1), precision: 3);
        Assert.Contains("whole number", Assert.Throws<InvalidOperationException>(() => LaunchSettingParser.ReadInt("1.5", "Threads", 0)).Message, StringComparison.Ordinal);
        Assert.Contains("at least 0", Assert.Throws<InvalidOperationException>(() => LaunchSettingParser.ReadDouble("-0.1", "Top P", 0, 1)).Message, StringComparison.Ordinal);
        Assert.Contains("no more than 1", Assert.Throws<InvalidOperationException>(() => LaunchSettingParser.ReadDouble("1.2", "Top P", 0, 1)).Message, StringComparison.Ordinal);
    }


    [Fact]
    public void LaunchSettingMetadataOwnsTooltipsAndContextSuggestions()
    {
        Assert.Contains("conversation history", LaunchSettingMetadataService.Tooltip("Context size"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Setting used", LaunchSettingMetadataService.Tooltip("Unknown setting"), StringComparison.Ordinal);
        Assert.Contains("Suggestion: 200,704 tokens.", LaunchSettingMetadataService.ContextSizeTooltip("196k"), StringComparison.Ordinal);
        Assert.DoesNotContain("Suggestion:", LaunchSettingMetadataService.ContextSizeTooltip("200704"), StringComparison.Ordinal);
        Assert.DoesNotContain("Suggestion:", LaunchSettingMetadataService.ContextSizeTooltip("200_704"), StringComparison.Ordinal);
        Assert.Contains("q4_0", LaunchSettingMetadataService.CacheTypeOptions);
        Assert.Contains("draft-mtp", LaunchSettingMetadataService.SpeculativeTypeOptions);
        Assert.Contains("dynamic-resolution vision", LaunchSettingMetadataService.Tooltip("Image min"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model default", LaunchSettingMetadataService.Tooltip("Image max"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("auto", LaunchSettingMetadataService.AutoOnOffOptions[0]);
        Assert.Equal("on", LaunchSettingMetadataService.OnOffOptions[0]);
    }


    [Fact]
    public void AppOwnedDeletionRejectsRootAndOutsidePaths()
    {
        var root = CreateTempRoot();
        var model = new ModelRecord(
            "model",
            "Model",
            Path.Combine(root, "models", "model.gguf"),
            OwnershipKind.AppOwned,
            "{}",
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => FileOwnershipService.EnsureDeletionAllowed(model, root, root));
        Assert.Throws<InvalidOperationException>(() => FileOwnershipService.EnsureDeletionAllowed(model, Path.GetTempPath(), root));

        var appOwnedChild = Path.Combine(root, "models", "model");
        FileOwnershipService.EnsureDeletionAllowed(model, appOwnedChild, root);
    }


    [Fact]
    public void AppOwnedDeletionRejectsExistingFolderThatDoesNotContainModel()
    {
        var root = CreateTempRoot();
        var target = Path.Combine(root, "models", "different-model");
        Directory.CreateDirectory(target);
        var model = new ModelRecord(
            "model",
            "Model",
            Path.Combine(root, "models", "model.gguf"),
            OwnershipKind.AppOwned,
            "{}",
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => FileOwnershipService.EnsureDeletionAllowed(model, target, root));
    }

}
