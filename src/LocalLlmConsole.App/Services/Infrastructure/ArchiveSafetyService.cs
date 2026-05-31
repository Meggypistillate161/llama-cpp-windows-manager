using System.Formats.Tar;
using System.IO.Compression;

namespace LocalLlmConsole.Services;

public static class ArchiveSafetyService
{
    public static void ValidateZipArchiveEntries(string archivePath, string destination)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
            ValidateArchiveEntryPath(entry.FullName, destination);
    }

    public static void ValidateTarGzipArchiveEntries(string archivePath, string destination)
    {
        using var file = File.OpenRead(archivePath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry(copyData: false)) is not null)
            ValidateTarArchiveEntry(entry, destination);
    }

    public static void ValidateArchiveEntryPath(string entryName, string destination)
    {
        if (ArchiveTargetPath(entryName, destination, allowDotSegments: false) is null) return;
    }

    private static void ValidateTarArchiveEntry(TarEntry entry, string destination)
    {
        if (entry.EntryType is TarEntryType.ExtendedAttributes
            or TarEntryType.GlobalExtendedAttributes
            or TarEntryType.LongLink
            or TarEntryType.LongPath)
        {
            return;
        }

        ValidateArchiveEntryPath(entry.Name, destination);

        if (entry.EntryType is TarEntryType.SymbolicLink or TarEntryType.HardLink)
        {
            ValidateTarLinkTarget(entry, destination);
            return;
        }

        if (entry.EntryType is TarEntryType.BlockDevice
            or TarEntryType.CharacterDevice
            or TarEntryType.Fifo
            or TarEntryType.MultiVolume
            or TarEntryType.RenamedOrSymlinked
            or TarEntryType.SparseFile
            or TarEntryType.TapeVolume)
        {
            throw new InvalidOperationException($"Archive entry uses an unsupported tar entry type {entry.EntryType}: {entry.Name}");
        }
    }

    private static void ValidateTarLinkTarget(TarEntry entry, string destination)
    {
        var root = ArchiveRoot(destination);
        var entryTarget = ArchiveTargetPath(entry.Name, destination, allowDotSegments: false) ?? root;
        var linkBase = entry.EntryType == TarEntryType.HardLink
            ? root
            : Path.GetDirectoryName(entryTarget) ?? root;
        var linkTarget = ArchiveTargetPath(entry.LinkName, linkBase, allowDotSegments: true);
        if (linkTarget is null)
            throw new InvalidOperationException($"Archive link entry has an empty target: {entry.Name}");

        EnsureInsideRoot(linkTarget, root, entry.LinkName);
    }

    private static string? ArchiveTargetPath(string entryName, string destination, bool allowDotSegments)
    {
        var segments = ArchivePathSegments(entryName, allowDotSegments);
        if (segments.Length == 0) return null;

        var root = ArchiveRoot(destination);
        var target = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        EnsureInsideRoot(target, root, entryName);
        return target;
    }

    private static string[] ArchivePathSegments(string entryName, bool allowDotSegments)
    {
        var name = (entryName ?? "").Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(name)) return [];
        if (name.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(name))
            throw new InvalidOperationException($"Archive entry uses an absolute path: {entryName}");

        var segments = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => (!allowDotSegments && segment is "." or "..")
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new InvalidOperationException($"Archive entry uses an unsafe path: {entryName}");
        }

        return segments;
    }

    private static string ArchiveRoot(string destination)
        => Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void EnsureInsideRoot(string target, string root, string entryName)
    {
        if (target.Equals(root, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException($"Archive entry would extract outside the destination folder: {entryName}");
    }
}
