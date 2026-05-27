param(
  [switch] $Restore,
  [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$AppDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$Project = Join-Path $AppDir "src\LocalLlmConsole.App\LocalLlmConsole.App.csproj"
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
  throw ".NET runtime is installed, but no SDK was found. Install the .NET 8 SDK to build the WPF app."
}
if (-not (Test-Path -LiteralPath $Project)) {
  throw "WPF project not found: $Project"
}

if ($Restore) {
  & $Dotnet restore $Project
  if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }
}

& $Dotnet build $Project -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

Write-Host "Built llama.cpp Windows Manager app." -ForegroundColor Green
