# Windows Installer

The installer is built with Inno Setup 6 from the self-contained `win-x64` publish output.

## Build

Install Inno Setup 6, make sure `ISCC.exe` is on `PATH`, or set:

```powershell
$env:LLAMA_CPP_CONSOLE_INNO_SETUP = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

Then run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

For a public build, sign the app and installer:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
```

The setup executable is written to:

```text
dist\installer\LlamaCppConsole-Setup-1.1.0-win-x64.exe
```

## Install Behavior

- Fresh installs prefer `D:\LlamaCppConsole` when the `D:` drive exists.
- If `D:` is unavailable, the installer defaults to `%LocalAppData%\Programs\LlamaCppConsole`.
- The install folder is still editable in the setup wizard before files are copied.
- Existing installations are detected by a stable Inno Setup `AppId`, so updates and repairs reuse the previous install folder.
- The final installer page includes a launch-after-install option.
- The installer creates a Start Menu shortcut and offers an optional Desktop shortcut.

## Data Preservation

The app creates its workspace under `data` beside `LlamaCppConsole.exe` when that location is writable:

```text
data\
  models\
  runtimes\
  cache\
  state\
  logs\
```

Installer updates and repairs overwrite application files only. They do not delete `data`, models, runtimes, logs, cache, or state.

Uninstall keeps `data` by default. If `data` exists, the uninstaller asks whether to delete it, with the safe default set to keep the data.
