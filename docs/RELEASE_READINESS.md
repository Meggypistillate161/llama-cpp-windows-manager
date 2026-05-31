# Release Readiness Checklist

Last updated: 2026-05-31

## Automated Gate

Run from a clean checkout with the .NET 8 SDK on `PATH`, or set `LLAMA_CPP_WINDOWS_MANAGER_DOTNET` to an explicit SDK `dotnet.exe`. The legacy `LLAMA_CPP_CONSOLE_DOTNET` and `LOCAL_LLM_CONSOLE_DOTNET` variables are still accepted.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-app.ps1 -Restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-app.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-vulnerabilities.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-app.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

The same source-level gate can be run through the local wrapper, with packaging
included when the machine has Inno Setup and any required signing certificate:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller
```

Trusted signed release builds use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-app.ps1 -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
```

## Release Gate

- Publish `dist\LlamaCppWindowsManager-win-x64.zip` and `dist\LlamaCppWindowsManager-win-x64\LlamaCppWindowsManager.exe` from a clean checkout.
- Build `dist\installer\LlamaCppWindowsManager-Setup-1.1.3-win-x64.exe` from the published app with Inno Setup 6.
- Confirm the publish folder contains no `.pdb` files.
- Confirm the portable zip, published executable, and installer each have a matching `.sha256` companion file. For signed builds, generate the companion file after signing.
- Confirm signed installer builds fail before compilation if `-SkipPublish`
  points at an unsigned published executable.
- Confirm the portable zip contains both `LlamaCppWindowsManager.exe` and the legacy `LlamaCppConsole.exe` alias for renamed-app updates.
- Confirm fresh installer default path is `D:\LlamaCppWindowsManager` when `D:` exists, `%LocalAppData%\Programs\LlamaCppWindowsManager` when it does not, and that the setup wizard still allows the user to change the install folder.
- Confirm the installer detects an existing install and reuses its install directory on update or repair.
- Confirm the final installer page can launch `LlamaCppWindowsManager.exe`.
- Confirm fresh installer setups offer Start with Windows checked by default,
  and that Settings can disable or re-enable the current-user startup entry.
- Confirm installer update/repair does not delete `data`, models, runtimes, cache, logs, or state.
- Confirm uninstall keeps `data` by default and only deletes it when the user explicitly chooses to delete app data.
- Launch the published app on a clean Windows user profile with no repository checkout.
- Confirm only one app instance can run in the same user session.
- Confirm Runtime Downloads can check the upstream official llama.cpp release feed and list the official prebuilt packages for CUDA Windows, CUDA WSL, Vulkan Windows, Vulkan WSL, Intel Arc SYCL Windows, Intel Arc SYCL WSL, CPU Windows, and CPU WSL.
- Confirm Runtime Downloads can check the Atomic TurboQuant binary feed, install the Windows CUDA package when published, and show the WSL CUDA row as not published until a matching Linux/WSL asset exists.
- Confirm installing a prebuilt runtime does not require Git, CMake, Visual Studio Build Tools, WSL build tools, or source checkout.
- Confirm installed prebuilt runtimes are registered, can be selected per model, and show update/delete state on the Runtime Downloads page.
- Confirm official prebuilt CUDA downloads include the matching runtime DLL/archive companion when upstream publishes one.
- Confirm source-built official runtimes can be reconciled with matching prebuilt runtimes by local runtime fingerprint.
- Confirm WSL is installed and the configured Ubuntu distro exists when a WSL runtime or WSL source build is selected, or missing prerequisites are reported clearly.
- Confirm the WSL Linux page detects `wsl.exe`, installed distros, the WSL default distro, and the app-selected distro.
- Confirm Docker-managed WSL distros such as `docker-desktop` are not shown as selectable runtime distros.
- Confirm the app prefers an installed Ubuntu distro instead of keeping a missing hardcoded distro.
- Confirm WSL install appears when WSL is missing.
- Confirm Ubuntu install appears when WSL exists but no Ubuntu distro is installed.
- Confirm Ubuntu install attempts to install `cmake` and the CPU build toolchain after the distro is ready.
- Confirm the WSL Linux page offers an Install CPU Tools action for existing Ubuntu distros and does not imply CUDA is installed.
- Confirm the WSL Linux page offers an Install CUDA action for existing Ubuntu distros and that it verifies `nvcc` and `libcudart`.
- Confirm the WSL Linux page offers an Install Vulkan action for existing Ubuntu distros and that it verifies `vulkaninfo --summary`.
- Confirm the WSL Linux page offers Intel GPU runtime and Intel oneAPI actions for existing Ubuntu distros and that they verify `sycl-ls`/Level Zero visibility for SYCL.
- Confirm the Windows page detects Git, CMake, MSVC, CUDA, Vulkan, Intel oneAPI/SYCL tools, and whether an Intel GPU is visible to `sycl-ls`.
- Confirm CPU/CUDA/Vulkan/SYCL actions switch to Update/Repair when detected and show Delete actions only when detected.
- Confirm Delete WSL and Delete Ubuntu actions require explicit confirmation and open visible PowerShell.
- Confirm WSL and Ubuntu update checks appear when those components are installed.
- Confirm the WSL row shows Install WSL when WSL is missing and Update WSL when WSL exists.
- Confirm the Ubuntu row shows Install Ubuntu when Ubuntu is missing and Update Ubuntu when Ubuntu exists.
- Confirm the local service binds only to `127.0.0.1`.
- Confirm model serving defaults to local-only `127.0.0.1`.
- Confirm Settings LAN exposure maps Local only to loopback, Gateway LAN only to the router listener, Direct models LAN only to runtime hosts, and Gateway + direct LAN to both serving surfaces.
- Confirm Settings LAN exposure changes only model-serving endpoints, not the app-local control service.
- Confirm the Overview Loaded Model Sessions grid shows an auto-load gateway
  router row with endpoint, policy, LAN exposure, and current direct-session
  count.
