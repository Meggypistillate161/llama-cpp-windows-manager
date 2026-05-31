namespace LocalLlmConsole.Services;

public static class VisionProjectorSelection
{
    public const string EmbeddedToken = "<embedded>";

    public static bool IsAuto(string? value)
        => string.IsNullOrWhiteSpace(value);

    public static bool IsEmbedded(string? value)
    {
        var normalized = (value ?? "").Trim();
        return string.Equals(normalized, EmbeddedToken, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "embedded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "model-bundled", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExternal(string? value)
        => !IsAuto(value) && !IsEmbedded(value);

    public static bool IsEmbeddedOrMainModel(string modelPath, string? value)
    {
        if (IsEmbedded(value)) return true;
        if (!IsExternal(value)) return false;

        try
        {
            return string.Equals(Path.GetFullPath(modelPath), Path.GetFullPath(value!.Trim()), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string DisplayText(string? value)
    {
        if (IsEmbedded(value)) return "Embedded vision";
        if (IsAuto(value)) return "Auto-detect vision head";

        var fileName = Path.GetFileName(value!.Trim());
        return string.IsNullOrWhiteSpace(fileName) ? "External vision head" : fileName;
    }

    public static string Tooltip(string? value)
    {
        if (IsEmbedded(value))
            return "Use vision support bundled in the selected model GGUF. llama-server must support embedded multimodal data for this model.";
        if (IsAuto(value))
            return "Auto-detect a nearby mmproj/projector GGUF file when the model is launched.";

        return $"External vision head: {value!.Trim()}{Environment.NewLine}Click to change the vision head source.";
    }
}
