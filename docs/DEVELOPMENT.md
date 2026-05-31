# Development Guide

This repo is a Windows-first .NET 8 WPF app. The app should stay easy to run
from source, but end users should receive the published portable app or
installer from `dist`.

## Local Gate

Run these before opening a release PR or after any architecture-level change:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1
```

That wrapper runs the same gate as the individual commands below:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-app.ps1 -Restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-app.ps1
dotnet format LocalLlmConsole.sln --verify-no-changes --no-restore --verbosity minimal
git diff --check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-vulnerabilities.ps1
```

To include packaging on a machine with publish/installer prerequisites, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller
```

Use `-RequireCleanTree` on `test-release-gate.ps1`, `publish-app.ps1`, or
`build-installer.ps1` when producing release artifacts that must come from a
clean Git worktree.

If `dotnet` is not on `PATH`, set `LLAMA_CPP_WINDOWS_MANAGER_DOTNET` to a .NET
8 SDK `dotnet.exe`.

## Module Layout

The durable rules live in `docs/ARCHITECTURE.md` under "Architecture
Contract". Treat that section as the source of truth when deciding whether a
change belongs in `MainWindow`, a page controller, an application service, a
workflow service, a domain service, or infrastructure.

Top-level `Services` files are reserved for composition/root wiring:

- `AppServiceFactory*.cs`
- `MainWindowServices.cs`
  - Defines infrastructure, core, and loaded service bundles by feature.
  - Keep new dependencies in the narrowest matching bundle rather than adding
    another top-level constructor parameter.

Implementation services live under feature modules:

| Folder | Ownership |
| --- | --- |
| `Services/App` | App settings, startup/shutdown, updates, logs, help, cache, and shared app workflows. |
| `Services/Environment` | Windows and WSL detection, setup command planning, and visible tool setup launchers. |
| `Services/Gateway` | Local model gateway host/runtime contracts and gateway activity state. |
| `Services/HuggingFace` | Hugging Face search, metadata, download safety, download history, and launch suggestions. |
| `Services/Infrastructure` | State store, local app service, process runner, filesystem/config safety, dialogs, jobs, formatting, and shell helpers. |
| `Services/Models` | Model catalog, model capabilities, aliases, model launch profiles, and model deletion/import behavior. |
| `Services/OpenCode` | OpenCode config discovery, provider/model/agent edits, gateway/direct local model sync, and vision-capability metadata. |
| `Services/Runtimes` | Runtime registry, packages, source/build jobs, launch validation, sessions, metrics, readiness, and process supervision. |

UI factories and page state live under:

- `Ui/Common`
- `Ui/Pages/<Feature>`

The current code keeps file-scoped namespaces stable. Namespace tightening can
happen module-by-module after behavior is settled.

## Service Naming

Use these names consistently:

- `WorkflowService`: owns a domain sequence or business workflow.
- `ApplicationService`: adapts a workflow to UI-facing actions and status.
- `Controller`: owns stateful UI coordination, timers, reentrancy, or lifecycle state.
- `Factory`: constructs controls or services.
- `State`: stores control references or page/session state without business rules.

Avoid adding a new service for a single pass-through method. Prefer extending an
existing feature service unless the new type owns a real decision, state, or
boundary.

## MainWindow Direction

`MainWindow` is the shell, navigation host, app lifetime coordinator, and event
broker. Keep feature behavior in services, workflow/application services, page
state, view models, or page controllers.

Use these rules when touching `MainWindow`:

- Persistent fields should be shell state, service bundles, loaded-service
  lifecycle holders, page state, or page-controller bundles.
- Raw WPF control references should stay grouped behind page state objects.
- Core services should be reached through named bundles such as
  `_coreServices.App`, `_coreServices.Ui`, `_coreServices.Models`,
  `_coreServices.Runtime`, `_coreServices.OpenCodeServices`,
  `_coreServices.HuggingFaceServices`, and `_coreServices.Environment`.
- Loaded services should be reached through `AppServices`, `ModelServices`,
  `GatewayServices`, and `RuntimeServices`. Do not add flat pass-through aliases
  to `MainWindowLoadedServices`.
- Page-specific row/event routing belongs in page controllers. Models, Hugging
  Face download history, Runtimes, Windows, WSL, OpenCode, Overview, Logs,
  Lifetime, and Settings pages already follow this pattern.
- Empty placeholder partials should be deleted.

## Test Guidance

Prefer behavior tests over source-shape tests. Source-shape tests are acceptable
for architectural guardrails, but they should check durable boundaries, not
fragile line-by-line implementation details.

Useful test groups:

- `ReleaseHardening.Architecture.Tests.cs`: module layout guardrails.
- `ReleaseHardening.Runtime.Tests.cs`: runtime/session/metrics/build behavior.
- `ReleaseHardening.HuggingFace.Tests.cs`: search/download/safety behavior.
- `ReleaseHardening.Ui.Tests.cs`: view model and UI composition invariants.

## Documentation Guidance

When behavior changes, update both the repo docs and in-app Help in the same
pass:

- `README.md` for the public feature overview, quick start, safety defaults,
  and distribution behavior.
- `docs/ARCHITECTURE.md` for module ownership, serving topology, and durable
  architectural guardrails.
- `docs/RELEASE_READINESS.md` for manual validation steps and latest verified
  command results.
- `docs/GITHUB_RELEASE_NEXT.md` for unreleased user-visible changes.
- `src/LocalLlmConsole.App/Ui/Pages/Help/HelpContentFactory.cs` for user-facing
  Help text shown in the app.

Prefer describing current behavior over refactor history. Historical release
notes should stay historically accurate and point to the next-release notes for
newer behavior.

## Generated Output

Generated output is ignored and should stay out of commits:

- `bin`
- `obj`
- `dist`
- `TestResults`
- local `data`, `models`, `runtimes`, `cache`, `state`, and `logs`
