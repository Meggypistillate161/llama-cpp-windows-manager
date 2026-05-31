namespace LocalLlmConsole.Services;

public static class ConfigFileSafetyService
{
    public static void WriteTextWithBackup(string path, string text, Encoding encoding, string label)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        BackupBeforeOverwrite(path, label);
        File.WriteAllText(path, text, encoding);
    }

    public static void BackupBeforeDelete(string path, string label)
    {
        RejectReparsePointFile(path, label);
        BackupExistingFile(path);
    }

    public static void BackupBeforeOverwrite(string path, string label)
    {
        RejectReparsePointFile(path, label);
        BackupExistingFile(path);
    }

    public static void RejectReparsePointFile(string path, string label)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException($"Refusing to modify {label} because it is a symlink or junction: {path}");
    }

    private static void BackupExistingFile(string path)
    {
        if (!File.Exists(path)) return;
        RejectReparsePointFile(path, "config file");
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) return;

        var backupDirectory = Path.Combine(directory, ".local-llm-console-backups");
        Directory.CreateDirectory(backupDirectory);
        var backupName = $"{Path.GetFileName(path)}.{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.bak";
        File.Copy(path, Path.Combine(backupDirectory, backupName), overwrite: false);
    }
}
