using System.Collections.Concurrent;

namespace LocalLlmConsole.Services;

public sealed class BoundedLogWriter : IDisposable
{
    private readonly FileStream _stream;
    private readonly long _maxBytes;
    private readonly object _gate = new();

    public BoundedLogWriter(string path, long maxBytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _maxBytes = Math.Max(0, maxBytes);
    }

    public void WriteLine(string line)
    {
        lock (_gate)
            BoundedLogFile.WriteToStream(_stream, line + Environment.NewLine, _maxBytes);
    }

    public void Dispose() => _stream.Dispose();
}

public static class BoundedLogFile
{
    private const string ResetMarker = "[log limit reached; overwriting from beginning]";
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly ConcurrentDictionary<string, object> Locks = new(StringComparer.OrdinalIgnoreCase);

    public static long MegabytesToBytes(int megabytes)
        => megabytes <= 0 ? 0 : megabytes * 1024L * 1024L;

    public static async Task AppendAsync(string path, string text, long maxBytes)
    {
        await Task.Run(() => Append(path, text, maxBytes));
    }

    public static void Append(string path, string text, long maxBytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var gate = Locks.GetOrAdd(Path.GetFullPath(path), _ => new object());
        lock (gate)
        {
            using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            WriteToStream(stream, text, Math.Max(0, maxBytes));
        }
    }

    internal static void WriteToStream(FileStream stream, string text, long maxBytes)
    {
        if (maxBytes <= 0)
        {
            stream.Seek(0, SeekOrigin.End);
            WriteUtf8(stream, text);
            return;
        }

        var bytes = Utf8.GetBytes(text);
        if (bytes.LongLength > maxBytes)
        {
            stream.SetLength(0);
            stream.Position = 0;
            WriteBytes(stream, TailBytes(text, maxBytes));
            return;
        }

        if (stream.Length + bytes.LongLength > maxBytes)
        {
            var resetText = ResetMarker + Environment.NewLine + text;
            var resetBytes = Utf8.GetByteCount(resetText) <= maxBytes
                ? Utf8.GetBytes(resetText)
                : TailBytes(resetText, maxBytes);
            stream.SetLength(0);
            stream.Position = 0;
            WriteBytes(stream, resetBytes);
            return;
        }

        stream.Seek(0, SeekOrigin.End);
        WriteBytes(stream, bytes);
    }

    private static byte[] TailBytes(string text, long maxBytes)
    {
        var capped = (int)Math.Min(maxBytes, int.MaxValue);
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (Utf8.GetByteCount(text.AsSpan(mid)) > capped)
                low = mid + 1;
            else
                high = mid;
        }
        return Utf8.GetBytes(text[low..]);
    }

    private static void WriteUtf8(FileStream stream, string text)
        => WriteBytes(stream, Utf8.GetBytes(text));

    private static void WriteBytes(FileStream stream, byte[] bytes)
    {
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }
}
