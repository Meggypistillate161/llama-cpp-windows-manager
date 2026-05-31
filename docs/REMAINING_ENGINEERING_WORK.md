# Remaining Engineering Work

Last updated: 2026-06-01

## Immediate Internal Cleanup

These are codebase quality items that can continue locally without external input:

- Keep the source tree free of generated build output by using `clean-repo.ps1`; `dist` stays ignored and should contain only the current release artifact unless `-AllDist` is requested.
- Treat the MainWindow shell cleanup as complete; future behavior work should
  preserve the existing shell/service-bundle/page-controller boundaries.
- Keep page-specific event routing in page controllers; Models, Hugging Face
  download history, Runtimes, Windows, WSL, OpenCode, Overview, Logs, Lifetime,
  and Settings action wiring now follow this pattern.
- Continue tightening async/UI reliability:
  - keep fire-and-forget work behind `RunBackground`;
  - keep event-handler awaits behind `RunEventAsync`;
  - add cancellation paths where background work can outlive the current page.
- Keep architectural guard tests current as files are split or moved.

## Service-Level Cleanup

- Add integration-style `LlamaProcessSupervisor` tests around WSL kill fallback when a suitable test harness is available.
- Add more Hugging Face launch-suggestion behavior tests only when new option mappings are introduced.

## Bug-Hardening Follow-Ups

These came from the May 26 external bug report triage. The scary Hugging Face
download timeout and range-resume corruption claims were reviewed against the
current code and are not being tracked as release blockers. A later post-v1.1.2
triage also closed the actionable low-risk items around runtime backend
inference, runtime state transitions, log-head encoding, and GGUF version
validation; see `docs/GITHUB_RELEASE_NEXT.md` for the next-release note.

## Product/Workflow Work Still Open

These are broader product items already implied by the release docs:

- Continue clean Windows VM validation for a published app with no repo checkout.
- Add trusted signing and broaden installer smoke testing on clean Windows VMs.
- Keep the GitHub update feed and release asset naming stable for future releases.
- Hardware matrix validation across missing WSL, CPU build tools, CUDA-visible WSL, Vulkan-visible WSL, Intel Arc/SYCL-visible Windows and WSL, and unsupported backends.
- Benchmark WSL runtime source/build staging on `/mnt/<drive>` versus the distro's Linux filesystem; if Linux-side staging wins, preserve Windows-side metadata/cleanup semantics while copying only final runtime artifacts back to the workspace.
- Prefer upstream runtime archive checksums/signatures when llama.cpp publishes them; current runtime packages are downloaded from official GitHub releases and locally fingerprinted after extraction.
- Before publishing the next artifact, run a final clean-machine smoke pass for
  the new gateway/direct LAN exposure modes, Start with Windows installer task,
  OpenCode auto-sync toggle, and auto/embedded/explicit Vision head selection.

## Completed This Pass

