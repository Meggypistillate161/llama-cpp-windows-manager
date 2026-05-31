# Release Hardening Audit

Audit date: 2026-05-31

## Executive Summary

Overall release posture: **v1.1.2 is ready as an unsigned community release
with explicit SmartScreen and SHA-256 verification notes. Trusted code-signing
and broader clean-machine/hardware validation remain follow-up hardening work,
not hidden blockers for the current public release.**

The core release blockers from the full audit have been addressed in code:

- Release build and self-contained publish now verify on .NET SDK 8.0.421 / runtime 8.0.27.
- Automated release-hardening tests now cover concurrent SQLite access, corrupt settings recovery, deletion boundaries, and runtime host validation.
- SQLite access is serialized and settings saves are transactional.
- Corrupt settings are backed up before defaults are restored; corrupt DB files are quarantined and recreated.
- The workspace is fixed at process startup instead of being editable at runtime.
- Job IDs use GUIDs.
- Hugging Face downloads are bounded to the models folder, block duplicate destinations, reject unsafe local filenames and partial-file links, preflight disk space, and require expected-size or SHA-256 verification before model registration.
- Model serving now requires a strong API key even in local-only mode, and the persisted key is protected with current-user Windows data protection.
- Runtime source IDs loaded from custom JSON are sanitized, and recursive runtime deletes are path-bounded.
- WSL shutdown no longer uses a broad port-only kill.
- The WSL Linux page now detects WSL, installed non-Docker distros, the default distro, and shows focused WSL/Ubuntu install or update actions.
- Release publish omits PDB files and supports certificate signing with `-CertificateThumbprint` and `-RequireSigned`.
- App update checks are staged through the workspace cache and replace the portable exe only after the running process closes.
- App update staging verifies a matching SHA-256 companion asset when present and requires same-certificate signature continuity when the installed app is already signed.
- Runtime onboarding is prebuilt-first: official llama.cpp release packages can
  be installed directly before using source builds.
- The Windows and WSL setup workflows now cover CPU, CUDA, Vulkan, and Intel
  Arc SYCL prerequisites before advanced source builds start.
- Per-model launch settings now include vision image token allowances and map them to llama.cpp server flags.
- Per-model ports and loaded model sessions allow more than one model endpoint
  to stay available when hardware capacity allows it.
- The auto-load gateway provides one shared OpenAI-compatible endpoint, routes
  by requested model id, starts models on their saved direct ports, and exposes
  policy controls for keeping loaded sessions or switching to one active model.
- LAN exposure is scoped by Settings so users can expose only the gateway, only
  direct model endpoints, both, or neither.
- Per-model launch profiles now support saved variants, auto-detected,
  embedded/model-bundled, or explicit vision head/projector choices, vision
  image token allowances, separate MTP head choices for compatible runtimes,
  and OpenCode vision metadata when synced.
- OpenCode sync can be automatic on launch-setting/variant save or manually
  controlled from the OpenCode page.
- Fresh installer setups offer Start with Windows by default, with a matching
  current-user startup preference in Settings.
- The local app service now keeps request handlers observed and tolerates
  bounded transient listener errors instead of silently faulting the listener
  loop.

## Remaining External Hardening Work

### Clean Windows VM validation

- Severity: High
- Area: Installation and onboarding
- Status: Follow-up hardening
- Required result: Published app launches with no repository checkout, creates state, shows clear prerequisite guidance, and does not require a developer SDK.

### Trusted signing and distribution

- Severity: High for reducing Windows trust warnings
- Area: Distribution and trust
- Status: Portable single-exe publish and Inno Setup installer source exist; signing support exists; certificate is not present in this repo. The current public release is unsigned and labeled as such.
- Required result: A future trusted release is signed with a trusted certificate and distributed as a signed portable zip or installer with shortcut/uninstall flow.

### GitHub update feed

