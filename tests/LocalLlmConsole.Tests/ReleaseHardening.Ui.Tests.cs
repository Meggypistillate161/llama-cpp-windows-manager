using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public void SettingsPageDoesNotExposeCacheFolder()
    {
        var source = ReadMainWindowSources();

        Assert.DoesNotContain("\"Cache folder\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cacheRoot\"", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MainWindowHasVisibleAppStatusLine()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.xaml"));
        var source = ReadMainWindowSources();

        Assert.Contains("x:Name=\"AppStatusText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Current action", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ServiceStatusText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ServiceStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeStatusText", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeStatusText", source, StringComparison.Ordinal);
        Assert.Contains("AppStatusText.Text", source, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.Yield", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MainWindowUsesLlamaCppWindowsManagerBrandingAndIcon()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.xaml"));
        var source = ReadMainWindowSources();
        var project = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "LocalLlmConsole.App.csproj"));
        var iconPath = FindRepositoryFile("src", "LocalLlmConsole.App", "Assets", "AppIcon.ico");

        Assert.Contains("Title=\"llama.cpp Windows Manager v1.1.3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"v1.1.3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AppDisplayName = \"llama.cpp Windows Manager\"", source, StringComparison.Ordinal);
        Assert.Contains("AppVersionLabel = \"v1.1.3\"", source, StringComparison.Ordinal);
        Assert.Contains("<AssemblyName>LlamaCppWindowsManager</AssemblyName>", project, StringComparison.Ordinal);
        Assert.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.True(new FileInfo(iconPath).Length > 1024);
    }


    [Fact]
    public void MainWindowCodeBehindStaysSplitByWorkflow()
    {
        var mainWindowPath = FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.xaml.cs");
        var appRoot = Path.GetDirectoryName(mainWindowPath)!;
        var partialNames = Directory.EnumerateFiles(appRoot, "MainWindow*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var oversizedPartials = Directory.EnumerateFiles(appRoot, "MainWindow*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Name = Path.GetFileName(path), Lines = File.ReadAllLines(path).Length })
            .Where(file => !string.Equals(file.Name, "MainWindow.xaml.cs", StringComparison.OrdinalIgnoreCase) && file.Lines > 500)
            .Select(file => $"{file.Name}:{file.Lines}")
            .ToArray();

        Assert.True(File.ReadAllLines(mainWindowPath).Length < 300);
        Assert.Empty(oversizedPartials);
        Assert.Contains("MainWindow.State.cs", partialNames);
        Assert.Contains("MainWindow.Navigation.cs", partialNames);
        Assert.Contains("MainWindow.Help.cs", partialNames);
        Assert.DoesNotContain("MainWindow.HelpSections.cs", partialNames);
        Assert.Contains("MainWindow.Pages.cs", partialNames);
        Assert.Contains("MainWindow.FolderSettings.cs", partialNames);
        Assert.Contains("MainWindow.Wsl.cs", partialNames);
        Assert.Contains("MainWindow.WslActions.cs", partialNames);
        Assert.Contains("MainWindow.OpenCode.cs", partialNames);
        Assert.Contains("MainWindow.OpenCodeActions.cs", partialNames);
        Assert.Contains("MainWindow.OpenCodeAgents.cs", partialNames);
        Assert.Contains("MainWindow.OpenCodeFiles.cs", partialNames);
        Assert.Contains("MainWindow.OpenCodeLocalModels.cs", partialNames);
        Assert.Contains("MainWindow.ModelLaunchProfiles.cs", partialNames);
        Assert.Contains("MainWindow.LaunchSettings.cs", partialNames);
        Assert.Contains("MainWindow.LaunchSettingsCapabilities.cs", partialNames);
        Assert.Contains("MainWindow.LaunchSettingsPanel.cs", partialNames);
        Assert.Contains("MainWindow.LaunchSettingsRuntimeSelection.cs", partialNames);
        Assert.Contains("MainWindow.ModelDownloads.cs", partialNames);
        Assert.Contains("MainWindow.ModelRows.cs", partialNames);
        Assert.Contains("MainWindow.DownloadHistory.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeBuilds.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeSourceDownloads.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeBuildJobs.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeJobControls.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeBuildGit.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeDashboard.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeMetrics.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeMetricCounters.cs", partialNames);
        Assert.Contains("MainWindow.ModelRuntime.cs", partialNames);
        Assert.Contains("MainWindow.ModelRuntimeLifecycle.cs", partialNames);
        Assert.Contains("MainWindow.ModelRuntimePrerequisites.cs", partialNames);
        Assert.Contains("MainWindow.RuntimeSession.cs", partialNames);
        Assert.Contains("MainWindow.OverviewSelection.cs", partialNames);
        Assert.Contains("MainWindow.UiHelpers.cs", partialNames);
        Assert.DoesNotContain("MainWindow.MetricInlines.cs", partialNames);
        Assert.Contains("MainWindow.GridHelpers.cs", partialNames);
        Assert.Contains("MainWindow.GridColumnSizing.cs", partialNames);
        Assert.Contains("MainWindow.Theme.cs", partialNames);
        Assert.Contains("MainWindow.UiState.cs", partialNames);
    }


    [Fact]
    public void OverviewLoadedSessionRowsSelectModelStatus()
    {
        var source = ReadMainWindowSources();
        var overviewFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "OverviewPageFactory.cs"));
        var loadedSessionSelection = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "OverviewLoadedSessionSelectionApplicationService.cs"));

        Assert.Contains("loadedSessionsGrid.SelectionChanged", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("request.Actions.SelectLoadedSessionRowAsync", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("SelectLoadedSessionRowAsync", source, StringComparison.Ordinal);
        Assert.Contains("_overviewPage.SelectedLoadedSessionRow", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.OverviewLoadedSessionSelectionApplication.SelectAsync", source, StringComparison.Ordinal);
        Assert.Contains("OverviewLoadedSessionSelectionActions()", source, StringComparison.Ordinal);
        Assert.Contains("_overviewPage.SelectModelId", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Runtime.RuntimeSessions.SelectModel", source, StringComparison.Ordinal);
        Assert.Contains("Selected session is no longer loaded.", loadedSessionSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("Selected session is no longer loaded.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Selected loaded model session.", source, StringComparison.Ordinal);
    }


    [Fact]
    public void SelectionReentrancyCoordinatorOwnsSelectionSuppression()
    {
        var coordinator = new SelectionReentrancyCoordinator();
        var source = ReadMainWindowSources();

        using (var modelSelection = coordinator.TryBeginModelGridSelection())
        {
            Assert.NotNull(modelSelection);
            Assert.True(coordinator.IsModelGridSelectionChanging);
            Assert.Null(coordinator.TryBeginModelGridSelection());
        }

        Assert.False(coordinator.IsModelGridSelectionChanging);

        using (var loadedSelection = coordinator.TryBeginLoadedSessionSelection())
        {
            Assert.NotNull(loadedSelection);
            Assert.True(coordinator.IsLoadedSessionSelectionChanging);
            using (coordinator.SuppressLoadedSessionSelection())
            {
                Assert.True(coordinator.IsLoadedSessionSelectionChanging);
                Assert.Null(coordinator.TryBeginLoadedSessionSelection());
            }

            Assert.True(coordinator.IsLoadedSessionSelectionChanging);
        }

        Assert.False(coordinator.IsLoadedSessionSelectionChanging);

        using (coordinator.SuppressLoadedSessionSelection())
        {
            Assert.True(coordinator.IsLoadedSessionSelectionChanging);
            Assert.Null(coordinator.TryBeginLoadedSessionSelection());
        }

        Assert.False(coordinator.IsLoadedSessionSelectionChanging);
        Assert.Contains("_coreServices.Ui.SelectionReentrancy.TryBeginModelGridSelection()", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.SelectionReentrancy.TryBeginLoadedSessionSelection()", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.SelectionReentrancy.SuppressLoadedSessionSelection()", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.SelectionReentrancy.IsLoadedSessionSelectionChanging", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_selectingModelGridRow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_selectingLoadedSessionRow", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MainWindowUsesObservedBackgroundTasks()
    {
        var source = ReadMainWindowSources();

        Assert.DoesNotContain("_ = Refresh", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = Monitor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = CheckFor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = Seed", source, StringComparison.Ordinal);
        Assert.Contains("RunBackground", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.BackgroundTasks.RunAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LogsPageDeleteRulesStayInWorkflowService()
    {
        var source = ReadMainWindowSources();
        var logsWindow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Logs.cs"));
        var logWorkflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LogPageWorkflowService.cs"));
        var logApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LogPageApplicationService.cs"));
        var appLogApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "AppLogApplicationService.cs"));
        var logsPageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "LogsPageState.cs"));

        Assert.Contains("var logPageApplication = AppServices.LogPageApplication;", source, StringComparison.Ordinal);
        Assert.Contains("logPageApplication!.BuildSelectedDeletionCommand(SelectedLogPaths(), _sessions.Snapshots())", source, StringComparison.Ordinal);
        Assert.Contains("logPageApplication!.BuildSingleDeletionCommand(path, _sessions.Snapshots())", source, StringComparison.Ordinal);
        Assert.Contains("logPageApplication!.BuildAllDeletionCommandAsync(_sessions.Snapshots())", source, StringComparison.Ordinal);
        Assert.Contains("await logPageApplication!.DeleteAsync(commandPlan, LogPageDeleteActions())", source, StringComparison.Ordinal);
        Assert.Contains("private readonly LogsPageState _logsPage;", source, StringComparison.Ordinal);
        Assert.Contains("_logsPage = uiState.LogsPage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly LogsPageState _logsPage = new();", source, StringComparison.Ordinal);
        Assert.Contains("_logsPage.Apply(page.Controls);", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class LogsPageState", logsPageState, StringComparison.Ordinal);
        Assert.Contains("public string[] SelectedLogPaths()", logsPageState, StringComparison.Ordinal);
        Assert.Contains("public void RestoreSelection", logsPageState, StringComparison.Ordinal);
        Assert.Contains("public sealed class LogPageApplicationService", logApplication, StringComparison.Ordinal);
        Assert.Contains("_workflow.BuildSelectedDeletionCommand(selectedPaths, sessions)", logApplication, StringComparison.Ordinal);
        Assert.Contains("_workflow.BuildSingleDeletionCommand(path, sessions)", logApplication, StringComparison.Ordinal);
        Assert.Contains("_workflow.BuildAllDeletionCommandAsync(sessions, cancellationToken)", logApplication, StringComparison.Ordinal);
        Assert.Contains("public LogPageOpenApplicationOutcome Open", logApplication, StringComparison.Ordinal);
        Assert.Contains("_workflow.TryValidateForOpen(path, out var error)", logApplication, StringComparison.Ordinal);
        Assert.Contains("public Task<string> BuildPreviewAsync", logApplication, StringComparison.Ordinal);
        Assert.Contains("_workflow.BuildPreviewAsync(new LogPreviewRequest(", logApplication, StringComparison.Ordinal);
        Assert.Contains("public async Task<LogPageDeleteApplicationOutcome> DeleteAsync", logApplication, StringComparison.Ordinal);
        Assert.Contains("!File.Exists(request.Path)", logWorkflow, StringComparison.Ordinal);
        Assert.Contains("BuildSelectedDeletionCommand", logWorkflow, StringComparison.Ordinal);
        Assert.Contains("BuildSingleDeletionCommand", logWorkflow, StringComparison.Ordinal);
        Assert.Contains("BuildAllDeletionCommandAsync", logWorkflow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.AppLogApplication.WriteExceptionAsync", source, StringComparison.Ordinal);
        Assert.Contains("BoundedLogFile.AppendAsync(path, text, maxLogBytes)", appLogApplication, StringComparison.Ordinal);
        Assert.Contains("LogFileService.RedactSensitiveText(text, apiKey)", appLogApplication, StringComparison.Ordinal);
        Assert.DoesNotContain("_logPageWorkflow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BoundedLogFile.AppendAsync(path, text, MaxLogBytes())", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LogFileService.RedactSensitiveText(text, _settings.ModelApiKey)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_logsGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_logsBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(\"Select one or more log files first.\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(\"No selected logs can be deleted.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(\"Stop the running model before deleting its active runtime log.\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryValidateLogFileForOpen", source, StringComparison.Ordinal);
        Assert.DoesNotContain("logPageApplication.TryValidateForOpen", source, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Exists", logsWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsivenessSensitiveFilesystemWorkLeavesDispatcher()
    {
        var mainWindowSource = ReadMainWindowSources();
        var modelCatalog = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelCatalogService.cs"));
        var runtimeRegistry = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeRegistryService.cs"));
        var runtimeCatalogData = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeCatalogDataService.cs"));
        var openCodeLocalModelWorkflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "OpenCodeLocalModelWorkflowService.cs"));
        var logPageWorkflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LogPageWorkflowService.cs"));
        var modelCapabilities = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelCapabilityCacheService.cs"));
        var debouncedAction = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "DebouncedAsyncAction.cs"));
        var runtimeDashboardRefresh = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDashboardRefreshCoordinator.cs"));
        var runtimeDashboardRefreshApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDashboardRefreshApplicationService.cs"));
        var runtimeGpuSummary = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeGpuSummaryApplicationService.cs"));
        var runtimeReadinessMonitorApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeReadinessMonitorApplicationService.cs"));
        var factory = ReadAppServiceFactorySources();

        Assert.Contains("FindModelFilesAsync", modelCatalog, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => FindModelFiles", modelCatalog, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => MergeGgufManifest", modelCatalog, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => Directory.Delete", modelCatalog, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => CandidateRuntimeFolders(runtimeRoot).Take(1000).ToArray())", runtimeRegistry, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => CreateRuntimeRecord", runtimeRegistry, StringComparison.Ordinal);
        Assert.Contains("runtimeCatalog.RefreshAsync(new RuntimeCatalogRefreshApplicationRequest(", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => Sources(runtimeRoot).ToList()", runtimeCatalogData, StringComparison.Ordinal);
        Assert.Contains("var logPageApplication = AppServices.LogPageApplication;", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("logPageApplication!.LoadAsync(_sessions.SelectedSnapshot())", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("Directory.EnumerateFiles(LogRoot", logPageWorkflow, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => LogFileService.Tail(request.Path, 80000)", logPageWorkflow, StringComparison.Ordinal);
        Assert.Contains("ScheduleSelectedModelLaunchSettingsRefresh", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.LaunchSettingsRefresh.Schedule", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("new(TimeSpan.FromMilliseconds(120))", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("new(TimeSpan.FromMilliseconds(120))", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("Task.Delay(_delay, cancellation.Token)", debouncedAction, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderSelectedModelLaunchSettingsDebouncedAsync", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_coreServices.Ui.LaunchSettingsRefreshCts", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelCapabilities.ReadAsync(model, cancellationToken)", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run(() => ModelCapabilityService.CacheKey(model)", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Run(() => ModelCapabilityService.Inspect(model)", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => _cacheKeyReader(model)", modelCapabilities, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => _inspector(model)", modelCapabilities, StringComparison.Ordinal);
        Assert.Contains("ResolveOpenCodeModelLimitsAsync", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("Already added to OpenCode automatically when the model was saved", openCodeLocalModelWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeJobLogPreviewMaxChars", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", runtimeReadinessMonitorApplication, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (OperationCanceledException)", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_cache.TryGet", runtimeGpuSummary, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.RuntimeGpuSummaryApplication.SummaryAsync", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_gpuSummaryCache.TryGet", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("_telemetry.TryBeginRefresh", runtimeDashboardRefreshApplication, StringComparison.Ordinal);
        Assert.Contains("RuntimeDashboardRefreshTarget", mainWindowSource, StringComparison.Ordinal);
        Assert.Contains("new RefreshLease(_refreshGate.Complete)", runtimeDashboardRefresh, StringComparison.Ordinal);
        Assert.DoesNotContain("_cachedGpuSummary", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_cachedGpuSummaryAt", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeDashboardRefreshGate", mainWindowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeDashboardRefreshInFlight", mainWindowSource, StringComparison.Ordinal);
    }


    [Fact]
    public void LightThemeUsesLayeredSurfacesAndElevation()
    {
        var appXaml = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "App.xaml"));
        var source = ReadMainWindowSources();
        var metricFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "MetricCardFactory.cs"));
        var overviewFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "OverviewPageFactory.cs"));

        Assert.Contains("<DropShadowEffect", appXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"MetricCard\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Content, RelativeSource={RelativeSource TemplatedParent}}\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"DropDownPickerButton\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("ControlTemplate TargetType=\"Button\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"29\"/>", appXaml, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"0\"/>", appXaml, StringComparison.Ordinal);
        Assert.Contains("Data=\"M 0 0 L 4 4 L 8 0 Z\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("<Style TargetType=\"ContextMenu\">", appXaml, StringComparison.Ordinal);
        Assert.Contains("<Style TargetType=\"MenuItem\">", appXaml, StringComparison.Ordinal);
        Assert.Contains("Property=\"HasDropShadow\" Value=\"False\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("Property=\"Background\" Value=\"{DynamicResource PanelBackAlt}\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("ControlTemplate TargetType=\"ContextMenu\"", appXaml, StringComparison.Ordinal);
        Assert.Contains("private static string TooltipText(string text) => text;", source, StringComparison.Ordinal);
        Assert.Contains("MetricImportantValuePattern", metricFactory, StringComparison.Ordinal);
        Assert.Contains("SplitMetricLine", metricFactory, StringComparison.Ordinal);
        Assert.Contains("MetricShouldEmphasizeWholeLine", metricFactory, StringComparison.Ordinal);
        Assert.Contains("IsNeutralMetricStatus", metricFactory, StringComparison.Ordinal);
        Assert.Contains("MetricShouldRenderNeutralStatus", metricFactory, StringComparison.Ordinal);
        Assert.Contains("TryAddStatusNameMetricLine", metricFactory, StringComparison.Ordinal);
        Assert.Contains("MetricStatusNameBlock", metricFactory, StringComparison.Ordinal);
        Assert.Contains("var valueRows = new Grid { MinHeight = 34, Tag = label }", metricFactory, StringComparison.Ordinal);
        Assert.Contains("header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto })", metricFactory, StringComparison.Ordinal);
        Assert.Contains("MetricCardFactory.SetMetricText(target, value, emphasizeLoadedStatus)", source, StringComparison.Ordinal);
        Assert.Contains("gpu = MetricCardFactory.AddMetric(runtimeDashboard, \"GPU\", 0, 1)", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("tokens = MetricCardFactory.AddMetric(runtimeDashboard, \"Tokens\", 1, 0, out tokensLastKnown)", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("mtpTokens = MetricCardFactory.AddMetric(runtimeDashboard, \"MTP tokens\", 1, 1)", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("slots = MetricCardFactory.AddMetric(runtimeDashboard, \"Slots\", 1, 2)", overviewFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Tokens (Live)\"", overviewFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Tokens (Total)\"", overviewFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Runtime build\", 0, 1", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("SetLastKnownMetricText(_runtimeDashboardPage.TokensLastKnown", source, StringComparison.Ordinal);
        Assert.Contains("ClearLastKnownMetricText(_runtimeDashboardPage.TokensLastKnown)", source, StringComparison.Ordinal);
        Assert.Contains("SetMetricText(_runtimeDashboardPage.TokensMetric, summary.Tokens)", source, StringComparison.Ordinal);
        Assert.Contains("SetMetricText(_runtimeDashboardPage.MtpTokensMetric, summary.MtpTokens)", source, StringComparison.Ordinal);
        Assert.Contains("SetMetricText(_runtimeDashboardPage.SlotsMetric, summary.Slots)", source, StringComparison.Ordinal);
        Assert.Contains("_sessions.SelectedSnapshot()?.LogPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeDashboardTotalTokensLastKnown", source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(label, \"Model status\", StringComparison.Ordinal)", metricFactory, StringComparison.Ordinal);
        Assert.Contains("\"Loaded:\"", metricFactory, StringComparison.Ordinal);
        Assert.Contains("\"Loading\"", metricFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("SetMetricText(_runtimeDashboardPage.RuntimeMetric", source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(normalized, \"None\", StringComparison.OrdinalIgnoreCase)", metricFactory, StringComparison.Ordinal);
        Assert.Contains("string.Equals(normalized, \"Stopped\", StringComparison.OrdinalIgnoreCase)", metricFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("text.StartsWith(\"Loading \", StringComparison.OrdinalIgnoreCase)", metricFactory, StringComparison.Ordinal);
        Assert.Contains("MetricValueFont", metricFactory, StringComparison.Ordinal);
        Assert.Contains("Typography.SetNumeralAlignment(valueRun, FontNumeralAlignment.Tabular)", metricFactory, StringComparison.Ordinal);
        Assert.Contains("(\"AppBack\", \"#E5ECF3\")", source, StringComparison.Ordinal);
        Assert.Contains("(\"PanelBack\", \"#FFFFFF\")", source, StringComparison.Ordinal);
        Assert.Contains("(\"PanelBorder\", \"#B7C4D2\")", source, StringComparison.Ordinal);
        Assert.Contains("(\"PanelBorderStrong\", \"#8799AC\")", source, StringComparison.Ordinal);
        Assert.Contains("(\"GridRowAlt\", \"#EDF4FA\")", source, StringComparison.Ordinal);
        Assert.Contains("(\"Accent\", \"#126F5B\")", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MetricCardFactoryKeepsMetricParsingRulesOutOfMainWindow()
    {
        var uiHelpers = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.UiHelpers.cs"));

        Assert.Equal(("Gen", "12.3 t/s"), MetricCardFactory.SplitMetricLine("Gen 12.3 t/s"));
        Assert.Equal(("Context", "32,768"), MetricCardFactory.SplitMetricLine("Context 32,768"));
        Assert.Equal(("Port", "8081"), MetricCardFactory.SplitMetricLine("Port: 8081"));
        Assert.True(MetricCardFactory.IsNeutralMetricStatus("No loaded runtime"));
        Assert.True(MetricCardFactory.IsNeutralMetricStatus("Failed to load"));
        Assert.False(MetricCardFactory.IsNeutralMetricStatus("Qwen3 30B"));
        Assert.DoesNotContain("private static readonly Regex MetricImportantValuePattern", uiHelpers, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool MetricShouldRenderNeutralStatus", uiHelpers, StringComparison.Ordinal);
        Assert.Contains("MetricCardFactory.AddMetric", uiHelpers, StringComparison.Ordinal);
        Assert.Contains("MetricCardFactory.SetMetricText", uiHelpers, StringComparison.Ordinal);
    }


    [Fact]
    public void OverviewPageFactoryKeepsOverviewLayoutOutOfMainWindow()
    {
        var source = ReadMainWindowSources();
        var overviewFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "OverviewPageFactory.cs"));
        var overviewPageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "OverviewPageState.cs"));
        var pageSectionFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "PageSectionFactory.cs"));
        var runtimeDashboardState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "RuntimeDashboardPageState.cs"));

        Assert.Contains("OverviewPageFactory.Create", source, StringComparison.Ordinal);
        Assert.Contains("private readonly OverviewPageState _overviewPage;", source, StringComparison.Ordinal);
        Assert.Contains("_overviewPage = uiState.OverviewPage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly OverviewPageState _overviewPage = new();", source, StringComparison.Ordinal);
        Assert.Contains("_overviewPage.Apply(overview);", source, StringComparison.Ordinal);
        Assert.Contains("_runtimeDashboardPage.Apply(overview);", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record OverviewPageActions", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("public sealed record OverviewPageControls", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("public sealed class OverviewPageState", overviewPageState, StringComparison.Ordinal);
        Assert.Contains("public ModelRecord? SelectedModel", overviewPageState, StringComparison.Ordinal);
        Assert.Contains("public UiRow? SelectedLoadedSessionRow", overviewPageState, StringComparison.Ordinal);
        Assert.Contains("public void RestoreLoadedSessionSelection", overviewPageState, StringComparison.Ordinal);
        Assert.Contains("public sealed class RuntimeDashboardPageState", runtimeDashboardState, StringComparison.Ordinal);
        Assert.Contains("public Grid? ModelMetric", runtimeDashboardState, StringComparison.Ordinal);
        Assert.Contains("public DataGrid? RuntimeMetricsGrid", runtimeDashboardState, StringComparison.Ordinal);
        Assert.Contains("public WpfTextBox? RuntimeLogBox", runtimeDashboardState, StringComparison.Ordinal);
        Assert.Contains("LoadedSessionColumns", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("RuntimeMetricColumns", overviewFactory, StringComparison.Ordinal);
        Assert.Equal(("Model", "C1", 1.45), OverviewPageFactory.LoadedSessionColumns[0]);
        Assert.Equal(("Help", "C5", 3), OverviewPageFactory.RuntimeMetricColumns[^1]);
        Assert.Contains("PageSectionFactory.GridSection(LoadedSessionsTitle", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.FramedSection(LiveRuntimeLogTitle", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.HorizontalGridSplitter(3)", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("public static Grid FramedSection", pageSectionFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("var modelBar = new Grid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GridSection(\"Loaded Model Sessions\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeDashboardModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeDashboardGenerationRateLastKnown", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeDashboardTokensLastKnown", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeMetricsGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_overviewRuntimeLogBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_overviewModelCombo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_overviewLoadButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_overviewUnloadButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_loadedSessionsGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_gatewayStatusText", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MainWindowKeepsPolishedActionPlacementAndOverviewDiagnostics()
    {
        var source = ReadMainWindowSources();
        var overviewFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "OverviewPageFactory.cs"));
        var modelsFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "ModelsPageFactory.cs"));
        var modelsRowActions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "ModelsPageRowActionController.cs"));
        var settingsFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "SettingsPageFactory.cs"));
        var huggingFaceGridModeFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "HuggingFaceGridModeFactory.cs"));
        var overviewViewModel = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "ViewModels", "OverviewPageViewModel.cs"));
        var modelRuntimeCommands = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelRuntimeCommandDecisionService.cs"));
        var runtimeOverviewStatus = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeOverviewStatusService.cs"));
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var normalizedOverviewFactory = overviewFactory.Replace("\r\n", "\n", StringComparison.Ordinal);
        var normalizedModelsFactory = modelsFactory.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("FolderStripActionsFirst(\n            \"Models folder\"", normalizedModelsFactory, StringComparison.Ordinal);
        Assert.Contains("ScanModelsFolderAsync", modelsFactory, StringComparison.Ordinal);
        Assert.Contains("Scanning models...", source, StringComparison.Ordinal);
        Assert.Contains("Button(\"Save Settings\"", settingsFactory, StringComparison.Ordinal);
        Assert.Contains("SettingsPageFactory.Create(new SettingsPageRequest(", source, StringComparison.Ordinal);
        Assert.Contains("Select the loading or loaded model to unload it.", modelRuntimeCommands, StringComparison.Ordinal);
        Assert.Contains("Choose the loading or loaded model to unload it.", modelRuntimeCommands, StringComparison.Ordinal);
        Assert.Contains("Stop the currently loading or loaded model", source, StringComparison.Ordinal);
        Assert.Contains("OpenHuggingFaceModelCardRow_Click", source, StringComparison.Ordinal);
        Assert.Contains("_modelCards.OpenFromRow", modelsRowActions, StringComparison.Ordinal);
        Assert.Contains("_modelFolders.Open", modelsRowActions, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.AddButtonColumn(request.Grid, \"Card\", \"C8\", \"B2\", request.Actions.OpenModelCardRow", huggingFaceGridModeFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("Button(\"Model Card\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenSelectedHuggingFaceModelCard", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HuggingFaceService.TryCreateModelCardUrl(repo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Opened Hugging Face model card", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Model folder is unavailable.", source, StringComparison.Ordinal);
        Assert.Contains("(\"Signals\", \"C6\", 1.4)", huggingFaceGridModeFactory, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.FramedSection(LiveRuntimeLogTitle", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("public const string RuntimeMetricsTitle = \"All llama.cpp Metrics\"", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("model = MetricCardFactory.AddMetric(runtimeDashboard, \"Model status\", 0, 0)", overviewFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("gatewayStatusText", overviewFactory, StringComparison.OrdinalIgnoreCase);
        var gatewayRuntimeApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "GatewayRuntimeApplicationService.cs"));

        Assert.Contains("actions.StartActivity(request.Model, \"switching to\")", gatewayRuntimeApplication, StringComparison.Ordinal);
        Assert.Contains("Gateway auto-loading", gatewayRuntimeApplication, StringComparison.Ordinal);
        Assert.Contains("Gateway loaded", gatewayRuntimeApplication, StringComparison.Ordinal);
        Assert.Contains("UpdateGatewayStatusText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_coreServices.Ui.GatewayActivity.Build(_settings, _gateway is not null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_coreServices.Ui.GatewayActivityModelName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_lastGatewayError", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Gateway auto-loading", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Gateway loaded {model.Name}", source, StringComparison.Ordinal);
        Assert.Contains("_gatewayServices = services.Gateway", source, StringComparison.Ordinal);
        Assert.Contains("GatewayServices.GatewayRuntimeApplication.EnsureModelLoadedAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_modelWorkflowServices", source, StringComparison.Ordinal);
        Assert.Contains("_workflow.EnsureLoadedAsync", gatewayRuntimeApplication, StringComparison.Ordinal);
        Assert.True(normalizedOverviewFactory.IndexOf("PageSectionFactory.GridSection(LoadedSessionsTitle", StringComparison.Ordinal) < normalizedOverviewFactory.IndexOf("Text(\"Model Status\"", StringComparison.Ordinal));
        Assert.Contains("(\"Size\", \"C2\"", overviewFactory, StringComparison.Ordinal);
        Assert.Contains("SessionStatusLabel", overviewViewModel, StringComparison.Ordinal);
        Assert.Contains("request.Session.RuntimeName", runtimeOverviewStatus, StringComparison.Ordinal);
        Assert.Contains("Unknown runtime", runtimeOverviewStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("active.RuntimeName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("includeProgress: true", source, StringComparison.Ordinal);
        Assert.Contains("root.Children.Add(PageSectionFactory.HorizontalGridSplitter(2))", modelsFactory, StringComparison.Ordinal);
        Assert.Contains("BorderThickness = new Thickness(0)", overviewFactory, StringComparison.Ordinal);
    }


    [Fact]
    public void ModelsGridUsesPerRowActionsOnly()
    {
        var source = ReadMainWindowSources();
        var modelsFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "ModelsPageFactory.cs"));
        var modelsPageActions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "ModelsPageActionController.cs"));
        var modelsPageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "ModelsPageState.cs"));
        var launchPanelFactory = ReadLaunchSettingsPanelFactorySources();

        Assert.Contains("nameof(ModelGridRow.Name)", modelsFactory, StringComparison.Ordinal);
        Assert.Contains("nameof(ModelGridRow.Size)", modelsFactory, StringComparison.Ordinal);
        Assert.Contains("Saved Model Variants", modelsFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("RowDetailsVisibilityMode", modelsFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved launch variant. Same GGUF file", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "ViewModels", "ModelsPageViewModel.cs")), StringComparison.Ordinal);
        Assert.Contains("private readonly ModelsPageState _modelsPage;", source, StringComparison.Ordinal);
        Assert.Contains("_modelsPage = uiState.ModelsPage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly ModelsPageState _modelsPage = new();", source, StringComparison.Ordinal);
        Assert.Contains("_modelsPage.Apply(modelsPage);", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class ModelsPageState", modelsPageState, StringComparison.Ordinal);
        Assert.Contains("public ModelRecord? SelectedModel", modelsPageState, StringComparison.Ordinal);
        Assert.Contains("public void SelectModelAfterRefresh", modelsPageState, StringComparison.Ordinal);
        Assert.Contains("Save As New", launchPanelFactory, StringComparison.Ordinal);
        Assert.Contains("SaveLaunchSettingsAsNewModelAsync", source, StringComparison.Ordinal);
        Assert.Contains("nameof(ModelGridRow.OpenFolderAction)", modelsFactory, StringComparison.Ordinal);
        Assert.Contains("nameof(ModelGridRow.CanDelete)", modelsFactory, StringComparison.Ordinal);
        Assert.Contains("OpenModelFolderRow_Click", modelsPageActions, StringComparison.Ordinal);
        Assert.Contains("DeleteModelRow_Click", modelsPageActions, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelDeletionApplication.DeleteAsync", source, StringComparison.Ordinal);
        Assert.Contains("ModelDeletionActions()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ModelAliasService.IsLaunchAlias(model)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("delete the downloaded model files", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_deleteModelButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteSelectedModelAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_loadModelButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_restartModelButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_unloadModelButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateModelActionButtons", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_modelsGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_modelVariantsGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_modelsFolderText", source, StringComparison.Ordinal);
    }


    [Fact]
    public void FolderSettingsWorkflowStaysOutOfMainWindow()
    {
        var folderSettings = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.FolderSettings.cs"));
        var uiHelpers = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.UiHelpers.cs"));
        var application = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "FolderSettingsApplicationService.cs"));
        var dialogs = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "FileSystemDialogService.cs"));

        Assert.Contains("_coreServices.App.FolderSettingsApplication.ChooseModelsFolderAsync", folderSettings, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.FolderSettingsApplication.ChooseRuntimeFolderAsync", folderSettings, StringComparison.Ordinal);
        Assert.Contains("FolderSettingsActions()", folderSettings, StringComparison.Ordinal);
        Assert.Contains("=> _coreServices.App.FileSystemDialogs.PickFolder(initial)", uiHelpers, StringComparison.Ordinal);
        Assert.Contains("Forms.FolderBrowserDialog", dialogs, StringComparison.Ordinal);
        Assert.Contains("Models folder set to", application, StringComparison.Ordinal);
        Assert.Contains("Runtimes folder set to", application, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetFullPath(folder)", folderSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("FolderBrowserDialog", uiHelpers, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Exists(initial)", uiHelpers, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Changing models folder...\"", folderSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Changing runtimes folder...\"", folderSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Models folder set to", folderSettings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Runtimes folder set to", folderSettings, StringComparison.Ordinal);
    }


    [Fact]
    public void ToolSetupCommandPolicyStaysOutOfMainWindow()
    {
        var windows = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Windows.cs"));
        var wsl = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.WslActions.cs"));
        var application = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ToolSetupApplicationService.cs"));

        Assert.Contains("_coreServices.Environment.WindowsToolSetupApplication.Run", windows, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Environment.WslToolSetupApplication.Run", wsl, StringComparison.Ordinal);
        Assert.Contains("Install or select an Ubuntu distro first.", application, StringComparison.Ordinal);
        Assert.DoesNotContain("_windowsToolSetupWorkflow.Plan", windows, StringComparison.Ordinal);
        Assert.DoesNotContain("_windowsToolSetupWorkflow.Execute", windows, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslToolSetupWorkflow.RequiresUbuntuDistro", wsl, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslToolSetupWorkflow.Plan", wsl, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslToolSetupWorkflow.Execute", wsl, StringComparison.Ordinal);
        Assert.DoesNotContain("Install or select an Ubuntu distro first.", wsl, StringComparison.Ordinal);
    }


    [Fact]
    public void LifetimeMetricResetPolicyStaysOutOfMainWindow()
    {
        var lifetime = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RefreshAndLifetime.cs"));
        var application = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LifetimeMetricResetApplicationService.cs"));
        var metricsApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LifetimeMetricsApplicationService.cs"));

        Assert.Contains("_coreServices.App.LifetimeMetricResetApplication.ResetAsync", lifetime, StringComparison.Ordinal);
        Assert.Contains("LifetimeMetricResetActions()", lifetime, StringComparison.Ordinal);
        Assert.Contains("AppServices.LifetimeMetricsApplication", lifetime, StringComparison.Ordinal);
        Assert.Contains("lifetimeMetrics.ListAsync()", lifetime, StringComparison.Ordinal);
        Assert.Contains("lifetimeMetrics.DeleteModelUsageAsync(modelId)", lifetime, StringComparison.Ordinal);
        Assert.Contains("lifetimeMetrics.DeleteAllUsageAsync()", lifetime, StringComparison.Ordinal);
        Assert.Contains("Reset lifetime token metrics for all models?", application, StringComparison.Ordinal);
        Assert.Contains("Only model rows can be reset individually.", application, StringComparison.Ordinal);
        Assert.Contains("_stateStore.AddTokenUsageAsync(delta.ModelId", metricsApplication, StringComparison.Ordinal);
        Assert.Contains("_stateStore.ListTokenUsageAsync()", metricsApplication, StringComparison.Ordinal);
        Assert.Contains("_stateStore.DeleteTokenUsageAsync(modelId)", metricsApplication, StringComparison.Ordinal);
        Assert.Contains("_stateStore.DeleteAllTokenUsageAsync()", metricsApplication, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.AddTokenUsageAsync", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.ListTokenUsageAsync()", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.DeleteTokenUsageAsync", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.DeleteAllTokenUsageAsync", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("row.Data[\"Kind\"]", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("Reset lifetime token metrics for all models?", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("Only model rows can be reset individually.", lifetime, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowUsesLoadedLookupServicesForCatalogReads()
    {
        var selection = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.SelectionAndCleanup.cs"));
        var overview = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.OverviewSelection.cs"));
        var gateway = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Gateway.cs"));
        var openCode = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.OpenCodeActions.cs"));
        var launchRuntime = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.LaunchSettingsRuntimeSelection.cs"));
        var modelRuntime = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.ModelRuntime.cs"));
        var runtimeDashboard = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeDashboard.cs"));
        var runtimeSession = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeSession.cs"));
        var modelLookup = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelLookupApplicationService.cs"));
        var source = string.Join(Environment.NewLine, selection, overview, gateway, openCode, launchRuntime, modelRuntime, runtimeDashboard, runtimeSession);

        Assert.Contains("AppServices.ModelLookupApplication", source, StringComparison.Ordinal);
        Assert.Contains("AppServices.StateStore", source, StringComparison.Ordinal);
        Assert.Contains("modelLookup.FindByIdAsync(modelId)", selection, StringComparison.Ordinal);
        Assert.Contains("modelLookup.BuildHuggingFaceInstallInventoryAsync()", selection, StringComparison.Ordinal);
        Assert.Contains("modelLookup.ListAsync()", overview, StringComparison.Ordinal);
        Assert.Contains("modelLookup.DisplayNameAsync(modelId)", overview, StringComparison.Ordinal);
        Assert.Contains("modelLookup.ListAsync()", gateway, StringComparison.Ordinal);
        Assert.Contains("AppServices.ModelLookupApplication.ListAsync()", openCode, StringComparison.Ordinal);
        Assert.Contains("AppServices.StateStore.ListRuntimesAsync()", launchRuntime, StringComparison.Ordinal);
        Assert.Contains("AppServices.StateStore.ListRuntimesAsync()", modelRuntime, StringComparison.Ordinal);
        Assert.Contains("modelLookup.ListAsync", runtimeSession, StringComparison.Ordinal);
        Assert.Contains("appServices.StateStore.ListRuntimesAsync", runtimeSession, StringComparison.Ordinal);
        Assert.Contains("AppServices.StateStore.ListJobsAsync()", runtimeDashboard, StringComparison.Ordinal);
        Assert.Contains("_stateStore.ListModelsAsync()", modelLookup, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.ListModelsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.ListRuntimesAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_stateStore.ListJobsAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ModelCatalogRefreshCompositionStaysOutOfMainWindow()
    {
        var lifetime = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RefreshAndLifetime.cs"));
        var application = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelCatalogRefreshApplicationService.cs"));

        Assert.Contains("ModelServices.ModelCatalogRefreshApplication", lifetime, StringComparison.Ordinal);
        Assert.Contains("modelRefresh.RefreshAsync(ModelCatalogRefreshActions())", lifetime, StringComparison.Ordinal);
        Assert.Contains("result.LaunchProfileFor", lifetime, StringComparison.Ordinal);
        Assert.Contains("_catalog.CleanupModelRecordsAsync()", application, StringComparison.Ordinal);
        Assert.Contains("_stateStore.ListModelsAsync()", application, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupModelRecordsAsync", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("ListModelsAsync()", lifetime, StringComparison.Ordinal);
        Assert.DoesNotContain("new Dictionary<string, ModelLaunchSettings>", lifetime, StringComparison.Ordinal);
    }


    [Fact]
    public void HuggingFaceSearchKeepsDownloadActionVisibleAndSwitchesToHistory()
    {
        var source = ReadMainWindowSources();
        var downloadHistorySource = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.DownloadHistory.cs"));
        var downloadHistoryWorkflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "DownloadHistoryWorkflowService.cs"));
        var downloadHistoryApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "DownloadHistoryApplicationService.cs"));
        var searchApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "HuggingFaceSearchApplicationService.cs"));
        var downloadApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "HuggingFaceDownloadApplicationService.cs"));
        var gridModeFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "HuggingFaceGridModeFactory.cs"));

        Assert.Contains("_coreServices.HuggingFaceServices.HuggingFaceSearchApplication.SearchAsync", source, StringComparison.Ordinal);
        Assert.Contains("HuggingFaceSearchActions(", source, StringComparison.Ordinal);
        Assert.Contains("actions.ConfigureSearchGrid()", searchApplication, StringComparison.Ordinal);
        Assert.Contains("actions.ApplySearchResults(results, installed, settings.ModelsRoot)", searchApplication, StringComparison.Ordinal);
        Assert.Contains("_coreServices.HuggingFaceServices.HuggingFaceDownloadApplication.StartAsync", source, StringComparison.Ordinal);
        Assert.Contains("HuggingFaceDownloadActions(", source, StringComparison.Ordinal);
        Assert.Contains("await actions.ShowDownloadHistoryAsync(job.Id)", downloadApplication, StringComparison.Ordinal);
        Assert.Contains("actions.StartMonitor(job.Id)", downloadApplication, StringComparison.Ordinal);
        Assert.Contains("Download started: {file.Name} ({job.Id})", downloadApplication, StringComparison.Ordinal);
        Assert.Contains("SelectDownloadHistoryJob", source, StringComparison.Ordinal);
        Assert.Contains("_modelsPage.UseHuggingFaceSearchGrid()", source, StringComparison.Ordinal);
        Assert.Contains("_modelsPage.UseDownloadHistoryGrid()", source, StringComparison.Ordinal);
        Assert.Contains("HuggingFaceGridModeFactory.ConfigureSearch(HuggingFaceGridModeRequest(grid))", source, StringComparison.Ordinal);
        Assert.Contains("HuggingFaceGridModeFactory.ConfigureDownloadHistory(HuggingFaceGridModeRequest(grid))", source, StringComparison.Ordinal);
        Assert.Contains("_downloadHistoryPageState.ShowSearch()", source, StringComparison.Ordinal);
        Assert.Contains("_downloadHistoryPageState.ShowHistory()", source, StringComparison.Ordinal);
        Assert.Contains("_downloadHistoryPageState.TryBeginTimerRefresh", source, StringComparison.Ordinal);
        Assert.Contains("_downloadHistoryPageState.CompleteTimerRefresh", source, StringComparison.Ordinal);
        Assert.Contains("public async Task<DownloadHistoryApplicationOutcome> ShowAsync", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("public async Task<DownloadHistoryTimerRefreshOutcome> RefreshTimerAsync", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("actions.ConfigureHistoryGrid()", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("actions.TryBeginRefresh()", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("actions.CompleteRefresh()", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.DoesNotContain("_downloadHistoryPageState.IsShowingHistory", downloadHistorySource, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.DownloadHistoryRefreshTimer.Start(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.DownloadHistoryRefreshTimer.Stop()", source, StringComparison.Ordinal);
        Assert.Contains("DownloadHistoryTimerRefreshAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.DownloadCompletionApplication.MonitorAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new DownloadCompletionApplicationActions(", source, StringComparison.Ordinal);
        Assert.Contains("RunDownloadCompletionOnUiThreadAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshCompletedDownloadAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hfShowingDownloadHistory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_downloadHistoryRefreshInFlight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_downloadHistoryTimer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DownloadHistoryTimer_Tick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_viewModel.HuggingFace.ReplaceSearchResults(await huggingFace", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"Download started:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hfQueryBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hfGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_downloadHistoryGrid", source, StringComparison.Ordinal);
        Assert.Contains("grid.Columns[1].Width = new DataGridLength(1.85, DataGridLengthUnitType.Star)", source, StringComparison.Ordinal);
        Assert.Contains("grid.Columns[5].Width = new DataGridLength(1.05, DataGridLengthUnitType.Star)", source, StringComparison.Ordinal);
        Assert.Contains("grid.Columns[6].MinWidth = 96", source, StringComparison.Ordinal);
        Assert.Contains("grid.Columns[6].Width = new DataGridLength(104)", source, StringComparison.Ordinal);
        Assert.Contains("grid.Columns[7].Width = new DataGridLength(74)", source, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.AddButtonColumn(request.Grid, \"Actions\", \"C7\", \"B1\", request.Actions.DownloadSearchRow", gridModeFactory, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.AddButtonColumn(request.Grid, \"Delete\", \"C10\", \"B4\", request.Actions.DeleteDownloadRow", gridModeFactory, StringComparison.Ordinal);
        Assert.Contains("var downloadHistory = AppServices.DownloadHistoryApplication;", source, StringComparison.Ordinal);
        Assert.Contains("await downloadHistory!.DeleteAsync(job, _settings, DownloadHistoryDeleteActions())", source, StringComparison.Ordinal);
        Assert.Contains("await downloadHistory!.ResumeAsync(job, _settings, DownloadHistoryCommandActions())", source, StringComparison.Ordinal);
        Assert.Contains("await downloadHistory!.PauseAsync(job, DownloadHistoryCommandActions())", source, StringComparison.Ordinal);
        Assert.Contains("await downloadHistory!.StopAsync(job, DownloadHistoryCommandActions())", source, StringComparison.Ordinal);
        Assert.Contains("await downloadHistory!.ShowAsync(", source, StringComparison.Ordinal);
        Assert.Contains("await downloadHistory.RefreshTimerAsync(", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class DownloadHistoryApplicationService", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("var deletePlan = _workflow.BuildDeletePlan(job)", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("await _workflow.ResumeAsync(job, settings)", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("await _workflow.PauseAsync(job)", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.Contains("await _workflow.StopAsync(job)", downloadHistoryApplication, StringComparison.Ordinal);
        Assert.DoesNotContain("_downloadHistoryWorkflow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AppServices.HuggingFace!.ResumeDownloadAsync(job, _settings)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AppServices.HuggingFace!.PauseDownloadAsync(job)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AppServices.HuggingFace!.StopDownloadAsync(job)", source, StringComparison.Ordinal);
        Assert.Contains("DeletePartialFile", downloadHistoryWorkflow, StringComparison.Ordinal);
        Assert.Contains("Completed model files are kept.", downloadHistoryWorkflow, StringComparison.Ordinal);
        Assert.Contains("if (grid.Columns.Count < 10) return;", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MainWindowExposesAppUpdatesAndCacheClearing()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.xaml"));
        var source = ReadMainWindowSources();
        var project = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "LocalLlmConsole.App.csproj"));
        var themedMessageBox = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "ThemedMessageBox.cs"));
        var settingsDefinitions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "SettingsPageDefinitionService.cs"));
        var settingsPageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "SettingsPageState.cs"));
        var updatesPageFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "UpdatesPageFactory.cs"));

        Assert.Contains("x:Name=\"UpdatesNavButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HelpNavButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"WindowsNavButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ToolsNavLabel\"", xaml, StringComparison.Ordinal);
        Assert.True(xaml.IndexOf("x:Name=\"AppStatusText\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"UpdatesNavButton\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"LogsNavButton\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"ToolsNavLabel\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"ToolsNavLabel\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"WindowsNavButton\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"WindowsNavButton\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"WslLinuxNavButton\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"LogsNavButton\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"UpdatesNavButton\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"UpdatesNavButton\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"HelpNavButton\"", StringComparison.Ordinal));
        Assert.Contains("CheckForAppUpdatesOnStartupAsync", source, StringComparison.Ordinal);
        Assert.Contains("InstallAppUpdateAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.SettingsPageDefinitions.BuildRows(_settings)", source, StringComparison.Ordinal);
        Assert.Contains("private readonly SettingsPageState _settingsPage;", source, StringComparison.Ordinal);
        Assert.Contains("_settingsPage = uiState.SettingsPage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly SettingsPageState _settingsPage = new();", source, StringComparison.Ordinal);
        Assert.Contains("_settingsPage.Apply(page);", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class SettingsPageState", settingsPageState, StringComparison.Ordinal);
        Assert.Contains("public string SelectedThemeValue", settingsPageState, StringComparison.Ordinal);
        Assert.DoesNotContain("_themeCombo", source, StringComparison.Ordinal);
        Assert.Contains("CacheMaintenanceService.Size(settings.CacheRoot)", settingsDefinitions, StringComparison.Ordinal);
        Assert.Contains("ClearCacheAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.CacheClearApplication.ClearAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CacheClearPlanStatus.", source, StringComparison.Ordinal);
        Assert.Contains("<RepositoryUrl>https://github.com/alekk89/llama-cpp-windows-manager</RepositoryUrl>", project, StringComparison.Ordinal);

        Assert.Contains("UpdatesPageFactory.Create(new UpdatesPageRequest(", source, StringComparison.Ordinal);
        Assert.True(
            updatesPageFactory.IndexOf("actions.Children.Add(Button(request.ViewModel.ActionText", StringComparison.Ordinal)
            < updatesPageFactory.IndexOf("PageSectionFactory.FramedSection(\"Update Status\"", StringComparison.Ordinal));
        Assert.DoesNotContain("FramedSection(\"Update Status\"", source, StringComparison.Ordinal);
        Assert.Contains("MaxHeight = DialogMaxHeight(owner)", themedMessageBox, StringComparison.Ordinal);
        Assert.Contains("DialogMessageMaxHeight", themedMessageBox, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", themedMessageBox, StringComparison.Ordinal);
    }


    [Fact]
    public void MainWindowDialogCallsGoThroughDialogService()
    {
        var source = ReadMainWindowSources();
        var dialogs = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "DialogService.cs"));
        var factory = ReadAppServiceFactorySources();

        Assert.Contains("public sealed class DialogService", dialogs, StringComparison.Ordinal);
        Assert.Contains("ThemedMessageBox.Show", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemedMessageBox.Show", dialogs, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.Dialogs.Confirm", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.Dialogs.Notify", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemedMessageBox.Show", source, StringComparison.Ordinal);
    }


    [Fact]
    public void AppStartupSingleInstanceNoticeUsesServices()
    {
        var app = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "App.xaml.cs"));
        var singleInstance = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "SingleInstanceApplicationService.cs"));

        Assert.Contains("private readonly SingleInstanceApplicationService _singleInstance = new(SingleInstanceApplicationService.AcquireMutexLease);", app, StringComparison.Ordinal);
        Assert.Contains("private readonly DialogService _dialogs = new(ThemedMessageBox.Show);", app, StringComparison.Ordinal);
        Assert.Contains("_singleInstance.TryAcquire(SingleInstanceMutexName)", app, StringComparison.Ordinal);
        Assert.Contains("_dialogs.Notify(null, \"llama.cpp Windows Manager is already running.\"", app, StringComparison.Ordinal);
        Assert.Contains("_singleInstance.Dispose();", app, StringComparison.Ordinal);
        Assert.DoesNotContain("new Mutex(", app, StringComparison.Ordinal);
        Assert.Contains("public sealed class SingleInstanceApplicationService", singleInstance, StringComparison.Ordinal);
        Assert.Contains("AcquireMutexLease", singleInstance, StringComparison.Ordinal);
    }


    [Fact]
    public void SettingsThemePreviewDoesNotRebuildSettingsPage()
    {
        var settings = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Settings.cs"));
        var handlerStart = settings.IndexOf("private void PreviewSettingsTheme()", StringComparison.Ordinal);
        var handlerEnd = settings.IndexOf("private async Task RunSettingsRowActionAsync", handlerStart, StringComparison.Ordinal);

        Assert.True(handlerStart >= 0);
        Assert.True(handlerEnd > handlerStart);
        var handler = settings[handlerStart..handlerEnd];
        Assert.Contains("AppPreferenceService.ThemeMode(_settingsPage.SelectedThemeValue)", handler, StringComparison.Ordinal);
        Assert.Contains("ApplyTheme(mode);", handler, StringComparison.Ordinal);
        Assert.Contains("Theme preview applied. Save settings to keep it.", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("_themeCombo", handler, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowSettings()", handler, StringComparison.Ordinal);
    }


    [Fact]
    public void SettingsApiKeyCanBeShownAndCopied()
    {
        var settings = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Settings.cs"));
        var settingsActions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "SettingsPageActionController.cs"));
        var settingsFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "SettingsPageFactory.cs"));
        var settingsGridColumns = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "SettingsGridColumnFactory.cs"));
        var settingsDefinitions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "SettingsPageDefinitionService.cs"));
        var settingsRowActions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "SettingsRowActionApplicationService.cs"));
        var clipboard = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ClipboardService.cs"));
        var factory = ReadAppServiceFactorySources();
        var rows = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Models", "UiRows.cs"));

        Assert.Contains("new(\"Network\", \"API key\", \"modelApiKey\", settings.ModelApiKey, \"secret\", Action: \"Generate\",", settingsDefinitions, StringComparison.Ordinal);
        Assert.Contains("Bearer key required by local OpenAI-compatible endpoints", settingsDefinitions, StringComparison.Ordinal);
        Assert.Contains("OpenCode sync copies this key into OpenCode provider config in plain text", settingsDefinitions, StringComparison.Ordinal);
        Assert.Contains("SettingsGridColumnFactory.ActionsColumn", settingsFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("FrameworkElementFactory", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Header = \"Secret\"", settings, StringComparison.Ordinal);
        Assert.Contains("RevealSecretRow_Click", settingsActions, StringComparison.Ordinal);
        Assert.Contains("CopySecretRow_Click", settingsActions, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.SettingsRowActions.RunActionAsync", settings, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.SettingsRowActions.ToggleSecret", settings, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.SettingsRowActions.CopySecret", settings, StringComparison.Ordinal);
        Assert.Contains("new(_coreServices.App.Clipboard.SetText, SetStatus)", settings, StringComparison.Ordinal);
        Assert.Contains("System.Windows.Clipboard.SetText", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Clipboard.SetText", clipboard, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiSecurity.GenerateHexToken(32)", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("row.Type != \"folder\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Windows.Clipboard.SetText", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Clipboard.SetText(value)", settings, StringComparison.Ordinal);
        Assert.Contains("nameof(EditableSettingRow.RevealAction)", settingsGridColumns, StringComparison.Ordinal);
        Assert.Contains("nameof(EditableSettingRow.CopyAction)", settingsGridColumns, StringComparison.Ordinal);
        Assert.Contains("nameof(EditableSettingRow.Action)", settingsGridColumns, StringComparison.Ordinal);
        Assert.Contains("public static DataGridTemplateColumn ValueColumn()", settingsGridColumns, StringComparison.Ordinal);
        Assert.Contains("API key copied to clipboard.", settingsRowActions, StringComparison.Ordinal);
        Assert.DoesNotContain("API key copied to clipboard.", settings, StringComparison.Ordinal);
        Assert.Contains("IsSecretVisible", rows, StringComparison.Ordinal);
        Assert.Contains("RevealAction", rows, StringComparison.Ordinal);
        Assert.Contains("CopyAction", rows, StringComparison.Ordinal);
        Assert.Contains("Type == \"secret\" ? IsSecretVisible", rows, StringComparison.Ordinal);
    }


    [Fact]
    public void SettingsNumericEditsFailClosed()
    {
        var persistence = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.SettingsPersistence.cs"));
        var settingsApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "AppSettingsApplicationService.cs"));
        var settingsUpdates = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "AppSettingsUpdateService.cs"));
        var settingsWorkflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "AppSettingsWorkflowService.cs"));
        var preferences = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "AppPreferenceService.cs"));

        Assert.Contains("TryIntValue", preferences, StringComparison.Ordinal);
        Assert.Contains("Gateway port must be a whole number.", settingsUpdates, StringComparison.Ordinal);
        Assert.Contains("Auto unload idle min must be a whole number.", settingsUpdates, StringComparison.Ordinal);
        Assert.Contains("Max log file MB must be a whole number.", settingsUpdates, StringComparison.Ordinal);
        Assert.Contains("new AppSettingsSaveApplicationRequest", persistence, StringComparison.Ordinal);
        Assert.Contains("settingsApplication!.SaveEditedAndApplyAsync", persistence, StringComparison.Ordinal);
        Assert.Contains("SettingsSaveActions()", persistence, StringComparison.Ordinal);
        Assert.Contains("settingsApplication!.SyncOpenCodeLocalProviderAndApplyAsync", persistence, StringComparison.Ordinal);
        Assert.Contains("new AppSettingsSaveWorkflowRequest", settingsApplication, StringComparison.Ordinal);
        Assert.Contains("AppSettingsOpenCodeSyncApplicationActions", settingsApplication, StringComparison.Ordinal);
        Assert.Contains("await actions.WriteLogAsync(ex)", settingsApplication, StringComparison.Ordinal);
        Assert.Contains("Settings saved. A model API key was generated.", settingsApplication, StringComparison.Ordinal);
        Assert.Contains("new AppSettingsUpdateRequest", settingsWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Settings saved. A model API key was generated.", persistence, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (Exception ex)", persistence, StringComparison.Ordinal);
        Assert.DoesNotContain("TryIntValue", persistence, StringComparison.Ordinal);
        Assert.DoesNotContain("AppPreferenceService.IntValue", persistence, StringComparison.Ordinal);
        Assert.False(AppPreferenceService.TryIntValue("not-a-number", out _));
        Assert.True(AppPreferenceService.TryIntValue("42", out var value));
        Assert.Equal(42, value);
    }


    [Fact]
    public void HelpPageGuidesFirstRunWithoutRunningSetupInline()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.xaml"));
        var source = ReadMainWindowSources();
        var helpContent = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "HelpContentFactory.cs"));
        var helpPageFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "HelpPageFactory.cs"));
        var helpSections = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "HelpSectionService.cs"));
        var helpNavigation = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "HelpNavigationApplicationService.cs"));
        var sections = new HelpSectionService();

        Assert.Contains("ShowHelp_Click", source, StringComparison.Ordinal);
        Assert.Contains("SetPage(\"Help\"", source, StringComparison.Ordinal);
        Assert.Contains("HelpPageFactory.Create(new HelpPageRequest(", source, StringComparison.Ordinal);
        Assert.Contains("HelpSectionTabs", helpPageFactory, StringComparison.Ordinal);
        Assert.Contains("ShowHelpSection", source, StringComparison.Ordinal);
        Assert.Contains("First Steps", helpSections, StringComparison.Ordinal);
        Assert.Contains("Overview", helpSections, StringComparison.Ordinal);
        Assert.Contains("Models", helpSections, StringComparison.Ordinal);
        Assert.Contains("Runtimes", helpSections, StringComparison.Ordinal);
        Assert.Contains("Settings", helpSections, StringComparison.Ordinal);
        Assert.Contains("OpenCode", helpSections, StringComparison.Ordinal);
        Assert.Contains("Logs & Updates", helpSections, StringComparison.Ordinal);
        Assert.Contains("HelpContentFactory.AddSection", helpPageFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("HelpContentFactory.AddSection", source, StringComparison.Ordinal);
        Assert.Equal(HelpSectionService.FirstSteps, sections.ActiveSection);
        Assert.Equal("overview", sections.Select("overview").Key);
        Assert.Equal("overview", sections.ActiveSection);
        Assert.Equal(HelpSectionService.FirstSteps, sections.Select("missing").Key);
        Assert.Equal(["first-steps", "overview", "models", "runtimes", "settings", "opencode", "maintenance"], sections.Sections.Select(section => section.Key).ToArray());
        Assert.Contains("FirstStepsCount = 5", helpContent, StringComparison.Ordinal);
        Assert.Contains("Step 1", helpContent, StringComparison.Ordinal);
        Assert.Contains("Install an official runtime", helpContent, StringComparison.Ordinal);
        Assert.Contains("official prebuilt llama.cpp runtime", helpContent, StringComparison.Ordinal);
        Assert.Contains("Windows or WSL", helpContent, StringComparison.Ordinal);
        Assert.Contains("CUDA, CPU, Vulkan, or Intel Arc SYCL", helpContent, StringComparison.Ordinal);
        Assert.Contains("Open Runtimes", helpContent, StringComparison.Ordinal);
        Assert.Contains("Scan Models Folder only if", helpContent, StringComparison.Ordinal);
        Assert.Contains("Open OpenCode", helpContent, StringComparison.Ordinal);
        Assert.Contains("Loaded Model Sessions", helpContent, StringComparison.Ordinal);
        Assert.Contains("click a Loaded Model Sessions row", helpNavigation, StringComparison.Ordinal);
        Assert.Contains("Gateway policy", helpContent, StringComparison.Ordinal);
        Assert.Contains("Auto-load gateway", helpContent, StringComparison.Ordinal);
        Assert.Contains("direct per-model endpoint", helpContent, StringComparison.Ordinal);
        Assert.DoesNotContain("new(\"Network\", \"Port\", \"port\"", source, StringComparison.Ordinal);
        Assert.Contains("AddHelpDefinitionList", helpContent, StringComparison.Ordinal);
        Assert.Contains("Make sure OpenCode is installed", helpContent, StringComparison.Ordinal);
        Assert.Contains(".opencode\\\\opencode.jsonc", helpContent, StringComparison.Ordinal);
        Assert.Contains("%USERPROFILE%\\\\.config\\\\opencode\\\\opencode.jsonc", helpContent, StringComparison.Ordinal);
        Assert.Contains("runtime-name\\\\bin\\\\llama-server.exe", helpContent, StringComparison.Ordinal);
        Assert.Contains("local-llm-runtime.json is optional", helpContent, StringComparison.Ordinal);
        Assert.Contains("skips symlinks and junctions", helpContent, StringComparison.Ordinal);
        Assert.DoesNotContain(@"D:\LLM", helpContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vision needs a model/runtime combination that can process images", helpContent, StringComparison.Ordinal);
        Assert.Contains("Whole number from 1 to 65535", helpContent, StringComparison.Ordinal);
        Assert.Contains("NavigateFromHelp", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.HelpNavigation.Plan(target)", source, StringComparison.Ordinal);
        Assert.Contains("ShowHelpNavigationDestination(plan.Destination)", source, StringComparison.Ordinal);
        Assert.Contains("ApplyHelpNavigationFocus(plan.FocusTarget)", source, StringComparison.Ordinal);
        Assert.Contains("HelpNavigationDestination.Overview", source, StringComparison.Ordinal);
        Assert.Contains("HelpNavigationFocusTarget.OpenCodeLocalModelCombo", source, StringComparison.Ordinal);
        Assert.Contains("ApplyPendingHelpFocus", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.HelpSections.Select(sectionKey)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"overview\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("case \"opencode-gateway\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_helpFocusTarget", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private string _helpSection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_helpSection =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Help: choose the highlighted CPU", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Help: in Build From Source (Advanced)", source, StringComparison.Ordinal);
        Assert.True(xaml.IndexOf("Grid.Row=\"2\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"HelpNavButton\"", StringComparison.Ordinal));
    }


    [Fact]
    public void MainWindowKeepsLogDeletionActionsAndReadableRuntimeJobRows()
    {
        var source = ReadMainWindowSources();
        var themedMessageBox = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "ThemedMessageBox.cs"));
        var runtimeDeletionPlanner = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeDeletionPlanner.cs"));
        var runtimeBuildDeletionApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeBuildDeletionApplicationService.cs"));
        var runtimeJobControls = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeBuildJobControlService.cs"));
        var settingsGridColumns = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "SettingsGridColumnFactory.cs"));
        var pageSectionFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "PageSectionFactory.cs"));
        var lifetimeFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "LifetimePageFactory.cs"));
        var lifetimePageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "LifetimePageState.cs"));
        var modelsFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "ModelsPageFactory.cs"));
        var runtimesFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "RuntimesPageFactory.cs"));
        var runtimesPageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "RuntimesPageState.cs"));
        var logsFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "LogsPageFactory.cs"));
        var logsActions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "LogsPageActionController.cs"));
        var logsPartial = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.Logs.cs"));
        var downloadHistoryPartial = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.DownloadHistory.cs"));
        var runtimesRowActions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "RuntimesPageRowActionController.cs"));
        var logWorkflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LogPageWorkflowService.cs"));
        var advancedSections = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "AdvancedSectionStateController.cs"));
        var advancedState = new AdvancedSectionStateController();

        Assert.Contains("Delete Selected", logsFactory, StringComparison.Ordinal);
        Assert.Contains("Delete All Logs", logsFactory, StringComparison.Ordinal);
        Assert.Contains("DeleteLogRow_Click", logsActions, StringComparison.Ordinal);
        Assert.Contains("DataGridSelectionMode.Extended", logsFactory, StringComparison.Ordinal);
        Assert.Contains("LogsPageFactory.Create(new LogsPageRequest(", source, StringComparison.Ordinal);
        Assert.Contains("SelectedLogPaths", source, StringComparison.Ordinal);
        Assert.Contains("LifetimePageFactory.Create(new LifetimePageRequest(", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class LifetimePageState", lifetimePageState, StringComparison.Ordinal);
        Assert.Contains("public void RefreshMetricsGrid()", lifetimePageState, StringComparison.Ordinal);
        Assert.Contains("public const string TokenUsageTitle = \"Lifetime token usage\"", lifetimeFactory, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.GridFor(MetricColumns)", lifetimeFactory, StringComparison.Ordinal);
        Assert.DoesNotContain("_lifetimeMetricsGrid", source, StringComparison.Ordinal);
        Assert.Contains("IsActiveRuntimeLog", logWorkflow, StringComparison.Ordinal);
        Assert.Contains("BuildSelectedDeletionCommand", logWorkflow, StringComparison.Ordinal);
        Assert.Contains("SolidBrush(\"#F2F5F8\")", pageSectionFactory, StringComparison.Ordinal);
        Assert.Contains("Use Log to inspect compiler, git, Windows, or WSL output.", runtimesFactory, StringComparison.Ordinal);
        Assert.Contains("OpenRuntimeJobLogRow_Click", runtimesRowActions, StringComparison.Ordinal);
        Assert.Contains("OpenLogPath(job.LogPath)", runtimesRowActions, StringComparison.Ordinal);
        Assert.Contains("Logs are not ready yet.", logsPartial, StringComparison.Ordinal);
        Assert.DoesNotContain("Logs are not ready yet.", downloadHistoryPartial, StringComparison.Ordinal);
        Assert.DoesNotContain("logPageApplication.Open(job.LogPath", downloadHistoryPartial, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.AddButtonColumn(grid, \"Log\"", runtimesFactory, StringComparison.Ordinal);
        Assert.False(advancedState.ShowRuntimes);
        Assert.True(advancedState.ToggleRuntimes());
        Assert.True(advancedState.ShowRuntimes);
        Assert.Contains("public bool ShowRuntimes { get; private set; }", advancedSections, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.AdvancedSections.ShowRuntimes", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.AdvancedSections.ToggleRuntimes();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_showAdvancedRuntimes", source, StringComparison.Ordinal);
        Assert.Contains("private readonly RuntimesPageState _runtimesPage;", source, StringComparison.Ordinal);
        Assert.Contains("_runtimesPage = uiState.RuntimesPage", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly RuntimesPageState _runtimesPage = new();", source, StringComparison.Ordinal);
        Assert.Contains("_runtimesPage.Apply(runtimesPage);", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class RuntimesPageState", runtimesPageState, StringComparison.Ordinal);
        Assert.Contains("public RuntimeRecord? SelectedRuntime", runtimesPageState, StringComparison.Ordinal);
        Assert.Contains("public string SelectedCudaPackagePreference", runtimesPageState, StringComparison.Ordinal);
        Assert.Contains("public void RestoreRuntimeJobSelection", runtimesPageState, StringComparison.Ordinal);
        Assert.Contains("public void RefreshRuntimePackageGrid()", runtimesPageState, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimePackageGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeBuildGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeJobsGrid", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeCudaPreferenceCombo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimesFolderText", source, StringComparison.Ordinal);
        Assert.Contains("request.ShowAdvancedRuntimes ? \"Hide advanced\" : \"Show advanced\"", runtimesFactory, StringComparison.Ordinal);
        Assert.Contains("CUDA downloads", runtimesFactory, StringComparison.Ordinal);
        Assert.Contains("LaunchCombo(AppPreferenceService.CudaPackagePreferenceOptions())", runtimesFactory, StringComparison.Ordinal);
        Assert.Contains("ChangeRuntimeCudaPackagePreferenceAsync", source, StringComparison.Ordinal);
        Assert.Contains("if (request.ShowAdvancedRuntimes)", runtimesFactory, StringComparison.Ordinal);
        Assert.True(runtimesFactory.IndexOf("var (header, runtimesFolderText, runtimeAdvancedToggleButton, runtimeCudaPreferenceCombo) = Header(request)", StringComparison.Ordinal)
            < runtimesFactory.IndexOf("var runtimeBuildGrid = request.ShowAdvancedRuntimes ? RuntimeBuildGrid(request) : null", StringComparison.Ordinal));
        Assert.DoesNotContain("Runtime Job Log Tail", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadSelectedRuntimeJobLog", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_runtimeJobLogBox", source, StringComparison.Ordinal);
        Assert.Contains("ClearRuntimeJobRow_Click", runtimesRowActions, StringComparison.Ordinal);
        Assert.Contains("DeleteJobAsync(job.Id)", runtimeJobControls, StringComparison.Ordinal);
        Assert.Contains("DeleteRuntimeAsync(runtime, _settings, RuntimeBuildDeletionActions())", source, StringComparison.Ordinal);
        Assert.Contains("RuntimeBuildDeletionActions()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PlanRuntimeDeletionAsync(runtime", source, StringComparison.Ordinal);
        Assert.Contains("PlanRuntimeDeletionAsync(runtime", runtimeBuildDeletionApplication, StringComparison.Ordinal);
        Assert.Contains("Register another runtime before deleting this one", runtimeDeletionPlanner, StringComparison.Ordinal);
        Assert.Contains("Saved model launch settings that use this runtime will be moved", runtimeBuildDeletionApplication, StringComparison.Ordinal);
        Assert.Contains("nameof(RuntimeCatalogRow.DeleteToolTip)", runtimesFactory, StringComparison.Ordinal);
        Assert.Contains("ButtonToolTip", source, StringComparison.Ordinal);
        Assert.Contains("ApplyStaticButtonToolTips", source, StringComparison.Ordinal);
        Assert.Contains("ToolTipService.ShowOnDisabledProperty", pageSectionFactory, StringComparison.Ordinal);
        Assert.Contains("nameof(ModelGridRow.DeleteToolTip)", modelsFactory, StringComparison.Ordinal);
        Assert.Contains("nameof(RuntimeBuildPresetRow.DownloadToolTip)", runtimesFactory, StringComparison.Ordinal);
        Assert.Contains("nameof(EditableSettingRow.ActionToolTip)", settingsGridColumns, StringComparison.Ordinal);
        Assert.Contains("tooltipBinding: \"T1\"", lifetimeFactory, StringComparison.Ordinal);
        Assert.Contains("DialogButtonToolTip", themedMessageBox, StringComparison.Ordinal);
        Assert.Contains("LogFileService.TryValidateWorkspaceLogFile(_workspaceRoot, job.LogPath", runtimeJobControls, StringComparison.Ordinal);
        Assert.Contains("LogFileService.RedactSensitiveText(tail", logWorkflow, StringComparison.Ordinal);
    }


    [Fact]
    public void RuntimeCatalogCommandsStayOutOfMainWindow()
    {
        var source = ReadMainWindowSources();
        var runtimeCatalog = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.RuntimeCatalog.cs"));
        var application = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeCatalogCommandApplicationService.cs"));

        Assert.Contains("var runtimeCatalogCommands = RuntimeServices.RuntimeCatalogCommands;", runtimeCatalog, StringComparison.Ordinal);
        Assert.Contains("runtimeCatalogCommands.ChangeCudaPackagePreferenceAsync", runtimeCatalog, StringComparison.Ordinal);
        Assert.Contains("runtimeCatalogCommands.AddCustomRepositoryAsync", runtimeCatalog, StringComparison.Ordinal);
        Assert.Contains("RuntimeCatalogPreferenceActions()", runtimeCatalog, StringComparison.Ordinal);
        Assert.Contains("RuntimeCatalogCustomRepositoryActions", runtimeCatalog, StringComparison.Ordinal);
        Assert.Contains("AppPreferenceService.CudaPackagePreference(selectedPreference)", application, StringComparison.Ordinal);
        Assert.Contains("_customRepositories.AddAsync(runtimeRoot, draft", application, StringComparison.Ordinal);
        Assert.DoesNotContain("AppPreferenceService.CudaPackagePreference(_runtimesPage.SelectedCudaPackagePreference)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CUDA downloads set to", source, StringComparison.Ordinal);
        Assert.DoesNotContain("customRuntimeRepositories.AddAsync", runtimeCatalog, StringComparison.Ordinal);
    }


    [Fact]
    public void MinimizeBehaviorUsesExplicitTrayAndTaskbarModes()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var source = ReadMainWindowSources();
        var controller = new TrayWindowStateController();

        Assert.Equal("taskbarOnly", settings.MinimizeBehavior);
        Assert.Equal(["Taskbar only", "Tray only", "Tray + taskbar"], AppPreferenceService.MinimizeBehaviorOptions());
        Assert.Equal("trayAndTaskbar", AppPreferenceService.MinimizeBehavior("Tray + taskbar"));
        Assert.Equal("both", AppPreferenceService.ModelAccessMode("network access"));
        Assert.Equal("gateway", AppPreferenceService.ModelAccessMode("Gateway LAN only"));
        Assert.True(AppPreferenceService.GatewayAllowsLanAccess("Gateway LAN only"));
        Assert.False(AppPreferenceService.DirectModelsAllowLanAccess("Gateway LAN only"));
        Assert.Equal("127.0.0.1", AppPreferenceService.RuntimeHostForAccessMode("Gateway LAN only"));
        Assert.Equal("0.0.0.0", AppPreferenceService.RuntimeHostForAccessMode("Direct models LAN only"));
        Assert.Equal("latest", settings.CudaPackagePreference);
        Assert.Equal(["Latest", "Compatibility"], AppPreferenceService.CudaPackagePreferenceOptions());
        Assert.Equal("latest", AppPreferenceService.CudaPackagePreference("Latest"));
        Assert.Equal("compatibility", AppPreferenceService.CudaPackagePreference("CUDA 12 compatibility"));
        Assert.True(AppPreferenceService.YesNoValue("on", fallback: false));
        Assert.True(AppPreferenceService.TryIntValue("42", out var parsed));
        Assert.Equal(42, parsed);
        Assert.False(AppPreferenceService.TryIntValue("bad", out _));
        Assert.Equal(10, AppPreferenceService.ClampedIntValue("99", fallback: 7, min: 1, max: 10));
        Assert.Equal(TrayMinimizeAction.TaskbarOnly, controller.BuildMinimizePlan("taskbarOnly").Action);
        Assert.Equal(TrayMinimizeAction.TrayOnly, controller.BuildMinimizePlan("trayOnly").Action);
        var trayAndTaskbar = controller.BuildMinimizePlan("trayAndTaskbar");
        Assert.Equal(TrayMinimizeAction.TrayAndTaskbar, trayAndTaskbar.Action);
        Assert.Contains("taskbar and tray", trayAndTaskbar.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TrayMinimizeAction.TrayOnly, controller.WindowStateChangedAction(System.Windows.WindowState.Minimized, "trayOnly"));
        Assert.Equal(TrayMinimizeAction.TaskbarOnly, controller.WindowStateChangedAction(System.Windows.WindowState.Normal, "trayOnly"));

        var minimize = controller.BeginHideToTray(System.Windows.WindowState.Maximized);
        Assert.True(minimize.ShouldApply);
        Assert.True(minimize.ShouldShowHint);
        Assert.True(controller.IsMinimizingToTray);
        Assert.True(controller.HasShownTrayHint);
        Assert.Equal(System.Windows.WindowState.Maximized, controller.RestoreState);
        controller.CompleteHideToTray();
        Assert.False(controller.IsMinimizingToTray);
        Assert.Equal(System.Windows.WindowState.Maximized, controller.BuildRestorePlan().RestoreState);
        var secondMinimize = controller.BeginHideToTray(System.Windows.WindowState.Minimized);
        Assert.True(secondMinimize.ShouldApply);
        Assert.False(secondMinimize.ShouldShowHint);
        Assert.Equal(System.Windows.WindowState.Maximized, secondMinimize.RestoreState);
        controller.CompleteHideToTray();

        Assert.Contains("_coreServices.Ui.TrayWindowState.BuildMinimizePlan(_settings.MinimizeBehavior)", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.TrayWindowState.WindowStateChangedAction(WindowState, _settings.MinimizeBehavior)", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.TrayWindowState.BeginHideToTray(WindowState)", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.TrayWindowState.BuildRestorePlan()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldHideToTrayOnMinimize", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShouldShowTrayWithTaskbarOnMinimize", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_windowStateBeforeTray", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_minimizingToTray", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_shownTrayHint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Tray when running", source, StringComparison.Ordinal);
    }


    [Fact]
    public void MainWindowConstrainsMaximizedWindowToWorkingArea()
    {
        var source = ReadMainWindowSources();

        Assert.Contains("ApplyWindowWorkAreaBounds", source, StringComparison.Ordinal);
        Assert.Contains("Forms.Screen.FromHandle", source, StringComparison.Ordinal);
        Assert.Contains("TransformFromDevice", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PageViewModelsBuildStableRowsFromDomainState()
    {
        var root = CreateTempRoot();
        var now = DateTimeOffset.UtcNow;
        var modelPath = Path.Combine(root, "models", "qwen-q4_k_m.gguf");
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        File.WriteAllBytes(modelPath, new byte[1536]);
        var model = new ModelRecord("model-1", "Qwen Test", modelPath, OwnershipKind.External, "{}", now);
        var modelsVm = new ModelsPageViewModel();

        modelsVm.ReplaceModels([model], active => active.Id == model.Id);

        Assert.Single(modelsVm.Rows);
        Assert.Equal("Qwen Test", modelsVm.Rows[0].Name);
        Assert.Equal("Q4_K_M", modelsVm.Rows[0].Quant);
        Assert.Equal("1.5 KB", modelsVm.Rows[0].Size);
        Assert.False(modelsVm.Rows[0].CanDelete);
        Assert.Equal(model.Id, modelsVm.Rows[0].Model.Id);
        Assert.Empty(modelsVm.VariantRows);

        var alias = new ModelRecord(
            "variant-qwen-test",
            "Qwen Test 32K",
            modelPath,
            OwnershipKind.RegistryOnly,
            ModelAliasService.CreateMetadata(model, [model]),
            now);
        modelsVm.ReplaceModels([model, alias], active => active.Id == alias.Id, item =>
            item.Id == alias.Id ? ModelLaunchSettings.FromAppSettings(AppSettings.CreateDefault(root) with { Port = 8096 }) : null);

        Assert.Single(modelsVm.Rows);
        var variant = Assert.Single(modelsVm.VariantRows);
        Assert.Equal("Qwen Test 32K", variant.Name);
        Assert.Equal("Qwen Test", variant.BaseModel);
        Assert.Equal("8096", variant.Port);
        Assert.False(variant.CanDelete);
        Assert.Equal("Remove", variant.DeleteAction);

        var overviewVm = new OverviewPageViewModel();
        overviewVm.ReplaceModels(
        [
            new ModelRecord("model-b", "Beta Test", Path.Combine(root, "models", "beta.gguf"), OwnershipKind.External, "{}", now),
            model
        ]);

        Assert.Equal(2, overviewVm.ModelChoices.Count);
        Assert.Equal("Beta Test", overviewVm.ModelChoices[0].Name);
        Assert.Equal("Qwen Test", overviewVm.ModelChoices[1].Name);
        overviewVm.ReplaceSessions(
        [
            new LoadedModelSessionSnapshot(
                "session-a",
                "model-a",
                "Alpha",
                "runtime-a",
                "CUDA Windows",
                RuntimeMode.Native,
                RuntimeBackend.Cuda,
                AppSettings.CreateDefault(root) with { Port = 8081 },
                Path.Combine(root, "a.log"),
                now,
                "",
                11,
                LoadedModelSessionStatus.Warm,
                true,
                false,
                4096),
            new LoadedModelSessionSnapshot(
                "session-b",
                "model-b",
                "Beta",
                "runtime-b",
                "CUDA WSL",
                RuntimeMode.Wsl,
                RuntimeBackend.Cuda,
                AppSettings.CreateDefault(root) with { Port = 8082 },
                Path.Combine(root, "b.log"),
                now,
                "marker",
                0,
                LoadedModelSessionStatus.Running,
                true,
                true,
                8192)
        ]);

        Assert.Equal(2, overviewVm.SessionRows.Count);
        Assert.Contains("selected", overviewVm.SessionRows[0].C1, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("8 KB", overviewVm.SessionRows[0].C2);
        Assert.Equal("Loaded", overviewVm.SessionRows[0].C3);
        Assert.Equal("http://127.0.0.1:8082/v1", overviewVm.SessionRows[0].C4);
        Assert.Equal("Loaded", overviewVm.SessionRows[1].C3);
        overviewVm.ReplaceSessions(
        [
            new LoadedModelSessionSnapshot(
                "session-b",
                "model-b",
                "Beta",
                "runtime-b",
                "CUDA WSL",
                RuntimeMode.Wsl,
                RuntimeBackend.Cuda,
                AppSettings.CreateDefault(root) with { Port = 8083 },
                Path.Combine(root, "b.log"),
                now,
                "marker",
                0,
                LoadedModelSessionStatus.Running,
                true,
                true,
                8192)
        ], "http://127.0.0.1:8082/v1");
        Assert.Equal(2, overviewVm.SessionRows.Count);
        Assert.Equal("Auto-load gateway", overviewVm.SessionRows[0].C1);
        Assert.Equal("Router", overviewVm.SessionRows[0].C2);
        Assert.Contains("Gateway: http://127.0.0.1:8082/v1", overviewVm.SessionRows[0].C4, StringComparison.Ordinal);
        Assert.Contains("Routes by model id", overviewVm.SessionRows[0].C4, StringComparison.Ordinal);
        Assert.Contains("Direct: http://127.0.0.1:8083/v1", overviewVm.SessionRows[1].C4, StringComparison.Ordinal);

        var unchangedRow = overviewVm.SessionRows[1];
        Assert.False(overviewVm.ReplaceSessionsIfChanged(
        [
            new LoadedModelSessionSnapshot(
                "session-b",
                "model-b",
                "Beta",
                "runtime-b",
                "CUDA WSL",
                RuntimeMode.Wsl,
                RuntimeBackend.Cuda,
                AppSettings.CreateDefault(root) with { Port = 8083 },
                Path.Combine(root, "b.log"),
                now,
                "marker",
                0,
                LoadedModelSessionStatus.Running,
                true,
                true,
                8192)
        ], "http://127.0.0.1:8082/v1"));
        Assert.Same(unchangedRow, overviewVm.SessionRows[1]);
        Assert.True(overviewVm.ReplaceSessionsIfChanged(
        [
            new LoadedModelSessionSnapshot(
                "session-b",
                "model-b",
                "Beta",
                "runtime-b",
                "CUDA WSL",
                RuntimeMode.Wsl,
                RuntimeBackend.Cuda,
                AppSettings.CreateDefault(root) with { Port = 8083 },
                Path.Combine(root, "b.log"),
                now,
                "marker",
                0,
                LoadedModelSessionStatus.Loading,
                true,
                true,
                8192)
        ], "http://127.0.0.1:8082/v1"));
        Assert.Equal("Loading", overviewVm.SessionRows[1].C3);
        Assert.True(overviewVm.ReplaceSessionsIfChanged(
        [
            new LoadedModelSessionSnapshot(
                "session-b",
                "model-b",
                "Beta",
                "runtime-b",
                "CUDA WSL",
                RuntimeMode.Wsl,
                RuntimeBackend.Cuda,
                AppSettings.CreateDefault(root) with { Port = 8083 },
                Path.Combine(root, "b.log"),
                now,
                "marker",
                0,
                LoadedModelSessionStatus.Running,
                true,
                true,
                8192)
        ], "http://127.0.0.1:8082/v1"));
        Assert.Equal("Loaded", overviewVm.SessionRows[1].C3);

        var preset = new RuntimeBuildPreset("official-cuda", "Official CUDA", "https://example.com/llama.cpp.git", "main", true);
        var builtRuntime = new RuntimeRecord(
            "runtime-1",
            "llama.cpp CUDA",
            RuntimeMode.Wsl,
            RuntimeBackend.Cuda,
            Path.Combine(root, "runtimes", "official-cuda", "bin", "llama-server"),
            """{"managedPresetId":"official-cuda","commit":"abcdef1234567890","folder":"D:\\runtime"}""",
            now);
        var pendingSource = new RuntimeSourceEntry("official-cpu", "Official CPU", "https://example.com/llama.cpp.git", "main", false, Path.Combine(root, "source"), "fedcba9876543210", now);
        var launchSettingsVm = new LaunchSettingsViewModel();

        launchSettingsVm.ReplaceRuntimeChoices([builtRuntime]);

        Assert.Single(launchSettingsVm.RuntimeChoices);
        Assert.Equal("runtime-1", launchSettingsVm.RuntimeChoices[0].Id);
        Assert.Contains("llama.cpp CUDA", launchSettingsVm.RuntimeChoices[0].Label, StringComparison.Ordinal);
        Assert.Equal(RuntimeBackend.Cuda, launchSettingsVm.RuntimeChoices[0].Backend);

        launchSettingsVm.ApplyRuntimeSelectorState(new LaunchRuntimeSelectorState([builtRuntime], "missing-runtime", "missing-runtime"));

        Assert.Equal(2, launchSettingsVm.RuntimeChoices.Count);
        Assert.Equal("missing-runtime", launchSettingsVm.RuntimeChoices[0].Id);
        Assert.Equal("Missing runtime (missing-runtime)", launchSettingsVm.RuntimeChoices[0].Label);
        Assert.Equal(RuntimeBackend.Cpu, launchSettingsVm.RuntimeChoices[0].Backend);
        Assert.Equal("runtime-1", launchSettingsVm.RuntimeChoices[1].Id);

        var runtimeRows = RuntimeCatalogViewService.BuildRuntimeRows(
            [builtRuntime],
            [pendingSource],
            new Dictionary<string, List<string>> { [builtRuntime.Id] = ["Qwen Test"] },
            new HashSet<string>([builtRuntime.Id], StringComparer.OrdinalIgnoreCase));
        var runtimesVm = new RuntimesPageViewModel();
        runtimesVm.ReplaceRows(runtimeRows);

        Assert.Equal(2, runtimesVm.Rows.Count);
        Assert.Equal("llama.cpp CUDA", runtimesVm.Rows[0].Name);
        Assert.Contains("Qwen Test", runtimesVm.Rows[0].Details, StringComparison.Ordinal);
        Assert.False(runtimesVm.Rows[0].CanDelete);
        Assert.Contains("Qwen Test", runtimesVm.Rows[0].DeleteToolTip, StringComparison.Ordinal);
        Assert.Contains("Unload the running model", runtimesVm.Rows[0].DeleteToolTip, StringComparison.Ordinal);
        Assert.Equal("Downloaded", runtimesVm.Rows[1].State);
        Assert.Equal("Build", runtimesVm.Rows[1].BuildAction);
        Assert.Equal(RuntimeCatalogRowKind.Source, runtimesVm.Rows[1].Kind);
        runtimesVm.ReplaceRows(RuntimeCatalogViewService.BuildRuntimeRows([builtRuntime], [], new Dictionary<string, List<string>> { [builtRuntime.Id] = ["Qwen Test"] }, new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        Assert.True(runtimesVm.Rows[0].CanDelete);
        Assert.Contains("move saved launch settings", runtimesVm.Rows[0].DeleteToolTip, StringComparison.Ordinal);
        Assert.Contains("Qwen Test", runtimesVm.Rows[0].DeleteToolTip, StringComparison.Ordinal);
        runtimesVm.ReplaceRows(RuntimeCatalogViewService.BuildRuntimeRows([builtRuntime], [], new Dictionary<string, List<string>>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        Assert.True(runtimesVm.Rows[0].CanDelete);
        Assert.Contains("Delete this runtime", runtimesVm.Rows[0].DeleteToolTip, StringComparison.Ordinal);

        var buildsVm = new RuntimeBuildsPageViewModel();
        var downloaded = new RuntimeSourceEntry(preset.Id, preset.Label, preset.RepoUrl, preset.Branch, preset.Cuda, Path.Combine(root, "source-cuda"), "abcdef1234567890", now);
        var updateState = new RuntimeUpdateState(true, "abcdef1234567890", "abcdef9999999999", now);

        buildsVm.ReplaceRows(RuntimeCatalogViewService.BuildPresetRows([preset], [], [downloaded], new Dictionary<string, RuntimeUpdateState> { [preset.Id] = updateState }));

        Assert.Equal(2, buildsVm.Rows.Count);
        Assert.Equal("Official CUDA", buildsVm.Rows[0].Label);
        Assert.Equal("CUDA WSL", buildsVm.Rows[0].Backend);
        Assert.Contains("update available", buildsVm.Rows[0].LatestLocal, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Download", buildsVm.Rows[0].DownloadAction);
        Assert.True(buildsVm.Rows[1].IsCustomAdd);

        var packagesVm = new RuntimePackagesPageViewModel();
        var runtimeCatalogView = new RuntimeCatalogViewService(new RuntimePackageStatusService());
        var packagePreset = RuntimePackageSourceCatalog.PresetRows().First();
        var packageRuntime = builtRuntime with
        {
            Id = "package-runtime",
            Name = "Official llama.cpp CUDA Windows",
            Mode = RuntimeMode.Native,
            ExecutablePath = Path.Combine(root, "runtimes", "official-prebuilt-windows-cuda-b9354", "llama-server.exe"),
            MetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                folder = Path.Combine(root, "runtimes", "official-prebuilt-windows-cuda-b9354"),
                runtimeMetadata = new
                {
                    managedPackageId = packagePreset.Id,
                    managedPresetId = packagePreset.Id,
                    releaseTag = "b9354"
                }
            })
        };
        var packageUpdate = new RuntimePackageUpdateState(true, "b9354", "b9355", "https://example.com/release", "llama-b9355-bin-win-cuda-12.4-x64.zip, cudart-llama-bin-win-cuda-12.4-x64.zip", now);

        packagesVm.ReplaceRows(runtimeCatalogView.BuildPackageRows([packagePreset], [packageRuntime], new Dictionary<string, RuntimePackageUpdateState> { [packagePreset.Id] = packageUpdate }));

        Assert.Single(packagesVm.Rows);
        Assert.Equal("Official llama.cpp CUDA Windows", packagesVm.Rows[0].Label);
        Assert.Equal("CUDA Windows", packagesVm.Rows[0].Backend);
        Assert.Contains("update available", packagesVm.Rows[0].LatestRelease, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cudart", packagesVm.Rows[0].Assets, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Update", packagesVm.Rows[0].InstallAction);
        Assert.True(packagesVm.Rows[0].CanInstall);

        var lifetimeVm = new LifetimeMetricsViewModel();
        lifetimeVm.ReplaceRows([new TokenUsageRecord(model.Id, model.Name, 10, 15, now)]);

        Assert.Equal(2, lifetimeVm.Rows.Count);
        Assert.Equal("25", lifetimeVm.Rows[0].C4);
        Assert.Equal("Qwen Test", lifetimeVm.Rows[1].C1);
        Assert.Equal(model.Id, lifetimeVm.Rows[1].Data["ModelId"]?.ToString());

        var wslVm = new WslLinuxPageViewModel();
        var report = new WslEnvironmentReport(
            true,
            true,
            "WSL ready",
            "",
            "Debian",
            "Ubuntu-24.04",
            "Install Ubuntu.",
            [
                new WslDistroInfo("Debian", "Running", "2", true, false),
                new WslDistroInfo("Ubuntu-24.04", "Stopped", "2", false, true)
            ]);

        wslVm.ReplaceDistroRows(report, "Ubuntu-24.04");

        Assert.Equal("Ubuntu-24.04", wslVm.Rows[0].C2);
        Assert.Equal("Selected", wslVm.Rows[0].C6);
        Assert.False(wslVm.Rows[0].B1);
        Assert.Equal("Debian", wslVm.Rows[1].C2);

        var windowsVm = new WindowsPageViewModel();
        var windowsTools = new WindowsToolSnapshot(
            true,
            @"C:\Git\git.exe",
            true,
            @"C:\CMake\cmake.exe",
            true,
            "Visual Studio C++ tools",
            false,
            "",
            false,
            "nvcc.exe missing",
            true,
            "VULKAN_SDK: C:\\Vulkan",
            true,
            "Intel oneAPI ready",
            true);
        windowsVm.ReplaceToolRows(windowsTools);

        Assert.Equal(4, windowsVm.Rows.Count);
        Assert.Equal("CPU tools", windowsVm.Rows[0].C1);
        Assert.Equal("Ready", windowsVm.Rows[0].C2);
        Assert.Equal("CUDA tools", windowsVm.Rows[1].C1);
        Assert.Equal("Incomplete", windowsVm.Rows[1].C2);
        Assert.Equal("Vulkan tools", windowsVm.Rows[2].C1);
        Assert.Equal("Intel oneAPI", windowsVm.Rows[3].C1);
        Assert.Equal("Ready", windowsVm.Rows[3].C2);
        Assert.Equal("Intel GPU visible to sycl-ls", windowsVm.Rows[3].C4);
        Assert.Equal("Windows GPU build tools ready", WindowsEnvironmentService.Status(windowsTools));

        var hfFile = new HuggingFaceFile("owner/repo", "model-q4.gguf", "model-q4.gguf", "Q4_K_M", 1536, 1234)
        {
            HasVisionProjector = true,
            HasConfig = true,
            HasTokenizer = true,
            CapabilityHints = "vision,reasoning,moe",
            License = "apache-2.0"
        };
        var hfVm = new HuggingFacePageViewModel();
        hfVm.ReplaceSearchResults([hfFile], HuggingFaceInstallStateService.BuildInventory([]), Path.Combine(root, "models"));

        Assert.Single(hfVm.SearchRows);
        Assert.Equal("owner/repo", hfVm.SearchRows[0].C1);
        Assert.Equal("1.5 KB", hfVm.SearchRows[0].C4);
        Assert.Contains("Vision + mmproj", hfVm.SearchRows[0].C6, StringComparison.Ordinal);
        Assert.Contains("MoE", hfVm.SearchRows[0].C6, StringComparison.Ordinal);
        Assert.Contains("Config", hfVm.SearchRows[0].C6, StringComparison.Ordinal);
        Assert.Equal("Download", hfVm.SearchRows[0].C7);
        Assert.Equal("Card", hfVm.SearchRows[0].C8);
        Assert.Contains("Download this GGUF model", hfVm.SearchRows[0].T1, StringComparison.Ordinal);
        Assert.Contains("Hugging Face model card", hfVm.SearchRows[0].T2, StringComparison.Ordinal);
        Assert.True(hfVm.SearchRows[0].B1);
        Assert.True(hfVm.SearchRows[0].B2);

        var job = new JobRecord(
            "job-1",
            "huggingface-download",
            JobStatus.Running,
            System.Text.Json.JsonSerializer.Serialize(new DownloadJobPayload(hfFile, Path.Combine(root, "models", hfFile.Name), 512, 1024)),
            Path.Combine(root, "logs", "job.log"),
            now,
            now);
        hfVm.ReplaceDownloadHistory([job]);

        Assert.Single(hfVm.DownloadHistoryRows);
        Assert.Equal("Running", hfVm.DownloadHistoryRows[0].C1);
        Assert.Equal("50% (512 B)", hfVm.DownloadHistoryRows[0].C3);
        Assert.Contains("Pause this active model download", hfVm.DownloadHistoryRows[0].T2, StringComparison.Ordinal);
        Assert.Contains("Delete this download history entry", hfVm.DownloadHistoryRows[0].T4, StringComparison.Ordinal);
        Assert.True(hfVm.DownloadHistoryRows[0].B2);
        Assert.Equal("Delete", hfVm.DownloadHistoryRows[0].C10);
        Assert.True(hfVm.DownloadHistoryRows[0].B4);

        var jobsVm = new JobsViewModel();
        var runtimeJob = job with { Id = "runtime-job", Kind = "runtime-build", Status = JobStatus.Completed, PayloadJson = """{"message":"built"}""" };
        jobsVm.ReplaceJobs([job, runtimeJob]);

        Assert.Equal(2, jobsVm.Rows.Count);
        Assert.Single(jobsVm.RuntimeRows);
        Assert.Equal("Completed", jobsVm.RuntimeRows[0].C1);
        Assert.Equal("built", jobsVm.RuntimeRows[0].C5);
        Assert.Equal("Cancel", jobsVm.RuntimeRows[0].C7);
        Assert.Equal("Retry", jobsVm.RuntimeRows[0].C8);
        Assert.False(jobsVm.RuntimeRows[0].B2);
        Assert.False(jobsVm.RuntimeRows[0].B3);
        Assert.True(jobsVm.RuntimeRows[0].B4);
        Assert.Contains("Remove this finished runtime job", jobsVm.RuntimeRows[0].T4, StringComparison.Ordinal);

        var runtimeDownloadJob = runtimeJob with { Id = "runtime-download-job", Kind = "runtime-source-download" };
        jobsVm.ReplaceJobs([runtimeDownloadJob]);
        Assert.Equal("runtime-source-download", jobsVm.RuntimeRows[0].C2);
        Assert.True(jobsVm.RuntimeRows[0].B4);

        var logsVm = new LogsViewModel();
        var logRoot = Path.Combine(root, "logs");
        Directory.CreateDirectory(logRoot);
        var runtimeLog = Path.Combine(logRoot, "llama-server-test.log");
        var jobLog = Path.Combine(logRoot, "runtime-build-test.log");
        File.WriteAllText(runtimeLog, "runtime");
        File.WriteAllText(jobLog, "job");
        var jobsByPath = new Dictionary<string, JobRecord>(StringComparer.OrdinalIgnoreCase)
        {
            [LogFileService.NormalizePath(jobLog)] = runtimeJob with { LogPath = jobLog }
        };

        logsVm.ReplaceLogs(
            [new FileInfo(runtimeLog), new FileInfo(jobLog)],
            jobsByPath,
            runtimeLog,
            "Qwen Test");

        Assert.Equal(2, logsVm.Rows.Count);
        Assert.Contains(logsVm.Rows, row => row.C1 == "Model runtime" && row.C3 == "Current model: Qwen Test");
        Assert.Contains(logsVm.Rows, row => row.C1 == "Runtime build" && row.C3.Contains("Completed", StringComparison.Ordinal));

        var runtimeMetricsVm = new RuntimeMetricsViewModel();
        runtimeMetricsVm.ReplaceSamples(
        [
            new PrometheusSample("z_metric", "", 1.25, "", "gauge", "last"),
            new PrometheusSample("a_metric", "slot=1", 9, "raw", "counter", "first")
        ]);

        Assert.Equal("a_metric", runtimeMetricsVm.Rows[0].C1);
        Assert.Equal("raw", runtimeMetricsVm.Rows[0].C3);
        Assert.Equal("z_metric", runtimeMetricsVm.Rows[1].C1);
        Assert.Equal("1.25", runtimeMetricsVm.Rows[1].C3);

        var settingsVm = new SettingsPageViewModel();
        settingsVm.ReplaceRows(
        [
            new SettingRowDefinition("Network", "LAN exposure", "modelAccessMode", "Local only", "choice", ["Local only", "Gateway LAN only", "Direct models LAN only", "Gateway + direct LAN"]),
            new SettingRowDefinition("Network", "API key", "modelApiKey", "", "secret", Action: "Generate"),
            new SettingRowDefinition("Network", "Gateway policy", "autoLoadGatewayPolicy", "Single active model", "choice", ["Prefer keeping loaded models", "Single active model"], ToolTip: "Single active model unloads other models before loading the requested model."),
            new SettingRowDefinition("Logs", "Max log file MB", "maxLogFileSizeMb", "32")
        ]);
        var accessRow = settingsVm.Rows.Single(row => row.Key == "modelAccessMode");
        var apiKeyRow = settingsVm.Rows.Single(row => row.Key == "modelApiKey");
        var gatewayPolicyRow = settingsVm.Rows.Single(row => row.Key == "autoLoadGatewayPolicy");

        Assert.Equal(4, settingsVm.Rows.Count);
        Assert.Equal("secret", apiKeyRow.Type);
        Assert.All(settingsVm.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.ToolTip)));
        Assert.Contains("unloads other models", gatewayPolicyRow.ToolTip, StringComparison.OrdinalIgnoreCase);
        Assert.True(apiKeyRow.Value.Length >= 32);
        Assert.DoesNotContain(apiKeyRow.Value, apiKeyRow.DisplayValue, StringComparison.Ordinal);
        Assert.True(apiKeyRow.CanAction);
        apiKeyRow.Value = "";
        accessRow.Value = "Gateway LAN only";
        Assert.True(apiKeyRow.Value.Length >= 32);

        var openCodeVm = new OpenCodePageViewModel();
        openCodeVm.ReplaceLocalModels([model]);
        openCodeVm.ReplaceModels([new OpenCodeModelEntry("local/qwen", "local", "qwen", "Qwen")]);
        openCodeVm.ReplaceAgents([new OpenCodeAgentEntry("config:build", "build", OpenCodeAgentKind.Config, "opencode.jsonc", "build (config)")]);

        Assert.Single(openCodeVm.LocalModelChoices);
        Assert.Equal(2, openCodeVm.ModelChoices.Count);
        Assert.False(openCodeVm.ModelChoices[0].IsAddNew);
        Assert.True(openCodeVm.ModelChoices[^1].IsAddNew);
        Assert.Equal(2, openCodeVm.AgentChoices.Count);
        Assert.True(openCodeVm.AgentChoices[^1].IsAddNew);

        var updatesVm = new UpdatesPageViewModel();

        Assert.Equal("Check For Updates", updatesVm.ActionText);
        Assert.Contains("No update check", updatesVm.StatusDetails, StringComparison.Ordinal);

        updatesVm.SetLatestUpdate(new AppUpdateInfo(
            true,
            "v1.0",
            "v1.1.2",
            "Release v1.1.2",
            new string('x', 1900),
            "https://example.com/release",
            "LlamaCppWindowsManager.exe",
            "https://example.com/download",
            123));

        Assert.True(updatesVm.HasAvailableUpdate);
        Assert.Equal("Install Update", updatesVm.NavigationText);
        Assert.Contains("v1.0 -> v1.1.2", updatesVm.StatusText, StringComparison.Ordinal);
        Assert.Contains("Release v1.1.2", updatesVm.LatestReleaseText, StringComparison.Ordinal);
        Assert.True(updatesVm.LatestReleaseText.Length < 1900);
    }


    [Fact]
    public void MainWindowViewModelTracksPageStatusAndBusyState()
    {
        var vm = new MainWindowViewModel();
        var source = ReadMainWindowSources();
        var controller = new UiBusyStateController();
        var pageEnabled = true;
        bool? waitCursor = null;

        Assert.Equal("Overview", vm.CurrentPage);
        Assert.Equal("Starting...", vm.StatusText);
        Assert.True(vm.TryBeginBusy(out var busyMessage));
        Assert.Equal("", busyMessage);
        Assert.False(vm.TryBeginBusy(out busyMessage));
        Assert.Equal("Please wait: Starting...", busyMessage);
        Assert.True(vm.EndBusy());
        Assert.False(vm.EndBusy());

        vm.CurrentPage = "Models";
        vm.SetStatus("");

        Assert.Equal("Models", vm.CurrentPage);
        Assert.Equal("Ready", vm.DisplayStatusText);

        controller.Begin(
            pageEnabled,
            enabled => pageEnabled = enabled,
            enabled => waitCursor = enabled);

        Assert.True(controller.HasActiveBusyState);
        Assert.False(pageEnabled);
        Assert.True(waitCursor);

        controller.Begin(
            pageIsEnabled: true,
            enabled => pageEnabled = enabled,
            enabled => waitCursor = enabled);

        Assert.False(pageEnabled);
        Assert.True(waitCursor);
        Assert.True(controller.End(enabled => pageEnabled = enabled, enabled => waitCursor = enabled));
        Assert.True(pageEnabled);
        Assert.False(waitCursor);
        Assert.False(controller.HasActiveBusyState);
        Assert.False(controller.End(enabled => pageEnabled = enabled, enabled => waitCursor = enabled));
        Assert.Contains("_coreServices.Ui.UiBusyState.Begin(PageHost.IsEnabled, SetPageHostEnabled, SetWaitCursor)", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Ui.UiBusyState.End(SetPageHostEnabled, SetWaitCursor)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_pageHostEnabledBeforeBusy", source, StringComparison.Ordinal);
    }

}
