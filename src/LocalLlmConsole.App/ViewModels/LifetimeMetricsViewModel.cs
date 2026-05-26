using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class LifetimeMetricsViewModel
{
    public ObservableCollection<UiRow> Rows { get; } = new();

    public void ReplaceRows(IReadOnlyList<TokenUsageRecord> rows)
    {
        Rows.Clear();

        var totalPrompt = rows.Sum(row => row.PromptTokens);
        var totalGenerated = rows.Sum(row => row.GeneratedTokens);
        Rows.Add(new UiRow
        {
            C1 = "All models",
            C2 = totalPrompt.ToString("N0"),
            C3 = totalGenerated.ToString("N0"),
            C4 = (totalPrompt + totalGenerated).ToString("N0"),
            C5 = rows.Count == 0 ? "" : rows.Max(row => row.UpdatedAt).ToLocalTime().ToString("g"),
            C6 = "Reset All",
            T1 = "Reset lifetime token counters for all models.",
            B1 = rows.Count > 0,
            Data = new JsonObject { ["ModelId"] = "", ["ModelName"] = "All models", ["Kind"] = "total" }
        });

        foreach (var row in rows)
        {
            Rows.Add(new UiRow
            {
                C1 = row.ModelName,
                C2 = row.PromptTokens.ToString("N0"),
                C3 = row.GeneratedTokens.ToString("N0"),
                C4 = (row.PromptTokens + row.GeneratedTokens).ToString("N0"),
                C5 = row.UpdatedAt.ToLocalTime().ToString("g"),
                C6 = "Reset",
                T1 = "Reset lifetime token counters for this model.",
                B1 = true,
                Data = new JsonObject { ["ModelId"] = row.ModelId, ["ModelName"] = row.ModelName, ["Kind"] = "model" }
            });
        }
    }
}