- Confirm the Settings API key Generate action creates a new model API key.
- Confirm Settings > OpenCode > Sync on launch save controls whether saved
  launch settings and saved variants automatically rewrite OpenCode local model
  entries.
- Confirm Settings shows cache size at the top and Clear removes cache contents only when downloads/builds are idle.
- Confirm local-only model serving launches with an API key and client requests include that key.
- Confirm the persisted model API key is protected at rest for the current Windows user.
- Confirm ports outside `1..65535` are rejected on Settings save.
- Confirm model serving cannot launch without a strong model API key in any
  local-only or LAN exposure mode.
- Confirm a LAN client can reach the selected OpenAI-compatible `/v1` serving
  surface only after Windows Firewall and WSL networking allow the configured
  gateway or direct model port.
- Confirm the WPF app is the only user-facing surface; no web UI is launched.
- Confirm no command prompt windows remain open for app services.
- Confirm app-local API requests without the session token return `401`.
- Confirm SQLite state tables are created under the startup workspace.
- Confirm corrupt settings are backed up and defaulted.
- Confirm corrupt SQLite DB files are quarantined and the app recreates state.
- Confirm interrupted jobs are marked `Interrupted` on restart and can be resumed or removed.
- Confirm Hugging Face downloads cannot write outside the configured models folder.
- Confirm completed downloads are not registered when the final byte count mismatches the expected size or no expected size/SHA-256 metadata exists.
- Confirm imported external model deletion removes only app registration files.
- Confirm app-owned downloaded model deletion cannot escape the configured model root.
- Confirm vision-capable model settings persist image min/max token allowances and launch `llama-server` with `--image-min-tokens` / `--image-max-tokens` when set.
- Confirm per-model Vision head choices persist for auto-detect,
  embedded/model-bundled, and explicit external projectors; explicit projectors
  launch with `--mmproj`, embedded choices omit `--mmproj`, and synced OpenCode
  entries are marked vision-capable when the launch settings prove support.
- Confirm per-model MTP head choices persist separately from Vision head,
  `Spec type = mtp` launches with `--mtp-head`, and draft-* speculative modes
  continue to use the upstream `--model-draft` path.
- Confirm downloaded runtime source and build deletion cannot escape the configured runtimes folder.
- Confirm successful builds from downloaded runtime sources delete the source folder when Settings > Runtime > Delete source after build is `Yes`, and preserve it when set to `No`.
- Confirm multiple models can be loaded at the same time on different saved model ports when hardware capacity allows it.
- Confirm the auto-load gateway serves one shared `/v1` endpoint, launches the
  requested model on its saved direct port, and proxies requests to that direct
  endpoint.
- Confirm Gateway policy > Prefer keeping loaded models preserves existing
  sessions and blocks/warns clearly when VRAM admission predicts that another
  GPU model is unsafe.
- Confirm Gateway policy > Single active model unloads other direct sessions
  before loading the requested model.
- Confirm OpenCode local model entries can use either the shared gateway
  provider or direct per-model providers/endpoints, remain stable across app
  restarts, and include vision support when launch settings prove it.
- Confirm CPU-only Ubuntu/WSL llama.cpp source build path succeeds after Install CPU Tools, or fails early if Git/CMake/compiler tools are still missing inside Ubuntu.
- Confirm CUDA Ubuntu/WSL llama.cpp source build path succeeds after Install CUDA on supported NVIDIA hardware, or fails early with a clear driver/toolkit error.
- Confirm Vulkan Ubuntu/WSL llama.cpp source build path succeeds after Install Vulkan on supported WSL Vulkan hardware, or fails early with a clear driver/toolkit error.
- Confirm Intel Arc SYCL Windows and WSL launches/source builds fail early with clear oneAPI/SYCL prerequisite messages when tools or Level Zero GPU visibility are missing.
- Confirm custom runtime repository row can add an HTTPS repo and then download/check/delete it from Runtime Repositories.
- Confirm CUDA runtime builds fail before CMake with a clear message when `nvcc` or `libcudart`/CUDA Toolkit runtime libraries are missing inside the selected WSL distro.
- Confirm Vulkan runtime builds fail before CMake with a clear message when Vulkan headers, `glslc`, `vulkaninfo`, `libvulkan.so`, SPIR-V headers, or a WSL-visible Vulkan device are unavailable.
- Confirm OpenCode is absent or present; either way, core model management still works and the OpenCode page remains optional.
- Confirm startup update checks change the left-nav Updates item to Install Update when a newer GitHub release exists.
- Confirm manual Check For Updates shows a no-update popup when current, or an install confirmation when a newer release exists.
- Confirm release assets include a matching SHA-256 companion file and that a bad checksum prevents staging.
- Confirm a signed installed app refuses an unsigned or differently signed staged update.
- Confirm a completed staged update restarts `LlamaCppWindowsManager.exe` and shows the GitHub release notes.
- Confirm an older `LlamaCppConsole.exe` portable install can stage the v1.1.2 update without changing the target path unexpectedly.

