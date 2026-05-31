param(
  [ValidateSet("win-x64")]
  [string] $Runtime = "win-x64",
  [string] $Configuration = "Release",
  [string] $CertificateThumbprint = "",
  [string] $TimestampServer = "http://timestamp.digicert.com",
  [switch] $RequireSigned,
  [switch] $RequireCleanTree
)

$ErrorActionPreference = "Stop"

function Assert-CleanGitTree {
  param(
    [Parameter(Mandatory = $true)]
    [string] $Path
  )

  $git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue
  if (-not $git) {
    throw "Git was not found. Install Git or omit -RequireCleanTree."
  }

  & $git.Source -C $Path rev-parse --is-inside-work-tree | Out-Null
  if ($LASTEXITCODE -ne 0) {
    throw "Clean-tree check requires a Git worktree: $Path"
  }

  $status = @(& $git.Source -C $Path status --porcelain --untracked-files=all)
  if ($LASTEXITCODE -ne 0) {
    throw "git status failed while checking the release worktree."
  }
  if ($status.Count -ne 0) {
    throw "Release requires a clean Git worktree. Commit, stash, or remove changes before retrying:`n$($status -join [Environment]::NewLine)"
  }
}

function Remove-DistPath {
  param(
    [Parameter(Mandatory = $true)]
    [string] $Path,
    [Parameter(Mandatory = $true)]
    [string] $Label,
    [switch] $Recurse
  )

  $full = [System.IO.Path]::GetFullPath($Path)
  $root = $DistRoot.TrimEnd('\', '/')
  if (-not $full.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove $Label outside the dist folder: $full"
  }
  if (-not (Test-Path -LiteralPath $full)) {
    return
  }

  $item = Get-Item -LiteralPath $full -Force
  if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Refusing to remove $Label because it is a symlink or junction: $full"
  }

  if ($Recurse) {
    Remove-Item -LiteralPath $full -Recurse -Force
  } else {
    Remove-Item -LiteralPath $full -Force
  }
}

$AppDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ($RequireCleanTree) {
  Assert-CleanGitTree -Path $AppDir
}

$Project = Join-Path $AppDir "src\LocalLlmConsole.App\LocalLlmConsole.App.csproj"
$DistRoot = [System.IO.Path]::GetFullPath((Join-Path $AppDir "dist"))
$PublishDir = [System.IO.Path]::GetFullPath((Join-Path $DistRoot "LlamaCppWindowsManager-$Runtime"))
if (-not ($PublishDir.StartsWith($DistRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))) {
  throw "Refusing to publish outside the dist folder: $PublishDir"
}
$BundledDotnet = Join-Path (Split-Path -Parent $AppDir) ".dotnet-sdk-8\dotnet.exe"
$Dotnet = if ($env:LLAMA_CPP_WINDOWS_MANAGER_DOTNET) {
  $env:LLAMA_CPP_WINDOWS_MANAGER_DOTNET
} elseif ($env:LLAMA_CPP_CONSOLE_DOTNET) {
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
  Remove-DistPath -Path $PublishDir -Label "publish folder" -Recurse
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

$Exe = Join-Path $PublishDir "LlamaCppWindowsManager.exe"
$LegacyExe = Join-Path $PublishDir "LlamaCppConsole.exe"
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

Copy-Item -LiteralPath $Exe -Destination $LegacyExe -Force

$ExeHash = (Get-FileHash -LiteralPath $Exe -Algorithm SHA256).Hash.ToLowerInvariant()
$ExeHashPath = "$Exe.sha256"
Set-Content -LiteralPath $ExeHashPath -Value "$ExeHash  $(Split-Path -Leaf $Exe)" -Encoding ascii

$LegacyExeHashPath = "$LegacyExe.sha256"
Set-Content -LiteralPath $LegacyExeHashPath -Value "$ExeHash  $(Split-Path -Leaf $LegacyExe)" -Encoding ascii

$ZipPath = Join-Path $DistRoot "LlamaCppWindowsManager-$Runtime.zip"
if (Test-Path -LiteralPath $ZipPath) {
  Remove-DistPath -Path $ZipPath -Label "portable release archive"
}
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force
$ZipHash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$ZipHashPath = "$ZipPath.sha256"
Set-Content -LiteralPath $ZipHashPath -Value "$ZipHash  $(Split-Path -Leaf $ZipPath)" -Encoding ascii

Write-Host "Published llama.cpp Windows Manager self-contained app to $PublishDir" -ForegroundColor Green
Write-Host "Wrote SHA-256 companion file to $ExeHashPath" -ForegroundColor Green
Write-Host "Wrote portable release archive to $ZipPath" -ForegroundColor Green