- Added a Windows CI workflow, pinned SDK `global.json`, editor defaults, and a solution file.
- Added idempotent SQLite schema migration recording through a dedicated `StateStore.Migrations` partial.
- Added update SHA-256 companion asset verification and same-certificate update signature continuity for signed installs.
- Replaced the central model/runtime/runtime-repository `UiRow` projections with typed row view models.
- Split release-hardening tests into domain partials and added CI, migration, and update-checksum coverage.
- Split runtime source download/update/delete flow into `MainWindow.RuntimeSourceDownloads.cs`.
- Split runtime source build/delete/job tracking flow into `MainWindow.RuntimeBuildJobs.cs`.
- Kept `MainWindow.RuntimeBuilds.cs` focused on timer hooks and row event routing.
- Split launch setting capability visibility into `MainWindow.LaunchSettingsCapabilities.cs`.
- Split runtime choice and model action state into `MainWindow.LaunchSettingsRuntimeSelection.cs`.
- Kept `MainWindow.LaunchSettings.cs` focused on profile render/save/read/apply behavior.
- Split folder selection and app setting persistence into `MainWindow.FolderSettings.cs`.
- Split WSL/port launch preflight into `MainWindow.ModelRuntimePrerequisites.cs`.
- Split model runtime start/stop/loading/readiness lifecycle into `MainWindow.ModelRuntimeLifecycle.cs`.
- Moved runtime stop/switch follow-up application into `RuntimeSessionFollowupApplicationService`.
- Kept `MainWindow.ModelRuntime.cs` focused on load/unload command entry points.
- Split per-grid column sizing into `MainWindow.GridColumnSizing.cs`, leaving `MainWindow.GridHelpers.cs` focused on generic grid construction and button columns.
- Split registered model row actions into `MainWindow.ModelRows.cs`.
- Split Hugging Face download history/resume/pause/stop/timer flow into `MainWindow.DownloadHistory.cs`.
- Moved download monitor completion polling into `DownloadHistoryWorkflowService`.
- Kept `MainWindow.ModelDownloads.cs` focused on Hugging Face search and starting downloads.
- Split runtime metric counter, lifetime-token, and idle-unload bookkeeping into `MainWindow.RuntimeMetricCounters.cs`.
- Moved model runtime start follow-up plan application into `ModelRuntimeStartFollowupApplicationService`.
- Moved runtime dashboard refresh admission, refresh reentrancy, and pollable-session selection into `RuntimeDashboardRefreshCoordinator`.
- Moved idle auto-unload policy reentrancy and idle-session selection into `RuntimeIdleUnloadPolicyService`.
- Moved runtime readiness polling/completion composition into `RuntimeReadinessMonitorWorkflowService`.
- Moved runtime readiness completion plan application into `RuntimeReadinessCompletionApplicationService`.
- Moved model-grid and loaded-session selection suppression into `SelectionReentrancyCoordinator`.
- Moved transient model loading/loaded status timer ownership into `ModelRuntimeStatusController`.
- Moved gateway auto-load activity status timer ownership into `GatewayActivityStatusController`.
- Moved download-history and runtime-dashboard refresh timer ownership into `UiAsyncRefreshTimerController`.
- Moved page busy/restore state into `UiBusyStateController`.
- Moved tray/minimize window state decisions into `TrayWindowStateController`.
- Moved help section catalog and active-section state into `HelpSectionService`.
- Moved launch-settings programmatic form-update suppression into `LaunchSettingsEditorSession`.
- Moved selected launch-settings render ordering and stale-selection handling into `LaunchSettingsRenderApplicationService`.
- Moved launch profile/default save result application into `ModelLaunchSettingsSaveApplicationService`.
- Moved launch variant save result follow-up sequencing into `ModelLaunchVariantSaveApplicationService`.
- Moved advanced launch/runtime visibility state into `AdvancedSectionStateController`.
- Moved shutdown close-admission and cleanup-complete state into `AppShutdownStateController`.
- Moved selected model capability display and vision-control state into `SelectedModelCapabilityController`.
- Moved Lifetime token-usage layout into `LifetimePageFactory` and grouped its grid reference behind `LifetimePageState`.
- Grouped launch-settings panel control references behind `LaunchSettingsPanelState`.
- Grouped Logs page grid selection and preview references behind `LogsPageState`.
- Grouped Models page folder, model-grid, and Hugging Face grid references behind `ModelsPageState`.
- Removed vestigial model action-button fields and the no-op `UpdateModelActionButtons` path from `MainWindow`.
- Grouped OpenCode page control references behind `OpenCodePageState`.
- Moved mutable OpenCode file-set ownership behind `OpenCodeFileSetState`.
- Grouped OpenCode config/sync/page/local/settings service references behind `OpenCodeWorkflowServiceState`.
- Moved runtime catalog autoscan and update-state bookkeeping behind `RuntimeCatalogSessionState`.
- Grouped Runtimes page folder, runtime/package/build/job grid, advanced toggle, and CUDA preference references behind `RuntimesPageState`.
- Grouped runtime dashboard metric, log, and metrics-grid references behind `RuntimeDashboardPageState`.
- Grouped Settings page theme selection and settings-grid references behind `SettingsPageState`.
- Grouped Overview selector, gateway-status, and loaded-session grid references behind `OverviewPageState`.
- Grouped Windows setup page metric, grid, and action-button references behind `WindowsPageState`.
- Grouped WSL setup page metric, distro-grid, and setup-action references behind `WslPageState`.
- Changed `LocalAppService` to track in-flight request handlers, observe handler completion, and wait briefly for active handlers during shutdown.
- Grouped service implementation files under feature folders (`Services/App`,
  `Services/Environment`, `Services/Gateway`, `Services/HuggingFace`,
  `Services/Infrastructure`, `Services/Models`, `Services/OpenCode`, and
  `Services/Runtimes`) while keeping root service files limited to composition
  wiring.
