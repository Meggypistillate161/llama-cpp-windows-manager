# Release Readiness Checklist

Last updated: 2026-05-27

## Automated Gate

Run from a clean checkout with the .NET 8 SDK on `PATH`, or set `LLAMA_CPP_WINDOWS_MANAGER_DOTNET` to an explicit SDK `dotnet.exe`. The legacy `LLAMA_CPP_CONSOLE_DOTNET` and `LOCAL_LLM_CONSOLE_DOTNET` variables are still accepted.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-app.ps1 -Restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-app.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-vulnerabilities.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-app.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

Trusted signed release builds use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-app.ps1 -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
```

## Release Gate

- Publish `dist\LlamaCppWindowsManager-win-x64.zip` and `dist\LlamaCppWindowsManager-win-x64\LlamaCppWindowsManager.exe` from a clean checkout.
- Build `dist\installer\LlamaCppWindowsManager-Setup-1.1.2-win-x64.exe` from the published app with Inno Setup 6.
- Confirm the publish folder contains no `.pdb` files.
- Confirm the portable zip, published executable, and installer each have a matching `.sha256` companion file. For signed builds, generate the companion file after signing.
- Confirm the portable zip contains both `LlamaCppWindowsManager.exe` and the legacy `LlamaCppConsole.exe` alias for renamed-app updates.
- Confirm fresh installer default path is `D:\LlamaCppWindowsManager` when `D:` exists, `%LocalAppData%\Programs\LlamaCppWindowsManager` when it does not, and that the setup wizard still allows the user to change the install folder.
- Confirm the installer detects an existing install and reuses its install directory on update or repair.
- Confirm the final installer page can launch `LlamaCppWindowsManager.exe`.
- Confirm installer update/repair does not delete `data`, models, runtimes, cache, logs, or state.
- Confirm uninstall keeps `data` by default and only deletes it when the user explicitly chooses to delete app data.
- Launch the published app on a clean Windows user profile with no repository checkout.
- Confirm only one app instance can run in the same user session.
- Confirm Runtime Downloads can check the upstream official llama.cpp release feed and list the official prebuilt packages for CUDA Windows, CUDA WSL, Vulkan Windows, Vulkan WSL, Intel Arc SYCL Windows, Intel Arc SYCL WSL, CPU Windows, and CPU WSL.
- Confirm installing an official prebuilt runtime does not require Git, CMake, Visual Studio Build Tools, WSL build tools, or source checkout.
- Confirm installed official prebuilt runtimes are registered, can be selected per model, and show update/delete state on the Runtime Downloads page.
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
- Confirm Settings model access maps Local only to `127.0.0.1` and LAN access to `0.0.0.0`.
- Confirm Settings LAN access changes only the model runtime bind host, not the app-local control service.
- Confirm the Settings API key Generate action creates a new model API key.
- Confirm Settings shows cache size at the top and Clear removes cache contents only when downloads/builds are idle.
- Confirm local-only model serving launches with an API key and client requests include that key.
- Confirm the persisted model API key is protected at rest for the current Windows user.
- Confirm ports outside `1..65535` are rejected on Settings save.
- Confirm model serving cannot launch without a strong model API key in either local-only or LAN mode.
- Confirm a LAN client can reach the OpenAI-compatible `/v1` endpoint only after Windows Firewall and WSL networking allow the configured model port.
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
- Confirm downloaded runtime source and build deletion cannot escape the configured runtimes folder.
- Confirm successful builds from downloaded runtime sources delete the source folder when Settings > Runtime > Delete source after build is `Yes`, and preserve it when set to `No`.
- Confirm multiple models can be loaded at the same time on different saved model ports when hardware capacity allows it.
- Confirm OpenCode local model entries keep separate providers/endpoints for concurrently served models and remain stable across app restarts.
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

Last verified on 2026-05-27:

```powershell
dotnet test LocalLlmConsole.sln -c Release --no-restore --filter "FullyQualifiedName~ReleaseHardeningTests"
dotnet format LocalLlmConsole.sln --verify-no-changes --no-restore --verbosity minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-app.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -SkipPublish
```

Result: release-hardening tests passed, formatting was clean, the portable zip
was produced with both executable names, and the v1.1.2 installer compiled.

## Manual Clean-Machine Test

1. Start from a clean Windows VM.
2. Install `dist\installer\LlamaCppWindowsManager-Setup-1.1.2-win-x64.exe`.
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
22. Import an external model folder, delete the registration, and verify GGUF files remain.
23. Add a downloaded app-owned model, delete it, and verify only app-owned paths are removed.
24. Verify the OpenCode page remains optional and does not block core workflows.
25. Verify app update checks can reach the GitHub release feed, and that update install works from a copied portable exe folder.

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
