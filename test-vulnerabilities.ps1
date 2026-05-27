param(
  [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

function Resolve-Dotnet {
  $appDir = $PSScriptRoot
  $bundledDotnet = Join-Path (Split-Path -Parent $appDir) ".dotnet-sdk-8\dotnet.exe"
  if ($env:LLAMA_CPP_WINDOWS_MANAGER_DOTNET) {
    return $env:LLAMA_CPP_WINDOWS_MANAGER_DOTNET
  }
  if ($env:LLAMA_CPP_CONSOLE_DOTNET) {
    return $env:LLAMA_CPP_CONSOLE_DOTNET
  }
  if ($env:LOCAL_LLM_CONSOLE_DOTNET) {
    return $env:LOCAL_LLM_CONSOLE_DOTNET
  }
  if (Test-Path -LiteralPath $bundledDotnet) {
    return $bundledDotnet
  }
  $command = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
  if ($command) {
    return $command.Source
  }
  return ""
}

function Count-Vulnerabilities($node) {
  if ($null -eq $node) { return 0 }

  $count = 0
  if ($node -is [System.Collections.IDictionary]) {
    foreach ($key in $node.Keys) {
      $value = $node[$key]
      if ($key -eq "vulnerabilities" -and $value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
        $count += @($value).Count
      } else {
        $count += Count-Vulnerabilities $value
      }
    }
    return $count
  }

  if ($node -is [pscustomobject]) {
    foreach ($property in $node.PSObject.Properties) {
      $value = $property.Value
      if ($property.Name -eq "vulnerabilities" -and $value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
        $count += @($value).Count
      } else {
        $count += Count-Vulnerabilities $value
      }
    }
    return $count
  }

  if ($node -is [System.Collections.IEnumerable] -and -not ($node -is [string])) {
    foreach ($item in $node) {
      $count += Count-Vulnerabilities $item
    }
  }

  return $count
}

$appDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projects = @(
  (Join-Path $appDir "src\LocalLlmConsole.App\LocalLlmConsole.App.csproj"),
  (Join-Path $appDir "tests\LocalLlmConsole.Tests\LocalLlmConsole.Tests.csproj")
)

$dotnet = Resolve-Dotnet
if (-not $dotnet) {
  throw ".NET SDK was not found. Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0."
}
if (-not (Test-Path -LiteralPath $dotnet)) {
  throw "Configured dotnet path was not found: $dotnet"
}

$info = & $dotnet --info
if ($info -match "No SDKs were found") {
  throw ".NET runtime is installed, but no SDK was found. Install the .NET 8 SDK to audit packages."
}

$totalVulnerabilities = 0
foreach ($project in $projects) {
  if (-not (Test-Path -LiteralPath $project)) {
    throw "Project not found: $project"
  }

  & $dotnet restore $project
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed for $project."
  }

  $jsonText = & $dotnet list $project package --vulnerable --include-transitive --format json
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet package vulnerability audit failed for $project."
  }

  $json = $jsonText | ConvertFrom-Json
  $count = Count-Vulnerabilities $json
  $totalVulnerabilities += $count
  if ($count -gt 0) {
    Write-Host "Vulnerable packages found in $project" -ForegroundColor Red
    $jsonText | Write-Host
  } else {
    Write-Host "No vulnerable packages found in $project" -ForegroundColor Green
  }
}

if ($totalVulnerabilities -gt 0) {
  throw "Package vulnerability audit failed: $totalVulnerabilities vulnerable package reference(s) found."
}
