namespace LocalLlmConsole.Services;

public sealed class ModelGatewayRequestBodyTooLargeException : InvalidOperationException
{
    public ModelGatewayRequestBodyTooLargeException(string message) : base(message)
    {
    }
}

public static class ModelGatewayRequestBodyReader
{
    private const int BufferSize = 81920;

    public static byte[] ReadBodyBuffer(Stream stream, long contentLength, long maxBytes)
    {
        if (maxBytes <= 0)
            throw new InvalidOperationException("Gateway request body limit must be greater than zero.");
        if (contentLength > maxBytes)
            throw new ModelGatewayRequestBodyTooLargeException($"Gateway request body is too large. Limit is {DisplayFormatService.Bytes(maxBytes)}.");

        using var memory = contentLength is > 0 and <= int.MaxValue
            ? new MemoryStream((int)contentLength)
            : new MemoryStream();
        var buffer = new byte[BufferSize];
        long total = 0;
        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            total += read;
            if (total > maxBytes)
                throw new ModelGatewayRequestBodyTooLargeException($"Gateway request body is too large. Limit is {DisplayFormatService.Bytes(maxBytes)}.");
            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }
}
