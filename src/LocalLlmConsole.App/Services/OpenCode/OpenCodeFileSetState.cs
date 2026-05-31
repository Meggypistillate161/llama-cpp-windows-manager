namespace LocalLlmConsole;

public sealed class OpenCodeFileSetState
{
    private OpenCodeFileSet _current = new("", "");

    public OpenCodeFileSet Current => _current;

    public string ConfigPath => _current.ConfigPath;

    public string AgentsDirectory => _current.AgentsDirectory;

    public OpenCodeFileSet Set(OpenCodeFileSet fileSet)
    {
        ArgumentNullException.ThrowIfNull(fileSet);

        _current = fileSet;
        return _current;
    }
}