- Severity: Medium
- Area: Distribution
- Status: Update UI, staged installer, checksum verification, and signed-app signature continuity are implemented; the public repository and v1.1.2 asset naming are confirmed.
- Required result: Latest GitHub release contains `LlamaCppWindowsManager-win-x64.zip`, matching SHA-256 companion assets, and release notes suitable for the completion popup.

### WSL and hardware matrix

- Severity: Medium
- Area: llama.cpp runtime/build support
- Status: Requires manual hardware coverage
- Required result: Validate missing WSL, missing distro, CPU build, missing Git/CMake/compiler, CUDA-visible WSL, Vulkan-visible WSL, Intel Arc/SYCL-visible Windows and WSL, and unsupported backend paths.
- Added support: The app can detect installed non-Docker distros and guide WSL install/update, Ubuntu install/update, CPU tools, CUDA Toolkit, Vulkan tool setup, Intel GPU runtime setup, and Intel oneAPI setup from the WSL Linux page. The Windows page detects native CPU/CUDA/Vulkan/SYCL tool readiness.

### Runtime/archive authenticity verification

- Severity: Medium
- Area: Third-party binaries
- Status: Prebuilt runtime downloads are installed from their configured package
  sources, including official GitHub release assets and selected fork binary
  feeds, then locally fingerprinted for source/prebuilt equivalence where
  possible. Package authenticity still depends on the package source transport
  and release trust unless matching trusted upstream checksums or signatures
  become available.
- Required result: Prefer trusted upstream checksums or signatures for runtime
  archives when upstream publishes them.

## Automated Checks

Current passing checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-app.ps1 -Restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-app.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-vulnerabilities.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-app.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -SkipPublish
```

The latest local architecture/release pass ran
`test-release-gate.ps1 -IncludePublish -IncludeInstaller`, which wraps the
release build, release-hardening suite, formatting verification,
`git diff --check`, vulnerable-package audit, publish smoke, and installer
artifact checks. The portable zip for v1.1.2 and newer includes both
`LlamaCppWindowsManager.exe` and the legacy `LlamaCppConsole.exe` alias for
renamed-app update compatibility.

## Post-v1.1.2 Hardening

After publishing `v1.1.2`, a follow-up bug-report triage fixed the actionable
low-risk items that were safe to take immediately:

- Runtime backend inference now prefers explicit packaged metadata and nearby
  runtime files over loose folder/path text, avoiding false CUDA/SYCL/Vulkan
  classification from names like `cuda-backup`.
- `LlamaProcessSupervisor` runtime state transitions are now atomic/volatile
  across process output callbacks, readiness checks, and exit handling.
- `LogFileService.Head` now detects byte-order marks like `Tail` already did.
- `GgufMetadataReader` now ignores unsupported/future GGUF versions instead of
  silently parsing unknown metadata layouts.

Verification for this hardening pass:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller
```

Result on 2026-05-31: release-hardening tests passed (`422/422`), formatting was
clean, the Release build succeeded with zero warnings, no vulnerable packages
were found, the diff had no whitespace errors, and publish/installer artifact
checks passed locally.

## Edge Cases To Keep Testing

- No internet during Hugging Face search or download.
- Slow internet with cancellation during a large GGUF download.
- Interrupted app shutdown during model download or llama.cpp build.
- Disk full during download, build, extract, or SQLite write.
- Missing WSL, missing configured Ubuntu distro, or WSL disabled.
- Git, CMake, compiler, CUDA, Vulkan, or Intel oneAPI/SYCL missing inside Ubuntu.
- Permission denied for workspace, models, runtime, or cache folders.
- Invalid, partial, renamed, or moved GGUF model files.
- Missing or deleted llama-server executable after registration.
- Manually edited or corrupt SQLite/settings state.
- Unicode, spaces, long paths, and non-default drive letters.
- OpenCode missing, outdated, or misconfigured.

## Release Decision

v1.1.2 is acceptable as a clearly unsigned public community release. A future
trusted/stable Windows distribution should add Authenticode signing, broader
clean-machine smoke testing, and wider hardware matrix validation.
