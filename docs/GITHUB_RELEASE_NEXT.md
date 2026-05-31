# GitHub Release v1.1.3 Draft

This file is the copy/paste source for the next GitHub release. It tracks
changes made after the published `v1.1.2` release.

## Copy/Paste Release Notes

### llama.cpp Windows Manager v1.1.3

v1.1.3 is an unsigned community release candidate focused on safer release
packaging, clearer model-serving controls, better OpenCode sync, and a more
useful live Overview dashboard.

#### Highlights

- Added scoped LAN exposure: Local only, Gateway LAN only, Direct models LAN
  only, or Gateway + direct LAN.
- Added an auto-load gateway row on Overview so the shared endpoint, policy,
  LAN exposure, and loaded direct-session count are visible beside model
  sessions.
- Added Settings > OpenCode > Sync on launch save, plus clearer API-key
  disclosure: the app protects its saved key with Windows user protection, while
  synced OpenCode provider config stores the key in plain text because OpenCode
  must read it.
- Added explicit Vision head choices and separate MTP head selection for
  compatible `--mtp-head` runtimes.
- Added Atomic TurboQuant CUDA Windows/WSL runtime package rows.
- Updated Overview metrics with compact normal and MTP token monitors, a live
  Slots card, idle-safe live token rates, and normalized GPU metric separators.

#### Safety And Hardening

- Runtime package downloads now fail closed without expected size and SHA-256
  verification metadata or companion checksum files.
- Runtime package archives and portable app update archives are prevalidated
  before extraction to reject traversal paths, absolute paths, and unsafe tar
  entries.
- Auto-load gateway request bodies are bounded and oversized payloads return
  `413 request_too_large`.
- Native and WSL runtime stop paths now verify targeted shutdown more
  carefully; WSL cleanup writes diagnostic warnings when verification fails.
- Release scripts support `-RequireCleanTree`, and CI now verifies formatting
  plus `git diff --check`.
- The release gate has optional publish/installer smoke checks for hashes,
  aliases, PDB exclusion, and installer artifacts.

#### Upgrade Notes

- Existing models, runtimes, logs, cache, state, and settings are preserved by
  installer update/repair and by default uninstall.
- Older portable installs that still launch `LlamaCppConsole.exe` remain
  supported by the portable zip alias.
- This release is unsigned. Verify the `.sha256` companion assets and expect
  Windows SmartScreen or publisher warnings until a trusted signing certificate
  is used.

#### Artifacts To Upload

- `dist\LlamaCppWindowsManager-win-x64.zip`
- `dist\LlamaCppWindowsManager-win-x64.zip.sha256`
- `dist\installer\LlamaCppWindowsManager-Setup-1.1.3-win-x64.exe`
- `dist\installer\LlamaCppWindowsManager-Setup-1.1.3-win-x64.exe.sha256`

## Detailed Change Log

### Hardening

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
- Added WSL runtime cleanup verification and log diagnostics when targeted WSL
  process shutdown cannot be confirmed.
- Added central `JobEngine` status transition validation against the persisted
  job row, preventing stale caller-side job records from making invalid durable
  status moves.
- Split runtime stdout/stderr observation into a tested helper covering
  redaction and loaded-line detection, and added recovered native-process
  shutdown coverage.
- Runtime package downloads now verify expected byte counts and SHA-256
  metadata or companion checksum files before install.
- Runtime package and portable app update archives are prevalidated for absolute
  paths, traversal paths, and unsafe tar link/device entries before extraction.
- Auto-load gateway request bodies are bounded with a default 64 MiB limit and
  return `413 request_too_large` when exceeded.
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
- Added `-RequireCleanTree` to publish, installer, and release-gate scripts for
  release artifacts that must come from a clean worktree.

### Product Updates

- Added a scoped LAN exposure setting with four modes: Local only, Gateway LAN
  only, Direct models LAN only, and Gateway + direct LAN. Existing `lan` / `LAN
  access` settings migrate to Gateway + direct LAN for compatibility.
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
- Overview Tokens and MTP Tokens now use compact two-row monitors in the form
  `0.0 t/s (Gen) | 0.0 t/s (Avg) | 0 t (Total)`, with live rates shown as
  `0.0 t/s` during idle periods and average/total segments hidden when those
  values are unavailable.
- Overview replaces the static Batching card with a live Slots card showing
  active/queued requests and busy decode slots.
- GPU metric summaries normalize separators as ` | `.
- Updated README, architecture docs, release-readiness docs, and in-app Help to
  match the current routing, OpenCode, LAN exposure, startup, vision-head, MTP,
  and runtime-dashboard behavior.

## Verification

Last verified locally on 2026-06-01:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller
```

Result: Release app build succeeded with zero warnings, release-hardening tests
passed (`432/432`), formatting was clean, no vulnerable packages were found,
the diff had no whitespace errors, and publish/installer artifact checks passed
locally.
