
namespace LocalLlmConsole.Services;

public static class FileOwnershipService
{
    public static void EnsureDeletionAllowed(ModelRecord model, string targetPath, string allowedRoot)
    {
        if (model.Ownership != OwnershipKind.AppOwned)
            throw new InvalidOperationException("Only app-owned files can be deleted automatically.");

        var root = Path.GetFullPath(allowedRoot);
        var target = Path.GetFullPath(targetPath);
        var relative = Path.GetRelativePath(root, target);
        if (relative.Length == 0
            || string.Equals(relative, ".", StringComparison.Ordinal)
            || string.Equals(relative, "..", StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
            throw new InvalidOperationException("Refusing to delete outside the app-owned storage root.");

        var modelPath = Path.GetFullPath(model.ModelPath);
        var modelRelative = Path.GetRelativePath(target, modelPath);
        if (Directory.Exists(target)
            && (string.IsNullOrWhiteSpace(modelRelative)
                || string.Equals(modelRelative, ".", StringComparison.Ordinal)
                || string.Equals(modelRelative, "..", StringComparison.Ordinal)
                || modelRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || modelRelative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathRooted(modelRelative)))
            throw new InvalidOperationException("Refusing to delete a folder that does not contain the app-owned model file.");

        if (ContainsReparsePointAncestor(root, target) || ContainsReparsePoint(target))
            throw new InvalidOperationException("Refusing to delete a path containing symlinks or junctions.");
    }

    private static bool ContainsReparsePointAncestor(string root, string target)
    {
        try
        {
            var current = Directory.Exists(target) ? target : Path.GetDirectoryName(target);
            while (!string.IsNullOrWhiteSpace(current)
                && Path.GetRelativePath(root, current) is var relative
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relative))
            {
                if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
                if (string.Equals(Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    return false;
                current = Path.GetDirectoryName(current);
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static bool ContainsReparsePoint(string target)
    {
        try
        {
            if (File.Exists(target) || Directory.Exists(target))
            {
                if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) return true;
            }

            if (!Directory.Exists(target)) return false;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = 0
            };
            foreach (var entry in Directory.EnumerateFileSystemEntries(target, "*", options))
            {
                try
                {
                    if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) return true;
                }
                catch
                {
                    return true;
                }
            }
        }
        catch
        {
            return true;
        }

        return false;
    }
}
