using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public void HostExecutableResolverDoesNotReturnUnresolvedPathSearchNames()
    {
        Assert.Throws<FileNotFoundException>(() => HostExecutableResolver.ResolveOnPath($"definitely-missing-{Guid.NewGuid():N}.exe"));
    }


    [Fact]
    public void HostExecutableResolverIgnoresRelativePathEntries()
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalDirectory = Environment.CurrentDirectory;
        var root = CreateTempRoot();
        var relativeDirectory = "relative-tools";
        Directory.CreateDirectory(Path.Combine(root, relativeDirectory));
        File.WriteAllText(Path.Combine(root, relativeDirectory, "fake-tool.exe"), "");
        try
        {
            Environment.CurrentDirectory = root;
            Environment.SetEnvironmentVariable("PATH", relativeDirectory);

            Assert.Throws<FileNotFoundException>(() => HostExecutableResolver.ResolveOnPath("fake-tool.exe"));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }


    [Fact]
    public void EmbeddedBuildScriptUsesStdinAndChecksCudaRuntimeLibrary()
    {
        var script = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "tools", "Build-LlamaCppRuntime.ps1"));

        Assert.Contains(".local-llm-build.sh", script, StringComparison.Ordinal);
        Assert.Contains("UTF8Encoding $false", script, StringComparison.Ordinal);
        Assert.Contains("bash <build>", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Script |", script, StringComparison.Ordinal);
        Assert.DoesNotContain("bash -lc $Script", script, StringComparison.Ordinal);
        Assert.Contains("libcudart", script, StringComparison.Ordinal);
        Assert.Contains("[switch] $Vulkan", script, StringComparison.Ordinal);
        Assert.Contains("[switch] $Sycl", script, StringComparison.Ordinal);
        Assert.Contains("Vulkan build dependencies", script, StringComparison.Ordinal);
        Assert.Contains("vulkaninfo --summary", script, StringComparison.Ordinal);
        Assert.Contains("Vulkan_GLSLC_EXECUTABLE", script, StringComparison.Ordinal);
        Assert.Contains("-DGGML_VULKAN=ON", script, StringComparison.Ordinal);
        Assert.Contains("Intel oneAPI", script, StringComparison.Ordinal);
        Assert.Contains("source /opt/intel/oneapi/setvars.sh", script, StringComparison.Ordinal);
        Assert.Contains("sycl-ls", script, StringComparison.Ordinal);
        Assert.Contains("sycl_cmake_args", script, StringComparison.Ordinal);
        Assert.Contains("-DGGML_SYCL=ON", script, StringComparison.Ordinal);
        Assert.Contains("ONEAPI_DEVICE_SELECTOR=level_zero:gpu", script, StringComparison.Ordinal);
        Assert.Contains("server_path=$InstallQ/bin/llama-server", script, StringComparison.Ordinal);
        Assert.Contains("--version >/dev/null 2>&1", script, StringComparison.Ordinal);
        Assert.Contains("probe_ld_path", script, StringComparison.Ordinal);
        Assert.Contains("Resolve-WslDistroName", script, StringComparison.Ordinal);
        Assert.Contains("LLAMA_CPP_WINDOWS_MANAGER_BUILD_MARKER", script, StringComparison.Ordinal);
        Assert.Contains("LLAMA_CPP_CONSOLE_BUILD_MARKER", script, StringComparison.Ordinal);
        Assert.Contains("LOCAL_LLM_CONSOLE_BUILD_MARKER", script, StringComparison.Ordinal);
        Assert.DoesNotContain("exit \"`$build_status", script, StringComparison.Ordinal);
    }


    [Fact]
    public void CommandLineServiceQuotesPowerShellBashAndWslCleanupMarkers()
    {
        Assert.Equal("'a''b'", CommandLineService.PowerShellQuote("a'b"));
        Assert.Equal("'a'\"'\"'b'", CommandLineService.BashQuote("a'b"));
        Assert.Equal("second", CommandLineService.FirstNonBlankLine("\r\n  second  \nthird"));

        var cleanup = CommandLineService.WslKillByEnvironmentMarkerCommand("marker'1");
        var ubuntuInstall = WslSetupCommands.InstallUbuntuAndBuildToolsPowerShell("C:\\Windows\\System32\\wsl.exe");
        var deleteWsl = WslSetupCommands.DeleteWslPowerShell("C:\\Windows\\System32\\wsl.exe");
        var deleteUbuntu = WslSetupCommands.DeleteUbuntuPowerShell("C:\\Windows\\System32\\wsl.exe", "Ubuntu-24.04");
        var windowsCpuInstall = WindowsSetupCommands.InstallCpuToolsPowerShell();

        Assert.Contains("LLAMA_CPP_WINDOWS_MANAGER_BUILD_MARKER=marker'\"'\"'1", cleanup, StringComparison.Ordinal);
        Assert.Contains("LLAMA_CPP_CONSOLE_BUILD_MARKER=marker'\"'\"'1", cleanup, StringComparison.Ordinal);
        Assert.Contains("LOCAL_LLM_CONSOLE_BUILD_MARKER=marker'\"'\"'1", cleanup, StringComparison.Ordinal);
        Assert.Contains("/proc/[0-9]*/environ", cleanup, StringComparison.Ordinal);
        Assert.Contains("kill \"$pid\"", cleanup, StringComparison.Ordinal);
        Assert.Contains("--install -d 'Ubuntu-24.04'", ubuntuInstall, StringComparison.Ordinal);
        Assert.Contains("Installing llama.cpp CPU build tools", ubuntuInstall, StringComparison.Ordinal);
        Assert.Contains("'C:\\Windows\\System32\\wsl.exe' -d 'Ubuntu-24.04' -- bash -s", ubuntuInstall, StringComparison.Ordinal);
        Assert.Contains("DELETE WSL", deleteWsl, StringComparison.Ordinal);
        Assert.Contains("--unregister 'Ubuntu-24.04'", deleteUbuntu, StringComparison.Ordinal);
        Assert.Contains("winget install --id Git.Git", windowsCpuInstall, StringComparison.Ordinal);
        Assert.Contains("Microsoft.VisualStudio.2022.BuildTools", windowsCpuInstall, StringComparison.Ordinal);
    }

    [Fact]
    public void VisibleCommandLaunchServiceOwnsVisiblePowerShellProcessLaunch()
    {
        var commandLine = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "CommandLineService.cs"));
        var started = new List<ProcessStartInfo>();
        var service = new VisibleCommandLaunchService(started.Add, () => "C:\\Windows\\System32\\wsl.exe");

        service.StartVisiblePowerShellScript("Write-Host ok", elevated: true);
        service.StartVisibleWindowsCommand("C:\\Tools\\tool.exe", ["a b", "c'd"], elevated: false);
        service.StartVisibleWslBashScript("Ubuntu-24.04", "echo hello", elevated: false);

        Assert.Equal(3, started.Count);
        Assert.All(started, process =>
        {
            Assert.Equal(HostExecutableResolver.WindowsPowerShellExe(), process.FileName);
            Assert.True(process.UseShellExecute);
            Assert.Equal(ProcessWindowStyle.Normal, process.WindowStyle);
            Assert.Contains("-NoExit -NoProfile -ExecutionPolicy Bypass -EncodedCommand", process.Arguments, StringComparison.Ordinal);
        });
        Assert.Equal("runas", started[0].Verb);
        Assert.Equal("", started[1].Verb);
        Assert.Contains("Write-Host ok", DecodeVisiblePowerShell(started[0]), StringComparison.Ordinal);
        Assert.Contains("& 'C:\\Tools\\tool.exe' 'a b' 'c''d'", DecodeVisiblePowerShell(started[1]), StringComparison.Ordinal);
        Assert.Contains("'C:\\Windows\\System32\\wsl.exe' -d 'Ubuntu-24.04' -- bash -s", DecodeVisiblePowerShell(started[2]), StringComparison.Ordinal);
        Assert.DoesNotContain("new VisibleCommandLaunchService", commandLine, StringComparison.Ordinal);
        Assert.DoesNotContain("Process.Start", commandLine, StringComparison.Ordinal);

        static string DecodeVisiblePowerShell(ProcessStartInfo process)
        {
            var parts = process.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var encodedIndex = Array.IndexOf(parts, "-EncodedCommand");
            Assert.True(encodedIndex >= 0 && encodedIndex + 1 < parts.Length);
            return System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(parts[encodedIndex + 1]));
        }
    }


    [Fact]
    public void WslToolSetupWorkflowServiceBuildsSafeActionPlans()
    {
        var service = new WslToolSetupWorkflowService(
            new VisibleCommandLaunchService(_ => { }, () => "C:\\Windows\\System32\\wsl.exe"),
            () => "C:\\Windows\\System32\\wsl.exe");

        var installWsl = service.Plan(WslToolSetupAction.InstallWsl);
        var installUbuntu = service.Plan(WslToolSetupAction.InstallUbuntu);
        var deleteWsl = service.Plan(WslToolSetupAction.DeleteWsl);
        var checkUbuntu = service.Plan(WslToolSetupAction.CheckUbuntuUpdates, "Ubuntu-24.04");
        var deleteUbuntu = service.Plan(WslToolSetupAction.DeleteUbuntu, "Ubuntu-24.04");
        var cpuTools = service.Plan(WslToolSetupAction.InstallUbuntuBuildTools, "Ubuntu-24.04");
        var cuda = service.Plan(WslToolSetupAction.InstallUbuntuCudaToolkit, "Ubuntu-24.04");
        var vulkan = service.Plan(WslToolSetupAction.InstallUbuntuVulkanTools, "Ubuntu-24.04");
        var syclRuntimeDelete = service.Plan(WslToolSetupAction.DeleteUbuntuSyclRuntime, "Ubuntu-24.04", "Test App");
        var oneApi = service.Plan(WslToolSetupAction.InstallUbuntuSyclOneApi, "Ubuntu-24.04");

        Assert.Equal(WslToolSetupLaunchKind.WindowsCommand, installWsl.LaunchKind);
        Assert.Equal("C:\\Windows\\System32\\wsl.exe", installWsl.Executable);
        Assert.Equal(["--install", "--no-distribution"], installWsl.Arguments);
        Assert.True(installWsl.Elevated);
        Assert.Contains("--install -d 'Ubuntu-24.04'", installUbuntu.PowerShellScript, StringComparison.Ordinal);
        Assert.True(deleteWsl.IsWarning);
        Assert.True(service.RequiresUbuntuDistro(WslToolSetupAction.InstallUbuntuCudaToolkit));
        Assert.False(service.RequiresUbuntuDistro(WslToolSetupAction.InstallUbuntu));
        Assert.Throws<ArgumentException>(() => service.Plan(WslToolSetupAction.CheckUbuntuUpdates));
        Assert.Equal(WslToolSetupLaunchKind.WslBashScript, checkUbuntu.LaunchKind);
        Assert.Equal("sudo apt update && apt list --upgradable", checkUbuntu.BashScript);
        Assert.Contains("--unregister 'Ubuntu-24.04'", deleteUbuntu.PowerShellScript, StringComparison.Ordinal);
        Assert.Contains(WslSetupCommands.BuildToolsPackages, cpuTools.ConfirmationMessage, StringComparison.Ordinal);
        Assert.Contains(WslSetupCommands.CudaToolkitPackage, cuda.ConfirmationMessage, StringComparison.Ordinal);
        Assert.Contains("vulkaninfo --summary", vulkan.ConfirmationMessage, StringComparison.Ordinal);
        Assert.True(syclRuntimeDelete.IsWarning);
        Assert.Contains("Test App", syclRuntimeDelete.ConfirmationMessage, StringComparison.Ordinal);
        Assert.Contains("apt.repos.intel.com/oneapi", oneApi.BashScript, StringComparison.Ordinal);
    }


    [Fact]
    public void WslToolSetupApplicationServiceOwnsDistroAdmissionConfirmExecuteAndStatus()
    {
        var plan = new WslToolSetupPlan(
            WslToolSetupAction.InstallUbuntuCudaToolkit,
            WslToolSetupLaunchKind.WslBashScript,
            "Install CUDA",
            "Install CUDA?",
            IsWarning: false,
            Elevated: false,
            "CUDA started.",
            DistroName: "Ubuntu-24.04",
            BashScript: "echo cuda");
        var calls = new List<string>();
        var confirm = false;
        var service = new WslToolSetupApplicationService(
            action => action == WslToolSetupAction.InstallUbuntuCudaToolkit,
            (action, distro, appName) =>
            {
                calls.Add($"plan:{action}:{distro}:{appName}");
                return plan;
            },
            executedPlan => calls.Add($"execute:{executedPlan.Action}:{executedPlan.DistroName}"));
        WslToolSetupApplicationActions Actions()
            => new(
                confirmation =>
                {
                    calls.Add($"confirm:{confirmation.Title}");
                    return confirm;
                },
                status => calls.Add($"status:{status}"));

        var missingDistro = service.Run(WslToolSetupAction.InstallUbuntuCudaToolkit, "", "Test App", Actions());
        var cancelled = service.Run(WslToolSetupAction.InstallUbuntuCudaToolkit, "Ubuntu-24.04", "Test App", Actions());

        confirm = true;

        var started = service.Run(WslToolSetupAction.InstallUbuntuCudaToolkit, "Ubuntu-24.04", "Test App", Actions());

        Assert.Equal(ToolSetupApplicationOutcome.MissingRequiredDistro, missingDistro);
        Assert.Equal(ToolSetupApplicationOutcome.Cancelled, cancelled);
        Assert.Equal(ToolSetupApplicationOutcome.Started, started);
        Assert.Equal([
            "status:Install or select an Ubuntu distro first.",
            "plan:InstallUbuntuCudaToolkit:Ubuntu-24.04:Test App",
            "confirm:Install CUDA",
            "plan:InstallUbuntuCudaToolkit:Ubuntu-24.04:Test App",
            "confirm:Install CUDA",
            "execute:InstallUbuntuCudaToolkit:Ubuntu-24.04",
            "status:CUDA started."
        ], calls);
    }


    [Fact]
    public async Task WslDistroSelectionApplicationServiceOwnsRowSelectionPersistenceAndRefresh()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { WslDistro = "Debian" };
        var service = new WslDistroSelectionApplicationService();
        var calls = new List<string>();
        var row = new UiRow { C2 = "Fallback" };
        row.Data["Name"] = System.Text.Json.Nodes.JsonValue.Create("Ubuntu-24.04");

        WslDistroSelectionApplicationActions Actions()
            => new(
                updated =>
                {
                    calls.Add($"persist:{updated.WslDistro}");
                    return Task.FromResult(updated);
                },
                () =>
                {
                    calls.Add("refresh-wsl");
                    return Task.CompletedTask;
                },
                status => calls.Add($"status:{status}"));

        var ignored = await service.SelectAsync(settings, null, Actions());
        var applied = await service.SelectAsync(settings, row, Actions());
        var selectedUbuntu = new UiRow { C2 = "Ubuntu-Selected", Data = new System.Text.Json.Nodes.JsonObject { ["IsUbuntu"] = true } };
        var selectedDebian = new UiRow { C2 = "Debian", Data = new System.Text.Json.Nodes.JsonObject { ["Name"] = "Debian", ["IsUbuntu"] = false } };
        var configuredUbuntu = new UiRow { C2 = "Ubuntu-Configured", Data = new System.Text.Json.Nodes.JsonObject { ["Name"] = "Ubuntu-Configured", ["IsUbuntu"] = true } };
        var fallbackUbuntu = new UiRow { C2 = "Ubuntu-Fallback", Data = new System.Text.Json.Nodes.JsonObject { ["Name"] = "Ubuntu-Fallback", ["IsUbuntu"] = true } };

        Assert.Equal(WslDistroSelectionApplicationOutcome.Ignored, ignored.Outcome);
        Assert.Equal(WslDistroSelectionApplicationOutcome.Applied, applied.Outcome);
        Assert.Equal("Ubuntu-24.04", applied.Settings.WslDistro);
        Assert.Equal("Ubuntu-24.04", applied.DistroName);
        Assert.Equal(
            "Ubuntu-Selected",
            WslDistroSelectionApplicationService.PreferredUbuntuDistroName(
                selectedUbuntu,
                [configuredUbuntu, fallbackUbuntu],
                "Ubuntu-Configured"));
        Assert.Equal(
            "Ubuntu-Configured",
            WslDistroSelectionApplicationService.PreferredUbuntuDistroName(
                selectedDebian,
                [configuredUbuntu, fallbackUbuntu],
                "Ubuntu-Configured"));
        Assert.Equal(
            "Ubuntu-Fallback",
            WslDistroSelectionApplicationService.PreferredUbuntuDistroName(
                selectedDebian,
                [selectedDebian, fallbackUbuntu],
                "missing"));
        Assert.Equal([
            "persist:Ubuntu-24.04",
            "refresh-wsl",
            "status:WSL distro set to Ubuntu-24.04."
        ], calls);
    }


    [Fact]
    public void WslDistroSelectionPolicyStaysOutOfMainWindow()
    {
        var mainWindow = ReadMainWindowSources();
        var actions = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.WslActions.cs"));
        var application = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "WslDistroSelectionApplicationService.cs"));

        Assert.Contains("_coreServices.Environment.WslDistroSelectionApplication.SelectAsync", actions, StringComparison.Ordinal);
        Assert.Contains("WslDistroSelectionActions()", actions, StringComparison.Ordinal);
        Assert.Contains("WslDistroSelectionApplicationService.PreferredUbuntuDistroName", actions, StringComparison.Ordinal);
        Assert.Contains("DistroName(UiRow? row)", application, StringComparison.Ordinal);
        Assert.Contains("PreferredUbuntuDistroName(", application, StringComparison.Ordinal);
        Assert.Contains("WSL distro set to", application, StringComparison.Ordinal);
        Assert.DoesNotContain("Data[\"IsUbuntu\"]", actions, StringComparison.Ordinal);
        Assert.DoesNotContain("_settings = _settings with { WslDistro =", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("WSL distro set to", mainWindow, StringComparison.Ordinal);
    }


    [Fact]
    public async Task WslPageWorkflowServiceSelectsRecommendedDistroAndProbesTools()
    {
        var root = CreateTempRoot();
        var report = new WslEnvironmentReport(
            WslExeFound: true,
            WslWorking: true,
            Status: "WSL ready",
            Details: "",
            DefaultDistro: "Ubuntu-24.04",
            RecommendedDistro: "Ubuntu-24.04",
            RecommendedAction: "",
            Distros: [new WslDistroInfo("Ubuntu-24.04", "Running", "2", IsDefault: true, IsUbuntu: true)]);
        var runner = new ScriptedProcessRunner(_ => new ProcessRunResult(
            0,
            "CPU=1\nCPU_SUMMARY=CPU OK\nCUDA=1\nCUDA_SUMMARY=CUDA OK\nVULKAN=0\nVULKAN_SUMMARY=Vulkan missing\nSYCL=1\nSYCL_SUMMARY=SYCL OK",
            ""));
        var service = new WslPageWorkflowService(_ => Task.FromResult(report), runner, () => "C:\\Windows\\System32\\wsl.exe");

        var result = await service.RefreshAsync(AppSettings.CreateDefault(root) with { WslDistro = "missing" }, TestContext.Current.CancellationToken);

        Assert.True(result.SettingsChanged);
        Assert.Equal("Ubuntu-24.04", result.Settings.WslDistro);
        Assert.True(result.Tools.CpuToolsInstalled);
        Assert.True(result.Tools.CudaToolsInstalled);
        Assert.False(result.Tools.VulkanToolsInstalled);
        Assert.True(result.Tools.SyclToolsInstalled);
        Assert.Single(runner.Commands);
        Assert.Equal(["-d", "Ubuntu-24.04", "--", "bash", "-s"], runner.Commands[0]);
        Assert.Contains("libcudart", runner.StandardInputs[0], StringComparison.Ordinal);
    }


    [Fact]
    public async Task WslPageWorkflowServiceSkipsToolProbeWhenWslIsUnavailable()
    {
        var root = CreateTempRoot();
        var report = new WslEnvironmentReport(
            WslExeFound: false,
            WslWorking: false,
            Status: "WSL not installed",
            Details: "",
            DefaultDistro: "",
            RecommendedDistro: "Ubuntu-24.04",
            RecommendedAction: "",
            Distros: []);
        var runner = new ScriptedProcessRunner(_ => throw new InvalidOperationException("Tool probe should not run."));
        var service = new WslPageWorkflowService(_ => Task.FromResult(report), runner, () => "wsl.exe");

        var result = await service.RefreshAsync(AppSettings.CreateDefault(root), TestContext.Current.CancellationToken);

        Assert.False(result.SettingsChanged);
        Assert.Equal("CPU tools unknown | CUDA unknown | Vulkan unknown | SYCL unknown", WslEnvironmentService.ToolSummary(result.Tools));
        Assert.Empty(runner.Commands);
    }


    [Fact]
    public void UbuntuInstallerIncludesCmakeBuildTools()
    {
        var mainWindow = ReadMainWindowSources();
        var wslPageFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "WslPageFactory.cs"));
        var wslPageWorkflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "WslPageWorkflowService.cs"));

        Assert.Contains("cmake", WslSetupCommands.BuildToolsPackages, StringComparison.Ordinal);
        Assert.Contains("build-essential", WslSetupCommands.BuildToolsPackages, StringComparison.Ordinal);
        Assert.Contains("libcurl4-openssl-dev", WslSetupCommands.InstallBuildToolsCommand, StringComparison.Ordinal);
        Assert.Contains("WslPageFactory.Create(new WslPageRequest(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Install CPU Tools", wslPageFactory, StringComparison.Ordinal);
        Assert.Contains("CPU build tools do not include CUDA", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "tools", "Build-LlamaCppRuntime.ps1")), StringComparison.Ordinal);
        Assert.Contains("Install CUDA", wslPageFactory, StringComparison.Ordinal);
        Assert.Contains("Install Vulkan", wslPageFactory, StringComparison.Ordinal);
        Assert.Contains("Install Intel GPU", wslPageFactory, StringComparison.Ordinal);
        Assert.Contains("Install oneAPI", wslPageFactory, StringComparison.Ordinal);
        Assert.Contains("Delete WSL", wslPageFactory, StringComparison.Ordinal);
        Assert.Contains("Delete Ubuntu", wslPageFactory, StringComparison.Ordinal);
        Assert.Equal("Update CPU Tools", WslEnvironmentService.CpuToolsActionLabel(new WslToolSnapshot(true, false, false, "CPU OK", "CUDA missing", "Vulkan missing")));
        Assert.Equal("Update CUDA", WslEnvironmentService.CudaToolsActionLabel(new WslToolSnapshot(false, true, false, "CPU missing", "CUDA OK", "Vulkan missing")));
        Assert.Equal("Update Vulkan", WslEnvironmentService.VulkanToolsActionLabel(new WslToolSnapshot(false, false, true, "CPU missing", "CUDA missing", "Vulkan OK")));
        Assert.Equal("Update oneAPI", WslEnvironmentService.SyclToolsActionLabel(new WslToolSnapshot(false, false, false, "CPU missing", "CUDA missing", "Vulkan missing", true, "SYCL OK")));
        Assert.Equal("cuda-toolkit-13-2", WslSetupCommands.CudaToolkitPackage);
        Assert.Contains("cuda-keyring_1.1-1_all.deb", WslSetupCommands.InstallCudaToolkitCommand, StringComparison.Ordinal);
        Assert.Contains("/usr/local/cuda*/bin/nvcc", WslSetupCommands.InstallCudaToolkitCommand, StringComparison.Ordinal);
        Assert.Contains("libvulkan-dev", WslSetupCommands.VulkanToolsPackages, StringComparison.Ordinal);
        Assert.Contains("glslc", WslSetupCommands.InstallVulkanToolsCommand, StringComparison.Ordinal);
        Assert.Contains("vulkaninfo --summary", WslSetupCommands.InstallVulkanToolsCommand, StringComparison.Ordinal);
        Assert.Contains("libze-intel-gpu1", WslSetupCommands.SyclRuntimePackages, StringComparison.Ordinal);
        Assert.Contains("intel-oneapi-compiler-dpcpp-cpp", WslSetupCommands.SyclOneApiPackages, StringComparison.Ordinal);
        Assert.Contains("apt.repos.intel.com/oneapi", WslSetupCommands.InstallSyclOneApiCommand, StringComparison.Ordinal);
        Assert.Contains("ToolProbeCommand", wslPageWorkflow, StringComparison.Ordinal);
        Assert.Contains("libcudart", WslSetupCommands.ToolProbeCommand, StringComparison.Ordinal);
        Assert.Contains("VULKAN_SUMMARY", WslSetupCommands.ToolProbeCommand, StringComparison.Ordinal);
        Assert.Contains("SYCL_SUMMARY", WslSetupCommands.ToolProbeCommand, StringComparison.Ordinal);
        Assert.Contains("CPU build tools do not include CUDA", WslSetupCommands.CudaToolkitPreflightCommand, StringComparison.Ordinal);
        Assert.Contains("libcudart", WslSetupCommands.CudaToolkitPreflightCommand, StringComparison.Ordinal);
        Assert.Contains("Vulkan build dependencies", WslSetupCommands.VulkanToolsPreflightCommand, StringComparison.Ordinal);
        Assert.Contains("vulkaninfo", WslSetupCommands.VulkanToolsPreflightCommand, StringComparison.Ordinal);
        Assert.Contains("sycl-ls", WslSetupCommands.SyclToolsPreflightCommand, StringComparison.Ordinal);
        Assert.Contains("Level Zero Intel GPU", WslSetupCommands.SyclToolsPreflightCommand, StringComparison.Ordinal);
        Assert.Contains("CUDAToolkit_ROOT", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "tools", "Build-LlamaCppRuntime.ps1")), StringComparison.Ordinal);
        Assert.Contains("CMAKE_CUDA_COMPILER", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "tools", "Build-LlamaCppRuntime.ps1")), StringComparison.Ordinal);
        Assert.Contains("vulkan_cmake_args", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "tools", "Build-LlamaCppRuntime.ps1")), StringComparison.Ordinal);
        Assert.Contains("sycl_cmake_args", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "tools", "Build-LlamaCppRuntime.ps1")), StringComparison.Ordinal);
    }


    [Fact]
    public void WslDistroParserHandlesDefaultUbuntu()
    {
        const string raw = """
          NAME              STATE           VERSION
        * Ubuntu-24.04      Stopped         2
          docker-desktop    Running         2
        """;

        var distros = WslEnvironmentService.ParseDistroList(raw);

        Assert.Single(distros);
        Assert.Equal("Ubuntu-24.04", distros[0].Name);
        Assert.True(distros[0].IsDefault);
        Assert.True(distros[0].IsUbuntu);
    }


    [Fact]
    public void WslEnvironmentServiceSummarizesSelectedDistrosAndProbeOutput()
    {
        var report = new WslEnvironmentReport(
            WslExeFound: true,
            WslWorking: true,
            Status: "WSL ready",
            Details: "",
            DefaultDistro: "Debian",
            RecommendedDistro: "Ubuntu-24.04",
            RecommendedAction: "",
            Distros:
            [
                new WslDistroInfo("Debian", "Running", "2", true, false),
                new WslDistroInfo("Ubuntu-24.04", "Stopped", "2", false, true)
            ]);

        var values = WslEnvironmentService.ParseKeyValueLines("CPU=1\nCPU_SUMMARY=CPU OK, CMake 3.28\nbad-line\nCUDA=0");
        var tools = WslEnvironmentService.ParseToolProbeOutput("CPU=1\nCPU_SUMMARY=CPU OK, CMake 3.28\nCUDA=0\nCUDA_SUMMARY=CUDA missing nvcc\nVULKAN=1\nVULKAN_SUMMARY=Vulkan OK, Microsoft Direct3D12\nSYCL=1\nSYCL_SUMMARY=SYCL OK, Intel Arc");
        var unknownTools = WslEnvironmentService.UnknownToolSnapshot();

        Assert.Equal("Ubuntu-24.04", WslEnvironmentService.SelectedUbuntuDistroName(report, "missing"));
        Assert.Equal("Ubuntu-24.04", WslEnvironmentService.SelectedUbuntuDistroName(report, "Ubuntu-24.04"));
        Assert.Equal("Ubuntu-24.04 | WSL 2 | Stopped", WslEnvironmentService.SelectedDistroSummary(report, "Ubuntu-24.04"));
        Assert.Equal("missing (missing)", WslEnvironmentService.SelectedDistroSummary(report, "missing"));
        Assert.Equal("2 distro(s), 1 Ubuntu", WslEnvironmentService.InstalledDistroSummary(report));
        Assert.Equal("1", values["cpu"]);
        Assert.Equal("CPU OK, CMake 3.28", values["CPU_SUMMARY"]);
        Assert.False(values.ContainsKey("bad-line"));
        Assert.True(tools.CpuToolsInstalled);
        Assert.False(tools.CudaToolsInstalled);
        Assert.True(tools.VulkanToolsInstalled);
        Assert.True(tools.SyclToolsInstalled);
        Assert.Equal("CPU OK, CMake 3.28 | CUDA missing nvcc | Vulkan OK, Microsoft Direct3D12 | SYCL OK, Intel Arc", WslEnvironmentService.ToolSummary(tools));
        Assert.Equal("Update CPU Tools", WslEnvironmentService.CpuToolsActionLabel(tools));
        Assert.Equal("Install CUDA", WslEnvironmentService.CudaToolsActionLabel(tools));
        Assert.Equal("Update Vulkan", WslEnvironmentService.VulkanToolsActionLabel(tools));
        Assert.Equal("Update oneAPI", WslEnvironmentService.SyclToolsActionLabel(tools));
        Assert.Equal("CPU tools unknown | CUDA unknown | Vulkan unknown | SYCL unknown", WslEnvironmentService.ToolSummary(unknownTools));
        Assert.Contains("Ubuntu-24.04", WslEnvironmentService.CudaToolkitIncompleteMessage("Ubuntu-24.04", "CUDA missing nvcc"), StringComparison.Ordinal);
        Assert.Contains("CUDA missing nvcc", WslEnvironmentService.CudaToolkitIncompleteMessage("Ubuntu-24.04", "CUDA missing nvcc"), StringComparison.Ordinal);
        Assert.Contains("Ubuntu-24.04", WslEnvironmentService.VulkanToolsIncompleteMessage("Ubuntu-24.04", "Vulkan missing glslc"), StringComparison.Ordinal);
        Assert.Contains("Vulkan missing glslc", WslEnvironmentService.VulkanToolsIncompleteMessage("Ubuntu-24.04", "Vulkan missing glslc"), StringComparison.Ordinal);
        Assert.Contains("Ubuntu-24.04", WslEnvironmentService.SyclToolsIncompleteMessage("Ubuntu-24.04", "SYCL missing"), StringComparison.Ordinal);
        Assert.Contains("SYCL missing", WslEnvironmentService.SyclToolsIncompleteMessage("Ubuntu-24.04", "SYCL missing"), StringComparison.Ordinal);
        Assert.Contains("libcudart", WslSetupCommands.ToolProbeCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void WslDetectionTreatsRemovedWslAsInstallableAndStaysBounded()
    {
        const string removedWsl = "The Windows Subsystem for Linux is not installed. You can install by running 'wsl.exe --install'.";
        const string noDistros = "Windows Subsystem for Linux has no installed distributions.";
        var source = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "WslEnvironmentService.cs"));
        var mainWindow = ReadMainWindowSources();

        Assert.True(WslEnvironmentService.LooksLikeWslNotInstalled(removedWsl));
        Assert.False(WslEnvironmentService.LooksLikeWslNotInstalled(noDistros));
        Assert.Contains("Status: \"WSL not installed\"", source, StringComparison.Ordinal);
        Assert.Contains("WslExeFound: false", source, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(4)", source, StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll(statusTask, listTask)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TimeSpan.FromSeconds(15));", source, StringComparison.Ordinal);
        Assert.DoesNotContain("await AutoSelectDetectedWslDistroAsync();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("RunBackground(AutoSelectDetectedWslDistroAsync", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Environment.WslPageWorkflow.DetectRecommendedDistroAsync(_settings)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_detectWsl(cancellationToken)", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "WslPageWorkflowService.cs")), StringComparison.Ordinal);
        Assert.Contains("_environmentPageSnapshots.TryStartWslAutoRefresh()", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslLinuxAutoRefreshDone", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsAndWslToolTabsReuseCachedDetectionOnReturn()
    {
        var mainWindow = ReadMainWindowSources();
        var windowsPageFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "WindowsPageFactory.cs"));
        var windowsPageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "WindowsPageState.cs"));
        var wslPageFactory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "WslPageFactory.cs"));
        var wslPageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "WslPageState.cs"));
        var toolSetupApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "Environment", "ToolSetupApplicationService.cs"));

        var snapshotCache = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "EnvironmentPageSnapshotCache.cs"));

        Assert.Contains("private readonly EnvironmentPageSnapshotCache _environmentPageSnapshots;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly WindowsPageState _windowsPage;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("private readonly WslPageState _wslPage;", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_environmentPageSnapshots = uiState.EnvironmentPageSnapshots", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_windowsPage = uiState.WindowsPage", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_wslPage = uiState.WslPage", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly EnvironmentPageSnapshotCache _environmentPageSnapshots = new();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("WindowsPageFactory.Create(new WindowsPageRequest(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_windowsPage.Apply(page);", mainWindow, StringComparison.Ordinal);
        Assert.Contains("WslPageFactory.Create(new WslPageRequest(", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_wslPage.Apply(page);", mainWindow, StringComparison.Ordinal);
        Assert.Contains("Native Windows tools", windowsPageFactory, StringComparison.Ordinal);
        Assert.Contains("public sealed class WindowsPageState", windowsPageState, StringComparison.Ordinal);
        Assert.Contains("public IEnumerable<WpfButton?> HelpButtons", windowsPageState, StringComparison.Ordinal);
        Assert.Contains("public Grid? StatusMetric", windowsPageState, StringComparison.Ordinal);
        Assert.Contains("public DataGrid? ToolsGrid", windowsPageState, StringComparison.Ordinal);
        Assert.Contains("Installed Linux distros", wslPageFactory, StringComparison.Ordinal);
        Assert.Contains("public sealed class WslPageState", wslPageState, StringComparison.Ordinal);
        Assert.Contains("public IEnumerable<WpfButton?> HelpButtons", wslPageState, StringComparison.Ordinal);
        Assert.Contains("public UiRow? SelectedDistroRow", wslPageState, StringComparison.Ordinal);
        Assert.Contains("public void ApplyActionState(WslEnvironmentReport report, bool hasUbuntu, WslToolSnapshot tools)", wslPageState, StringComparison.Ordinal);
        Assert.Contains("_environmentPageSnapshots.TryGetWindowsTools(out var cachedWindowsTools)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("PopulateWindowsPage(cachedWindowsTools);", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_environmentPageSnapshots.StoreWindowsTools,", mainWindow, StringComparison.Ordinal);
        Assert.Contains("actions.StoreTools(tools);", toolSetupApplication, StringComparison.Ordinal);
        Assert.Contains("actions.PopulatePage(tools);", toolSetupApplication, StringComparison.Ordinal);
        Assert.Contains("_environmentPageSnapshots.TryGetWslTools(out var cachedReport, out var cachedTools)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("PopulateWslLinuxPage(cachedReport, cachedTools);", mainWindow, StringComparison.Ordinal);
        Assert.Contains("_environmentPageSnapshots.StoreWslTools(refresh.Report, refresh.Tools);", mainWindow, StringComparison.Ordinal);
        Assert.Contains("TryStartWindowsAutoRefresh", snapshotCache, StringComparison.Ordinal);
        Assert.Contains("TryStartWslAutoRefresh", snapshotCache, StringComparison.Ordinal);
        Assert.DoesNotContain("_cachedWindowsTools", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_cachedWslReport", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_cachedWslTools", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_windowsStatusMetric", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_windowsToolsGrid", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_windowsInstallCpuToolsButton", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_windowsInstallSyclToolsButton", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslDistroGrid", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslStatusMetric", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslInstallButton", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("_wslInstallSyclOneApiButton", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentPageSnapshotCacheOwnsOneTimeRefreshAndCachedTools()
    {
        var cache = new EnvironmentPageSnapshotCache();
        var windows = WindowsTools();
        var wslReport = ReadyWslReport();
        var wslTools = WslTools();

        Assert.True(cache.TryStartWindowsAutoRefresh());
        Assert.False(cache.TryStartWindowsAutoRefresh());
        Assert.False(cache.TryGetWindowsTools(out _));

        cache.StoreWindowsTools(windows);

        Assert.True(cache.TryGetWindowsTools(out var cachedWindows));
        Assert.Same(windows, cachedWindows);

        Assert.True(cache.TryStartWslAutoRefresh());
        Assert.False(cache.TryStartWslAutoRefresh());
        Assert.False(cache.TryGetWslTools(out _, out _));

        cache.StoreWslTools(wslReport, wslTools);

        Assert.True(cache.TryGetWslTools(out var cachedReport, out var cachedWslTools));
        Assert.Same(wslReport, cachedReport);
        Assert.Same(wslTools, cachedWslTools);
    }

    private static WindowsToolSnapshot WindowsTools() => new(
        GitInstalled: true,
        GitPath: "git.exe",
        CMakeInstalled: true,
        CMakePath: "cmake.exe",
        MsvcInstalled: true,
        MsvcDetails: "MSVC ready",
        NvidiaDriverVisible: false,
        NvidiaSmiPath: "",
        CudaToolsInstalled: true,
        CudaDetails: "CUDA ready",
        VulkanToolsInstalled: true,
        VulkanDetails: "Vulkan ready",
        SyclToolsInstalled: true,
        SyclDetails: "SYCL ready",
        IntelGpuVisible: true);

    private static WslEnvironmentReport ReadyWslReport() => new(
        WslExeFound: true,
        WslWorking: true,
        Status: "WSL ready",
        Details: "",
        DefaultDistro: "Ubuntu-24.04",
        RecommendedDistro: "Ubuntu-24.04",
        RecommendedAction: "",
        Distros: [new WslDistroInfo("Ubuntu-24.04", "Running", "2", IsDefault: true, IsUbuntu: true)]);

    private static WslToolSnapshot WslTools() => new(
        CpuToolsInstalled: true,
        CudaToolsInstalled: true,
        VulkanToolsInstalled: true,
        CpuSummary: "CPU ready",
        CudaSummary: "CUDA ready",
        VulkanSummary: "Vulkan ready",
        SyclToolsInstalled: true,
        SyclSummary: "SYCL ready");

}
