param(
  [ValidateSet("win-x64")]
  [string] $Runtime = "win-x64",
  [string] $Configuration = "Release",
  [string] $CertificateThumbprint = "",
  [string] $TimestampServer = "http://timestamp.digicert.com",
  [switch] $RequireSigned
)

$ErrorActionPreference = "Stop"

$AppDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$Project = Join-Path $AppDir "src\LocalLlmConsole.App\LocalLlmConsole.App.csproj"
$DistRoot = [System.IO.Path]::GetFullPath((Join-Path $AppDir "dist"))
$PublishDir = [System.IO.Path]::GetFullPath((Join-Path $DistRoot "LlamaCppConsole-$Runtime"))
if (-not ($PublishDir.StartsWith($DistRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))) {
  throw "Refusing to publish outside the dist folder: $PublishDir"
}
$BundledDotnet = Join-Path (Split-Path -Parent $AppDir) ".dotnet-sdk-8\dotnet.exe"
$Dotnet = if ($env:LLAMA_CPP_CONSOLE_DOTNET) {
  $env:LLAMA_CPP_CONSOLE_DOTNET
} elseif ($env:LOCAL_LLM_CONSOLE_DOTNET) {
  $env:LOCAL_LLM_CONSOLE_DOTNET
} elseif (Test-Path -LiteralPath $BundledDotnet) {
  $BundledDotnet
} else {
  (Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue).Source
}
if (-not $Dotnet) {
  throw ".NET SDK was not found. Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0."
}
if (-not (Test-Path -LiteralPath $Dotnet)) {
  throw "Configured dotnet path was not found: $Dotnet"
}

$Info = & $Dotnet --info
if ($Info -match "No SDKs were found") {
  throw ".NET runtime is installed, but no SDK was found. Install the .NET 8 SDK to publish the self-contained app."
}

if (Test-Path -LiteralPath $PublishDir) {
  Remove-Item -LiteralPath $PublishDir -Recurse -Force
}

& $Dotnet publish $Project `
  -c $Configuration `
  -r $Runtime `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Get-ChildItem -Path $PublishDir -Recurse -Filter *.pdb -File -ErrorAction SilentlyContinue |
  Remove-Item -Force

$Exe = Join-Path $PublishDir "LlamaCppConsole.exe"
if ($CertificateThumbprint) {
  $Cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint -replace '\s', '' -ieq ($CertificateThumbprint -replace '\s', '') } |
    Select-Object -First 1
  if (-not $Cert) { throw "Code-signing certificate was not found in CurrentUser or LocalMachine certificate stores: $CertificateThumbprint" }
  $Signature = Set-AuthenticodeSignature -FilePath $Exe -Certificate $Cert -TimestampServer $TimestampServer
  if ($Signature.Status -ne "Valid") { throw "Code signing failed: $($Signature.Status) $($Signature.StatusMessage)" }
}

$PublishedSignature = Get-AuthenticodeSignature -FilePath $Exe
if ($RequireSigned -and $PublishedSignature.Status -ne "Valid") {
  throw "Published executable is not signed. Pass -CertificateThumbprint or sign $Exe before release."
}
if ($PublishedSignature.Status -ne "Valid") {
  Write-Warning "Published executable is not signed. Use -CertificateThumbprint and -RequireSigned for public release builds."
}

$ExeHash = (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
$ExeHashPath = "$Exe.sha256"
Set-Content -LiteralPath $ExeHashPath -Value "$ExeHash  $(Split-Path -Leaf $Exe)" -Encoding ascii

Write-Host "Published llama.cpp Console self-contained app to $PublishDir" -ForegroundColor Green
Write-Host "Wrote SHA-256 companion file to $ExeHashPath" -ForegroundColor Green
