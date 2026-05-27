using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class ModelsPageViewModel
{
    public ObservableCollection<ModelGridRow> Rows { get; } = new();

    public void ReplaceModels(IEnumerable<ModelRecord> models, Func<ModelRecord, bool> isModelActive)
    {
        Rows.Clear();
        foreach (var model in models)
        {
            Rows.Add(new ModelGridRow
            {
                Name = model.Name,
                Quant = ModelCatalogService.InferQuant(model.ModelPath),
                Size = ModelSizeLabel(model.ModelPath),
                CanDelete = !isModelActive(model),
                DeleteToolTip = isModelActive(model)
                    ? "Unload this model before deleting it from disk."
                    : "Delete this model file and remove it from the catalog.",
                Model = model
            });
        }
    }

    private static string ModelSizeLabel(string modelPath)
    {
        try
        {
            return File.Exists(modelPath)
                ? DisplayFormatService.Bytes(new FileInfo(modelPath).Length)
                : "";
        }
        catch
        {
            return "";
        }
    }
}
