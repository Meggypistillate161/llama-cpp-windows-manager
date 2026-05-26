using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public void ProjectDeclaresVersionOneMetadata()
    {
        var project = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "LocalLlmConsole.App.csproj"));

        Assert.Contains("<Version>1.0.0</Version>", project, StringComparison.Ordinal);
        Assert.Contains("<AssemblyVersion>1.0.0.0</AssemblyVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<FileVersion>1.0.0.0</FileVersion>", project, StringComparison.Ordinal);
        Assert.Contains("<InformationalVersion>v1.0</InformationalVersion>", project, StringComparison.Ordinal);
    }


    [Fact]
    public void ReleaseDocsAndScriptsUseLaunchBranding()
    {
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));
        var buildScript = File.ReadAllText(FindRepositoryFile("build-app.ps1"));
        var publishScript = File.ReadAllText(FindRepositoryFile("publish-app.ps1"));
        var startScript = File.ReadAllText(FindRepositoryFile("start-app.ps1"));
        var architecture = File.ReadAllText(FindRepositoryFile("docs", "ARCHITECTURE.md"));
        var license = File.ReadAllText(FindRepositoryFile("LICENSE"));

        Assert.StartsWith("# llama.cpp Console", readme, StringComparison.Ordinal);
        Assert.Contains("unofficial community project", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LlamaCppConsole.exe", readme, StringComparison.Ordinal);
        Assert.Contains("MIT License", license, StringComparison.Ordinal);
        Assert.Contains("LLAMA_CPP_CONSOLE_DOTNET", buildScript, StringComparison.Ordinal);
        Assert.Contains("LlamaCppConsole-$Runtime", publishScript, StringComparison.Ordinal);
        Assert.Contains("sha256", publishScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LlamaCppConsole.exe", startScript, StringComparison.Ordinal);
        Assert.Contains("LLAMA_CPP_CONSOLE_DOTNET", publishScript, StringComparison.Ordinal);
        Assert.Contains("LLAMA_CPP_CONSOLE_WORKSPACE", architecture, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow.RuntimeJobLogPreview.cs", architecture, StringComparison.Ordinal);
        Assert.DoesNotContain("# Local LLM Console", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("Local LLM Console", buildScript, StringComparison.Ordinal);
        Assert.DoesNotContain("Local LLM Console", publishScript, StringComparison.Ordinal);
        Assert.DoesNotContain("Local LLM Console", startScript, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryDefinesAutomatedCiGate()
    {
        var workflow = File.ReadAllText(FindRepositoryFile(".github", "workflows", "ci.yml"));
        var globalJson = File.ReadAllText(FindRepositoryFile("global.json"));
        var editorConfig = File.ReadAllText(FindRepositoryFile(".editorconfig"));
        var solution = File.ReadAllText(FindRepositoryFile("LocalLlmConsole.sln"));

        Assert.Contains("windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\build-app.ps1 -Restore", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\test-app.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains(".\\test-vulnerabilities.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("checksum was not produced", workflow, StringComparison.Ordinal);
        Assert.Contains("package --vulnerable --include-transitive --format json", File.ReadAllText(FindRepositoryFile("test-vulnerabilities.ps1")), StringComparison.Ordinal);
        Assert.Contains(".\\publish-app.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("\"version\": \"8.0.421\"", globalJson, StringComparison.Ordinal);
        Assert.Contains("TreatWarningsAsErrors", File.ReadAllText(FindRepositoryFile("Directory.Build.props")), StringComparison.Ordinal);
        Assert.Contains("root = true", editorConfig, StringComparison.Ordinal);
        Assert.Contains("LocalLlmConsole.App.csproj", solution, StringComparison.Ordinal);
        Assert.Contains("LocalLlmConsole.Tests.csproj", solution, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerKeepsUserDataUnlessExplicitlyRequested()
    {
        var installer = File.ReadAllText(FindRepositoryFile("installer", "LlamaCppConsole.iss"));
        var buildInstaller = File.ReadAllText(FindRepositoryFile("build-installer.ps1"));
        var installerDocs = File.ReadAllText(FindRepositoryFile("docs", "INSTALLER.md"));
        var readme = File.ReadAllText(FindRepositoryFile("README.md"));
        var releaseReadiness = File.ReadAllText(FindRepositoryFile("docs", "RELEASE_READINESS.md"));

        Assert.Contains("AppId={{5C6D440C-0EE0-4FEC-8D86-6AADEAA24620}", installer, StringComparison.Ordinal);
        Assert.Contains("DefaultDirName={code:GetDefaultDirName}", installer, StringComparison.Ordinal);
        Assert.Contains(@"D:\LlamaCppConsole", installer, StringComparison.Ordinal);
        Assert.Contains(@"DirExists('D:\')", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("IsWritableDirectory", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveStringToFile", installer, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesAllowed=x64compatible", installer, StringComparison.Ordinal);
        Assert.Contains(@"Source: ""{#SourceDir}\{#AppExeName}""", installer, StringComparison.Ordinal);
        Assert.DoesNotContain(@"Source: ""{#SourceDir}\*""", installer, StringComparison.Ordinal);
        Assert.Contains(@"%LocalAppData%\Programs\LlamaCppConsole", installerDocs, StringComparison.Ordinal);
        Assert.Contains("UsePreviousAppDir=yes", installer, StringComparison.Ordinal);
        Assert.Contains("AppMutex=Local\\llama.cpp-console-single-instance", installer, StringComparison.Ordinal);
        Assert.Contains("postinstall", installer, StringComparison.Ordinal);
        Assert.Contains("InitializeUninstall", installer, StringComparison.Ordinal);
        Assert.Contains("DeleteAppDataOnUninstall := False", installer, StringComparison.Ordinal);
        Assert.Contains("MB_DEFBUTTON2", installer, StringComparison.Ordinal);
        Assert.Contains("DelTree(ExpandConstant('{app}\\data')", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("[UninstallDelete]", installer, StringComparison.Ordinal);
        Assert.DoesNotContain(@"{app}\data\*", installer, StringComparison.Ordinal);

        Assert.Contains("publish-app.ps1", buildInstaller, StringComparison.Ordinal);
        Assert.Contains("ISCC.exe", buildInstaller, StringComparison.Ordinal);
        Assert.Contains("LLAMA_CPP_CONSOLE_INNO_SETUP", buildInstaller, StringComparison.Ordinal);
        Assert.Contains("Programs\\Inno Setup 6\\ISCC.exe", buildInstaller, StringComparison.Ordinal);
        Assert.Contains("Set-AuthenticodeSignature", buildInstaller, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", buildInstaller, StringComparison.Ordinal);
        Assert.Contains("LlamaCppConsole-Setup-$AppVersion-$Runtime.exe", buildInstaller, StringComparison.Ordinal);
        Assert.Contains("build-installer.ps1", readme, StringComparison.Ordinal);
        Assert.Contains("Uninstall keeps `data` by default", installerDocs, StringComparison.Ordinal);
        Assert.Contains("Any installer uninstall, repair, or update path that deletes models", releaseReadiness, StringComparison.Ordinal);
    }


    [Fact]
    public void AppUpdateServiceParsesGithubReleaseAndAsset()
    {
        var release = System.Text.Json.Nodes.JsonNode.Parse("""
        {
          "tag_name": "v1.1.0",
          "name": "v1.1",
          "body": "Added update checks.",
          "html_url": "https://github.com/alekk89/llama.cpp-Console/releases/tag/v1.1.0",
          "assets": [
            { "name": "notes.txt", "browser_download_url": "https://example.invalid/notes.txt", "size": 10 },
            { "name": "LlamaCppConsole-win-x64.zip", "browser_download_url": "https://example.invalid/app.zip", "size": 1234 },
            { "name": "LlamaCppConsole-win-x64.zip.sha256", "browser_download_url": "https://example.invalid/app.zip.sha256", "size": 64 }
          ]
        }
        """)!.AsObject();

        var update = AppUpdateService.ParseLatestRelease(release, "v1.0");

        Assert.True(update.IsAvailable);
        Assert.Equal("v1.0", update.CurrentVersion);
        Assert.Equal("v1.1.0", update.LatestVersion);
        Assert.Equal("LlamaCppConsole-win-x64.zip", update.AssetName);
        Assert.Equal("https://example.invalid/app.zip", update.AssetUrl);
        Assert.Equal("LlamaCppConsole-win-x64.zip.sha256", update.ChecksumAssetName);
        Assert.Equal("https://example.invalid/app.zip.sha256", update.ChecksumAssetUrl);
        Assert.Contains("update checks", update.ReleaseNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AppUpdateServiceChecksConfiguredGithubReleaseEndpoint()
    {
        using var handler = new CapturingHttpHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var service = new AppUpdateService(http);

        var update = await service.CheckLatestAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://api.github.com/repos/alekk89/llama.cpp-Console/releases/latest", handler.RequestUri?.ToString());
        Assert.False(update.IsAvailable);
        Assert.Contains("No GitHub release feed", update.ReleaseNotes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppUpdateServiceExtractsChecksumForSelectedAsset()
    {
        const string hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var exact = AppUpdateService.ExtractSha256($"{hash}  LlamaCppConsole-win-x64.zip", "LlamaCppConsole-win-x64.zip");
        var unrelated = AppUpdateService.ExtractSha256($"{hash}  other.zip", "LlamaCppConsole-win-x64.zip");

        Assert.Equal(hash, exact);
        Assert.Equal("", unrelated);
    }

    [Fact]
    public async Task AppUpdateServiceRequiresChecksumBeforeStaging()
    {
        var temp = CreateTempRoot();
        var service = new AppUpdateService(new HttpClient());
        var update = new AppUpdateInfo(
            true,
            "v1.0",
            "v1.1.0",
            "v1.1.0",
            "",
            "https://example.invalid/release",
            "LlamaCppConsole.exe",
            "https://example.invalid/LlamaCppConsole.exe",
            1024 * 1024);

        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.StageInstallAsync(update, temp, Path.Combine(temp, "LlamaCppConsole.exe"), TestContext.Current.CancellationToken));

            Assert.Contains("SHA-256 companion", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }

    private sealed class CapturingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

}