- Grouped UI factories and page state under `Ui/Common` and `Ui/Pages/*`.
- Added architecture guard tests to keep service and UI implementation files in
  those module folders.
- Grouped page action controllers behind `MainWindowPageControllers` so the
  shell keeps one page-controller dependency instead of one field per page.
- Removed duplicated `MainWindow` fields for Environment, OpenCode, Hugging Face,
  Help, and small App core services; call sites now read those dependencies from
  the existing `MainWindowCoreServices` feature bundles.
- Removed duplicated `MainWindow` fields for Model and Runtime core services;
  call sites now use the model/runtime feature bundles directly.
- Removed duplicated `MainWindow` fields and flat `MainWindowCoreServices`
  aliases for UI controllers; call sites now use the UI feature bundle directly.
- Removed duplicated `MainWindow` fields for App core services; call sites now
  use the App feature bundle directly.
- Removed the remaining flat `MainWindowCoreServices` pass-through facade so
  core service ownership flows through the named feature bundles.
- Removed the flat `MainWindowLoadedServices` pass-through facade so loaded
  service ownership also flows through the App/Models/Gateway/Runtime bundles.
- Removed constructor-copy infrastructure fields for process runner and HTTP
  clients; shutdown now uses the infrastructure bundle directly.
- Removed the empty `MainWindow.MetricInlines.cs` partial.
- Split OpenCode provider enablement helpers into `OpenCodeConfigService.Providers.cs`.
- Split OpenCode model envelope/export/import helpers into `OpenCodeConfigService.ModelEnvelopes.cs`.
- Kept `OpenCodeConfigService.Json.cs` focused on config file JSON I/O and low-level object helpers.
- Split `LlamaProcessSupervisor` WSL cleanup/path helpers into `LlamaProcessSupervisor.Wsl.cs`.
- Moved supervisor launch helper logic into `LlamaProcessSupervisor.Launch.cs`.
- Reused `LogFileService.RedactSensitiveText` for runtime log redaction instead of duplicating secret redaction regexes.
- Split Hugging Face launch suggestion config parsing into `HuggingFaceLaunchSettingsSuggester.Config.cs`.
- Split README command extraction into `HuggingFaceLaunchSettingsSuggester.CommandExtraction.cs`.
- Split shell tokenization into `HuggingFaceLaunchSettingsSuggester.ShellParsing.cs`.
- Added `ConfigFileSafetyService` for shared backup-before-write/delete and reparse-point rejection.
- Updated OpenCode config and markdown agent writes/deletes to use the shared config safety helper.
- Extracted runtime build action/path/message planning into `RuntimeBuildJobService.CreatePlan`.
- Added `LlamaProcessSupervisor` attach/load/stop state-transition coverage.
- Added Hugging Face launch-suggestion coverage for inline option values, quoted draft paths, config JSON, out-of-range context config, and llama-cli fallback.
- Added direct Hugging Face repo id and GGUF file URL parsing to search, including `huggingface.co` and `hf.co` URLs.
- Added safe Hugging Face model-card actions for selected search and download-history rows.
- Added compact GGUF-derived model manifest enrichment during downloaded/external model registration.
- Added Hugging Face search compatibility signals, including vision/mmproj, reasoning, MoE, config/tokenizer, and license indicators.
- Fixed model capability cache invalidation for added/removed mmproj projector files and covered explicit vision-launch validation.
- Added Hugging Face mmproj/projector companion metadata, safe validation, automatic verified companion download when available, and projector-specific service split.
- Added per-model dynamic-resolution vision image token allowances and llama-server `--image-min-tokens` / `--image-max-tokens` launch mapping.
- Added Ubuntu/WSL Vulkan runtime build presets, WSL setup/probe/preflight workflow, and backend plumbing through catalog rows, custom repositories, build commands, script flags, and runtime metadata inference.
- Added runtime build job Cancel and Retry actions with in-flight cancellation, WSL marker cleanup, retry payload parsing, and row-level enablement tests.
- Removed the redundant runtime build-job log-tail preview while keeping row-level log opening with safe workspace-log validation and redaction.
- Added running runtime build progress summaries from the latest meaningful build-log line.
- Added terminal runtime build-job cleanup with safe job-record deletion and workspace-log deletion.
- Tightened runtime build deletion so runtime files can only be deleted when the runtime is not active and not referenced by saved model launch settings.
- Added an Inno Setup installer source and build wrapper with preferred `D:\LlamaCppWindowsManager` installs, editable install location, launch-after-install, existing-install reuse, and uninstall data preservation by default.
- Added native Windows setup/tool detection, official and Atomic prebuilt runtime downloads, advanced source-build hiding, Windows/WSL CUDA/Vulkan/SYCL package presets, source/prebuilt runtime equivalence, multi-model loaded sessions, stable per-model ports, and OpenCode provider separation per model endpoint.
- Added Intel Arc/SYCL support for native Windows and WSL, including oneAPI setup/detection, WSL Intel GPU runtime packages, SYCL source-build arguments, SYCL launch environment, and Intel Arc runtime metric summaries.
- Made `LocalAppService` resilient to bounded unexpected listener errors while keeping request-handler tasks observed during shutdown.
- Added post-v1.1.2 hardening for runtime backend inference, `LlamaProcessSupervisor` state transitions, BOM-aware log-head previews, and unsupported GGUF metadata versions.
- Added `docs/DEVELOPMENT.md` with module ownership, local gate commands, and
  service naming guidance for contributors.
