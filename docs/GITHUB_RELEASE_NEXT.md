# Next Release Notes Draft

This file tracks changes made after the published `v1.1.2` release so the next
GitHub release notes are easy to assemble.

## Unreleased Hardening

- Hardened runtime backend detection so loose folder names such as
  `cuda-backup` no longer cause a CPU runtime to be misclassified as CUDA.
  Explicit packaged backend metadata and nearby runtime library markers now
  take priority.
- Made loaded-model runtime state transitions thread-safe across process output,
  readiness checks, and exit events.
- Made log head previews BOM-aware, matching the tail preview behavior.
- Added GGUF metadata version validation so unsupported/future GGUF versions are
  ignored instead of being silently parsed.
- Bounded Hugging Face repository metadata caching with a 30-minute TTL and
  256-entry cap so long search sessions cannot grow `_repoInfoCache`
  indefinitely.
- Tightened legacy launch-default migration so current saved app/model launch
  settings that intentionally match old defaults are preserved, while
  legacy-shaped records still migrate to the newer defaults.
- Added native `llama-server` shutdown verification with a process-id fallback
  kill pass after the primary process-tree stop request.
- Added central `JobEngine` status transition validation against the persisted
  job row, preventing stale caller-side job records from making invalid durable
  status moves.
- Split runtime stdout/stderr observation into a tested helper covering
  redaction and loaded-line detection, and added recovered native-process
  shutdown coverage.
- Added `test-release-gate.ps1` as a single local wrapper for the build, tests,
  formatting, whitespace, and vulnerability checks used before release, with
  optional publish/installer packaging switches for release machines.
- Extended the release gate's optional publish/installer smoke steps to verify
  generated artifact checksums, expected executable aliases, PDB exclusion, and
  archive/installer companion files.
- Hardened signed installer packaging so `build-installer.ps1 -SkipPublish
  -RequireSigned` refuses to compile an installer around an unsigned published
  executable.
- Added Git/editor line-ending rules for PowerShell scripts, Inno Setup
  sources, and `.gitattributes` so `git diff --check` stays quiet under
  `core.autocrlf=true`.
- Hardened `publish-app.ps1` cleanup so existing `dist` publish folders and
  archives are removed only through bounded, non-reparse-point paths.

## Unreleased Product Updates

- Added a scoped LAN exposure setting with four modes: Local only, Gateway LAN
  only, Direct models LAN only, and Gateway + direct LAN. Existing `lan` /
  `LAN access` settings migrate to Gateway + direct LAN for compatibility.
- Added an auto-load gateway routing status row to Overview's Loaded Model
  Sessions grid, showing the shared endpoint, policy, LAN exposure, and loaded
  direct-session count.
- Improved auto-load gateway failure and VRAM admission messages for
  keep-loaded mode, including guidance to switch to Single active model, unload
  a model, or reduce GPU/context settings.
- Added Settings > OpenCode > Sync on launch save so automatic OpenCode config
  rewrites after saved launch settings or variants change can be enabled or
  disabled.
- Added a single-button per-model Vision head selector with auto-detect,
  embedded/model-bundled, and explicit external projector choices, so companion
  files such as vision-head or MTP vision GGUFs can be linked intentionally
  instead of relying only on filename matching.
- Added a separate MTP head selector and `Spec type = mtp` launch path for
  compatible forks that use `--mtp-head`, keeping MTP assistant heads separate
  from Vision head / `--mmproj` projectors.
- Added Atomic TurboQuant CUDA Windows and WSL rows to Runtime Downloads. The
  Windows row resolves the published Atomic binary package, while the WSL row
  stays visible and reports not published until a matching Linux/WSL archive is
  available.
- OpenCode local model sync now carries vision support metadata when the saved
  launch settings prove that the model has embedded, detected, or explicit
  vision/projector support.
- Fresh installer setups offer Start with Windows checked by default, and the
  app exposes the same startup preference in Settings.
- Removed the explanatory Saved Model Variant row that appeared under selected
  variants in Models.
- Updated README, architecture docs, release-readiness docs, and in-app Help to
  match the current routing, OpenCode, LAN exposure, startup, and vision-head
  behavior.

## Verification

Last verified locally on 2026-05-31:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-app.ps1 -Restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-app.ps1
D:\LLM\.dotnet-sdk-8\dotnet.exe format LocalLlmConsole.sln --verify-no-changes --verbosity minimal
git diff --check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-vulnerabilities.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller
```

Result: Release app build succeeded with zero warnings, release-hardening tests
passed (`422/422`), formatting was clean, no vulnerable packages were found,
the diff had no whitespace errors, and publish/installer artifact checks passed
locally.
