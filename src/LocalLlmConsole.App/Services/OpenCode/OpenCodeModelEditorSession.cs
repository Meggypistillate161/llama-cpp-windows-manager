namespace LocalLlmConsole.Services;

public sealed class OpenCodeModelEditorSession
{
    public string SavedSnippet { get; private set; } = "";

    public bool IsProgrammaticUpdate { get; private set; }

    public void ClearSavedSnippet()
        => SavedSnippet = "";

    public void SetSavedSnippet(string snippet)
        => SavedSnippet = snippet ?? "";

    public void RunProgrammaticUpdate(Action update)
    {
        ArgumentNullException.ThrowIfNull(update);
        IsProgrammaticUpdate = true;
        try
        {
            update();
        }
        finally
        {
            IsProgrammaticUpdate = false;
        }
    }
}