- Removed pass-through runtime/job lookup application services; callers now use
  the loaded `StateStore` directly for plain runtime and job list reads.
- Added Start with Windows settings and checked-by-default installer task for
  fresh installs.
- Added Settings > OpenCode > Sync on launch save to control automatic OpenCode
  local model rewrites after saved launch settings or variants change.
- Added auto-detected, embedded/model-bundled, and explicit per-model Vision
  head/projector selection plus OpenCode vision support metadata for launch
  profiles with usable vision support.
- Added separate per-model MTP head selection for compatible runtimes that
  launch MTP assistant heads with `--mtp-head` instead of `--mmproj`.
- Added scoped LAN exposure for local-only, gateway-only, direct-model-only, and
  gateway + direct serving surfaces.
- Added a router status row to Overview's Loaded Model Sessions grid with the
  shared gateway endpoint, routing policy, LAN exposure, and loaded direct
  session count.
- Improved gateway keep-loaded VRAM/admission failure messages and documented
  the gateway routing model in README, architecture docs, release-readiness
  docs, next-release notes, and in-app Help.
- Added a bounded 30-minute, 256-entry Hugging Face repository metadata cache
  so repeated searches reuse recent API details without unbounded growth across
  many unique repositories.
- Tightened legacy launch-default migration so old default-looking values are
  migrated only for legacy-shaped app/model launch setting records, while
  current explicit saves of the same values are preserved.
- Added native `llama-server` shutdown verification with a process-id fallback
  kill pass after the primary process-tree stop request, while keeping WSL
  marker cleanup separate.
- Added central `JobEngine` status transition validation against the persisted
  job row, so stale caller-side `JobRecord` instances cannot reopen completed
  jobs or make other invalid durable status moves.
- Split runtime stdout/stderr observation into `LlamaRuntimeOutputObserver`,
  covering central redaction and loaded-line detection directly, and added
  recovered native-process shutdown coverage through `LlamaProcessSupervisor`.
- Added `test-release-gate.ps1` as a single local wrapper for build, tests,
  formatting verification, whitespace checks, and vulnerability auditing, with
  optional publish/installer packaging switches for release machines.
- Extended the release gate's optional publish/installer smoke steps to verify
  generated artifact checksums, expected executable aliases, PDB exclusion, and
  archive/installer companion files.
- Hardened signed installer packaging so `build-installer.ps1 -SkipPublish
  -RequireSigned` verifies the reused published executable is signed before the
  installer is compiled.
- Added Git/editor line-ending rules for PowerShell scripts, Inno Setup
  sources, and `.gitattributes` so release gate whitespace checks stay quiet
  under `core.autocrlf=true`.
- Hardened `publish-app.ps1` cleanup so existing `dist` publish folders and
  archives are removed only through bounded, non-reparse-point paths.
- Hardened runtime package install and portable update extraction with archive
  path/link validation, runtime package byte-count and SHA-256 verification,
  bounded auto-load gateway request bodies, observable WSL runtime cleanup, and
  `-RequireCleanTree` release packaging checks.
- Updated Overview runtime metrics with compact normal/MTP token monitors,
  idle-safe live rates, a live Slots card, and normalized GPU metric separators.
