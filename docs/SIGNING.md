# Signing Windows Releases

Trusted Windows releases should be Authenticode-signed and timestamped before
upload. The current v1.1.2 community release is unsigned and should be described
as unsigned wherever it is linked. The release scripts support signing with a
certificate already available in the Windows certificate store:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\publish-app.ps1 -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1 -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test-release-gate.ps1 -IncludePublish -IncludeInstaller -CertificateThumbprint "<cert-thumbprint>" -RequireSigned
```

When `build-installer.ps1 -SkipPublish -RequireSigned` reuses an existing
publish folder, the script verifies that the published executable is already
signed before compiling and signing the installer.

## Free Options

- **Free and publicly useful for qualifying OSS:** apply to SignPath Foundation
  for open-source code signing. If accepted, use their signing workflow for
  release artifacts.
- **Free but not publicly trusted:** self-signed certificates are useful for
  local testing and enterprise environments where the certificate is deployed to
  trusted stores. They do not remove SmartScreen or public trust warnings for
  normal users.
- **Free integrity, not Authenticode trust:** publish `.sha256` companion files
  and GitHub release provenance. This helps users verify downloads, but it is
  not a substitute for Windows code signing.

## Trusted Release Rule

Do not describe a release as signed, trusted, or production-hardened unless:

1. `LlamaCppWindowsManager.exe` is signed before the installer is compiled.
2. `LlamaCppWindowsManager-Setup-<version>-win-x64.exe` is signed.
3. `LlamaCppWindowsManager-win-x64.zip` is generated from signed contents.
4. Each uploaded binary/archive has a matching `.sha256` companion asset generated after
   signing.
