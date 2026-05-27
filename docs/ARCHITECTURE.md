# Target Architecture

## Boundary

The release target is Windows-first and self-contained for the UI, with llama.cpp running either as a native Windows `llama-server.exe` or inside Ubuntu/WSL. The repo owns code and process control:

- .NET 8 WPF desktop shell
- single app instance per Windows user session
- Local app service with per-session auth token
- serialized SQLite state store
- hidden process supervisor for native Windows or Ubuntu/WSL `llama-server`
- local-only model serving by default, with an API key required for all model access and explicit LAN model-serving opt-in
- hidden runtime-package/source-build/download jobs
- Windows and WSL/Linux environment detectors and setup launchers
- GitHub release update checker with staged portable-exe install
- PowerShell build script only when the user starts a build
- App-owned cache and temporary staging folders

The repo does not own large data by default:

- GGUF models
- downloaded/extracted llama.cpp builds
- OpenCode config

The startup workspace is fixed for the process and defaults to `data` beside `LlamaCppWindowsManager.exe` when that location is writable. If not, it falls back to `%LocalAppData%\llama.cpp Windows Manager`, while reusing `%LocalAppData%\llama.cpp Console` or `%LocalAppData%\LocalLlmConsole` only when those legacy folders already exist. It can be overridden with `LLAMA_CPP_WINDOWS_MANAGER_WORKSPACE` before launch; `LLAMA_CPP_CONSOLE_WORKSPACE` and `LOCAL_LLM_CONSOLE_WORKSPACE` remain accepted as legacy aliases. Models and runtimes are configured in App Settings and stored in SQLite. Cache data is kept inside the fixed workspace and is not exposed as a separate Settings folder. The source tree now contains only the WPF app, tests, docs, and the helper script that is embedded in the exe and extracted on demand to `data\tools` for llama.cpp builds.

## Runtime Shape

```mermaid
flowchart LR
  UI[".NET 8 WPF Shell"] --> API["Local App Service :8090"]
  API --> DB["SQLite State Store"]
  UI --> Jobs["Hidden Job Engine"]
  UI --> Supervisor["Hidden Process Supervisor"]
  Supervisor --> RuntimeA["Windows or Ubuntu/WSL llama-server :model port"]
  Supervisor --> RuntimeB["Additional loaded model sessions"]
  RuntimeA -. "optional LAN binding with required API key" .-> LAN["LAN OpenAI-compatible clients"]
  RuntimeB -. "optional LAN binding with required API key" .-> LAN
  Jobs --> HF["Hugging Face Downloads"]
  Jobs --> Build["Hidden llama.cpp Build"]
  UI --> Cache["Workspace Cache Folder"]
  UI --> Models["Models Folder"]
  UI --> Updates["GitHub Releases"]
```

## State And Recovery

Current:

1. SQLite operations are serialized inside `StateStore` so UI timers, downloads, and localhost API reads do not share the connection concurrently.
2. Schema migrations are applied idempotently and recorded in the `migrations` table.
3. Settings saves are transactional.
4. Bad settings rows are backed up under `state\corrupt-settings` and replaced with defaults.
5. Corrupt database files are quarantined under `state\corrupt-database-*` and recreated on startup.
6. Startup keeps the workspace root immutable for the running process.
7. Completed app updates write a pending notice under the workspace cache so the relaunched app can show release notes and then delete the notice.

## Current Service Boundaries

The WPF window owns page composition and user interaction, while reusable behavior is split into services. The window code-behind is split by workflow partials so startup/shutdown stays in `MainWindow.xaml.cs`, persistent control fields stay in `MainWindow.State.cs`, navigation/chrome/shared helpers live in dedicated partials, and feature work is isolated into page/workflow files such as `MainWindow.FolderSettings.cs`, `MainWindow.GridHelpers.cs`, `MainWindow.GridColumnSizing.cs`, `MainWindow.ModelRows.cs`, `MainWindow.ModelDownloads.cs`, `MainWindow.DownloadHistory.cs`, `MainWindow.Wsl.cs`, `MainWindow.OpenCode.cs`, `MainWindow.LaunchSettings.cs`, `MainWindow.LaunchSettingsCapabilities.cs`, `MainWindow.LaunchSettingsRuntimeSelection.cs`, `MainWindow.ModelRuntime.cs`, `MainWindow.ModelRuntimeLifecycle.cs`, `MainWindow.ModelRuntimePrerequisites.cs`, `MainWindow.RuntimeDashboard.cs`, `MainWindow.RuntimeMetrics.cs`, `MainWindow.RuntimeMetricCounters.cs`, `MainWindow.RuntimeBuilds.cs`, `MainWindow.RuntimeSourceDownloads.cs`, `MainWindow.RuntimeBuildJobs.cs`, and `MainWindow.RuntimeJobControls.cs`.