## Latest Local Verification

Current local check on 2026-05-31:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-app.ps1 -Restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-app.ps1
D:\LLM\.dotnet-sdk-8\dotnet.exe format LocalLlmConsole.sln --verify-no-changes --verbosity minimal
git diff --check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-vulnerabilities.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1
```

Result: Release app build succeeded with zero warnings, release-hardening tests
passed (`422/422`), formatting was clean, no vulnerable packages were found,
the diff had no whitespace errors, and publish/installer artifact checks passed
locally. The next release notes draft is tracked in
`docs/GITHUB_RELEASE_NEXT.md`.

## Manual Clean-Machine Test

1. Start from a clean Windows VM.
2. Install `dist\installer\LlamaCppWindowsManager-Setup-1.1.3-win-x64.exe`.
3. Confirm the installer prefers `D:\LlamaCppWindowsManager` when `D:` exists and allows choosing a different folder before install.
4. Confirm the launch-after-install option opens the app.
5. Confirm first launch creates `data\models`, `data\runtimes`, `data\cache`, `data\state`, and `data\logs` beside the exe when the install folder is writable.
6. Run the installer again and confirm it detects and updates the existing install without deleting `data`.
7. Uninstall and confirm `data` is kept by default; repeat on a disposable install and choose the explicit delete-data option to confirm data removal.
8. Copy only `dist\LlamaCppWindowsManager-win-x64\LlamaCppWindowsManager.exe` into a writable portable test folder.
9. Confirm launching from a non-writable location falls back to `%LocalAppData%\llama.cpp Windows Manager`, reuses `%LocalAppData%\llama.cpp Console` or `%LocalAppData%\LocalLlmConsole` only for an existing legacy folder, or reports a clear workspace error.
10. Launch the app without Git, CMake, CUDA, or OpenCode.
11. Verify the app opens, creates state, and explains missing Ubuntu/WSL prerequisites without crashing.
12. Use Runtime Downloads to install an official prebuilt CPU Windows runtime, then confirm it appears in model launch runtime choices.
13. On suitable hardware, repeat Runtime Downloads for CUDA, Vulkan, or Intel Arc SYCL Windows/WSL packages.
14. Use the WSL Linux page to install or detect Ubuntu only when testing WSL runtimes or source builds.
15. Use Install CPU Tools to install Git, CMake, and build tools inside Ubuntu, then validate CPU-only WSL source-build preflight.
16. Try a CUDA source build without CUDA Toolkit inside Ubuntu/WSL and confirm the app reports that the WSL CUDA Toolkit is missing before CMake runs.
17. Try a Vulkan source build without Vulkan tools or a WSL-visible Vulkan device and confirm the app reports the missing Vulkan prerequisite before CMake runs.
18. Try a SYCL launch/source build without oneAPI or a Level Zero-visible Intel GPU and confirm the app reports the missing Intel Arc prerequisite.
19. Change the selected distro and validate missing-distro errors.
20. Download a small GGUF, interrupt the app mid-download, relaunch, and verify job recovery.
21. Load two small models on different saved ports and confirm both endpoints remain reachable.
22. Enable Gateway LAN only, confirm a LAN client can reach the gateway but not
    direct model ports; then enable Direct models LAN only and confirm the
    inverse.
23. Import an external model folder, delete the registration, and verify GGUF files remain.
24. Add a downloaded app-owned model, delete it, and verify only app-owned paths are removed.
25. Verify the OpenCode page remains optional and does not block core workflows.
26. Verify app update checks can reach the GitHub release feed, and that update install works from a copied portable exe folder.

## Release Blockers

- Any unauthenticated mutating localhost API.
- Any wildcard CORS header on a local control API.
- Any recursive delete not bounded by ownership and path-root checks.
- Any llama.cpp launch default that binds model serving to `0.0.0.0`.
- Any model-serving mode that does not require an API key.
- Any completed download registered without expected-size or SHA-256 validation.
- Any clean-machine startup path that silently assumes hidden developer setup.
- Any release artifact described as signed or trusted when it is unsigned.
- Any signed install that can be replaced by an unsigned or differently signed update.
- Any installer uninstall, repair, or update path that deletes models, runtimes, logs, cache, or state without explicit user confirmation.
