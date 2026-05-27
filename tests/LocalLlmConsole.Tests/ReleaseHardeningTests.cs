using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    private static RuntimeLaunchRequest ValidLaunchRequest() => new()
    {
        Mode = RuntimeMode.Native,
        Backend = RuntimeBackend.Cpu,
        ExecutablePath = "llama-server.exe",
        ModelPath = "model.gguf",
        Host = "127.0.0.1",
        ApiKey = new string('a', 32),
        Port = 8081
    };

    private static void WriteMinimalGguf(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("GGUF"));
        writer.Write((uint)3);
        writer.Write((ulong)0);
        writer.Write((ulong)3);
        WriteGgufString(writer, "general.architecture");
        writer.Write((uint)8);
        WriteGgufString(writer, "qwen3");
        WriteGgufString(writer, "qwen3.context_length");
        writer.Write((uint)4);
        writer.Write((uint)32768);
        WriteGgufString(writer, "tokenizer.chat_template");
        writer.Write((uint)8);
        WriteGgufString(writer, "{{ bos_token }}");
    }

    private static void WriteGgufString(BinaryWriter writer, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        writer.Write((ulong)bytes.Length);
        writer.Write(bytes);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "LocalLlmConsole.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(segments)}");
    }

    private static string ReadMainWindowSources()
    {
        var mainWindowPath = FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.xaml.cs");
        var appRoot = Path.GetDirectoryName(mainWindowPath)!;
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(appRoot, "MainWindow*.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
    }

    private static void AssertServicePartials(string appRoot, string folder, string prefix, int maxLines, params string[] requiredFiles)
    {
        var root = Path.Combine(appRoot, folder);
        var files = Directory.EnumerateFiles(root, $"{prefix}*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => new { Name = Path.GetFileName(path), Lines = File.ReadAllLines(path).Length })
            .ToArray();
        var names = files.Select(file => file.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var oversized = files
            .Where(file => file.Lines > maxLines)
            .Select(file => $"{file.Name}:{file.Lines}")
            .ToArray();

        Assert.Empty(oversized);
        foreach (var requiredFile in requiredFiles)
            Assert.Contains(requiredFile, names);
    }

}