- `MainWindowViewModel` and page view models (`OverviewPageViewModel`, `ModelsPageViewModel`, `RuntimesPageViewModel`, `RuntimePackagesPageViewModel`, `RuntimeBuildsPageViewModel`, `RuntimeMetricsViewModel`, `WindowsPageViewModel`, `WslLinuxPageViewModel`, `HuggingFacePageViewModel`, `JobsViewModel`, `LogsViewModel`, `SettingsPageViewModel`, `OpenCodePageViewModel`, `LaunchSettingsViewModel`, `UpdatesPageViewModel`, and `LifetimeMetricsViewModel`) own row collections, selection lists, status/busy state, and deterministic row projection for migrated pages.
- `StateStore`, `JobEngine`, and `SecretProtector` own durable state, jobs, and protected settings.
- `ModelCatalogService`, `HuggingFaceService`, `HuggingFaceInstallStateService`, `HuggingFaceLaunchSettingsSuggester`, and `ModelCapabilityService` own model discovery, download lifecycle, matching mmproj/projector companion downloads, installed/download button state, README launch hints, and local model capability inference. Hugging Face launch suggestion parsing is split across config JSON parsing, README command extraction, shell tokenization, and option mapping.
- `RuntimeRegistryService`, `RuntimeAdapter`, `RuntimePackageCatalogService`, `RuntimeBuildCatalogService`, `RuntimeBuildJobService`, `RuntimeBuildToolService`, `RuntimeMetadataService`, `RuntimeEquivalenceService`, `RuntimeFileService`, `RuntimePortAllocator`, `ModelPortAllocator`, and `RuntimeEndpointService` own runtime discovery, launch validation, official prebuilt package selection/extraction, source/build catalog metadata and remote-ref parsing, build job payload/log metadata, build-tool command construction, source/prebuilt equivalence, safe delete boundaries, model-server URLs, stable per-model ports, and served-model matching.
- `LlamaProcessSupervisor`, `TrackedProcessRunner`, `WindowsEnvironmentService`, `WindowsSetupCommands`, `WslEnvironmentService`, `WslSetupCommands`, and `CommandLineService` own process supervision, tracked process execution, Windows and WSL detection/status/tool-probe parsing, setup/probe commands, and visible shell command quoting/launching.
- `RuntimeMetrics`, `RuntimeDashboardService`, `GpuStatusService`, `LogFileService`, `FileSystemSafetyService`, `ConfigFileSafetyService`, `VramAdmissionService`, and `CacheMaintenanceService` own metrics parsing, live runtime dashboard math, NVIDIA and Intel Arc GPU summaries, log previews/classification/redaction/deletion planning, shared filesystem guardrails, backup-before-write config safety, conservative multi-model VRAM admission, and cache clearing safety.
- `AppPreferenceService`, `DisplayFormatService`, `LaunchSettingMetadataService`, `LoadedModelSessionManager`, `ActiveRuntimeSessionStore`, and `AppUpdateService` own settings option normalization, shared UI value formatting, launch-setting option/help/suggestion text, in-memory loaded-session state, running-runtime recovery state, and GitHub release updates.

The largest service classes are also split by concern: `StateStore` separates catalog, settings, and job persistence; `HuggingFaceService` separates search, download lifecycle, safety verification, projector companion handling, and launch-profile suggestions; `OpenCodeConfigService` separates model/provider edits, agents, core JSON file handling, model envelopes, provider enablement, and path discovery; `LlamaProcessSupervisor` separates runtime lifecycle, launch helpers, and WSL cleanup helpers; and `ModelCatalogService` keeps legacy metadata parsing separate from normal scan/import/delete flows.

Domain models are grouped by use instead of living in one catch-all file: core records/enums, app defaults, per-model launch settings, and runtime/download launch payloads each have dedicated model files. MainWindow background refreshes and monitors go through a shared `RunBackground` wrapper so failures are logged and surfaced in the status line instead of becoming unobserved tasks.

## App Update Lifecycle

Current:

1. The Updates navigation item sits below Logs and defaults to **Check For Updates**.
2. Startup checks the configured GitHub release feed in the background. When a newer release is found, the nav item changes to **Install Update**.
3. Manual checks show either a no-updates popup or an install confirmation.
4. Install downloads the release asset into `cache\app-updates`, extracts the portable exe when the asset is a zip, starts a hidden PowerShell handoff script, closes the app, replaces `LlamaCppWindowsManager.exe`, and restarts it.
5. A matching SHA-256 companion asset is required and verified before extraction.
6. If the installed app is signed, the staged update executable must be signed by the same certificate before replacement.
7. The relaunched app shows the GitHub release name and notes from the installed update.

Still needed:

1. Sign release assets before uploading them to GitHub.
2. Publish SHA-256 companion assets for update packages.

## Model Lifecycle

Current:

1. Choose a models folder or scan it on demand.
2. Auto-register missing GGUF model folders in SQLite.
3. Pick an official prebuilt or custom built llama.cpp runtime and launch settings.
4. Load/restart/unload explicitly; more than one model can stay loaded at the same time when each model has a unique saved port and hardware capacity allows it.
5. Search Hugging Face from the Models page, paste a Hugging Face repo or GGUF file URL directly, review compatibility signals, open the selected repo's model card, and download/install the selected GGUF plus a discoverable verified mmproj/projector companion as a background job.
6. Delete registration or app-owned model directory according to ownership flags.
7. Generate compact model manifests from readable GGUF metadata while preserving imported/download metadata.
8. Verify expected byte counts or SHA-256 before registering downloaded GGUF files.
9. Validate local vision/projector pairing by surfacing missing mmproj files in capability summaries, invalidating cached capabilities when a projector is added or removed, blocking explicit vision launches without a projector, and carrying per-model dynamic-resolution image token allowances through to `llama-server`.
10. Keep model serving local-only unless Settings explicitly enables LAN access. All launches require an API key; LAN mode exposes the llama.cpp model server, not the app-local control API.

Still needed:

1. Add richer rollback controls for installed runtime builds.

## llama.cpp Runtime Lifecycle

Current:

1. Install official prebuilt llama.cpp runtime packages from Runtime Downloads first. Current presets cover CUDA Windows, CUDA WSL, Vulkan Windows, Vulkan WSL, Intel Arc SYCL Windows, Intel Arc SYCL WSL, CPU Windows, and CPU WSL when upstream publishes matching assets.
2. Scan configured runtime roots and register folders containing `llama-server` or `llama-server.exe`.
3. Select a runtime per model and save a stable per-model port next to that runtime in model launch settings.
4. Unregister unused runtimes; runtime file deletion is disabled when a runtime is active or referenced by saved model launch settings.
5. Reconcile official source-built and prebuilt runtimes by runtime fingerprint when their binaries match.
6. Build CPU, CUDA, Vulkan, or SYCL llama.cpp for native Windows or Ubuntu/WSL as a hidden advanced background job when a custom fork, patch, branch, or missing package target requires source.
7. Delete downloaded source/build folders only when bounded inside the configured runtimes folder; successful downloaded-source builds clean up the source folder by default, with a Settings toggle to keep it.
8. Cancel active runtime build jobs, retry failed/cancelled/interrupted runtime build jobs, clear finished runtime build job records/logs, and show latest build-log progress in the job summary.
9. Detect installed WSL distros from the WSL Linux page, ignoring Docker-managed WSL distros.
10. Select the Ubuntu distro used for WSL launches/builds.
11. Open visible setup commands for Windows CPU/CUDA/Vulkan/Intel oneAPI tools, WSL install, WSL update, Ubuntu install, Ubuntu CPU build-tool install, Ubuntu CUDA Toolkit install, Ubuntu Vulkan tool install, Ubuntu Intel GPU runtime install, Ubuntu Intel oneAPI install, and Ubuntu package update checks.
12. Install CPU build dependencies inside Ubuntu (`git`, `cmake`, compiler tools, pkg-config, libcurl headers, ccache, Ninja) on request.
13. Treat CUDA as a separate WSL setup action, installing NVIDIA's WSL CUDA Toolkit on request and checking for CUDA Toolkit before starting a CUDA CMake build.
14. Treat Vulkan as a separate setup action, installing the Ubuntu Vulkan packages needed by official llama.cpp builds (`libvulkan-dev`, `glslc`, `spirv-headers`, `vulkan-tools`, `mesa-vulkan-drivers`) and checking `vulkaninfo --summary` before starting a Vulkan CMake build.
15. Treat Intel Arc SYCL as a separate setup action, checking Windows oneAPI tools for native launches/builds and Ubuntu Level Zero/OpenCL runtime plus oneAPI DPC++/MKL/DNNL tools for WSL launches/builds.
16. Detect Windows CPU/CUDA/Vulkan/SYCL build tool presence from the Windows page and WSL CPU/CUDA/Vulkan/SYCL build tool presence from the WSL Linux page.
17. Keep Windows and WSL runtime presets distinct so package downloads, source downloads, update checks, build jobs, retries, and delete-all actions do not mix native and WSL artifacts.

Still needed:

1. Broaden runtime compatibility badges beyond the current build-prerequisite checks.
2. Add richer rollback controls for installed runtime packages/builds.

## Suggested Modules

- `SettingsStore`: typed path settings, validation, migration.
- `ModelCatalog`: scan/download/delete model registrations.
- `RuntimeRegistry`: scan/register/unregister/delete llama.cpp builds.
- `ProcessSupervisor`: start/stop/control cleanup for API/control/runtime/proxy.
- `Installer`: runtime package downloads, local archive fingerprinting, extraction, source builds, progress events.
- `Telemetry`: runtime metrics, logs, GPU state.
- `UiState`: WPF view models so UI updates do not reset selections.
