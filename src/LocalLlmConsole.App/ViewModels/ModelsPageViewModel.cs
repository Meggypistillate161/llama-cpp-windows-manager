using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class ModelsPageViewModel
{
    public ObservableCollection<ModelGridRow> Rows { get; } = new();
    public ObservableCollection<ModelGridRow> VariantRows { get; } = new();

    public void ReplaceModels(
        IEnumerable<ModelRecord> models,
        Func<ModelRecord, bool> isModelActive,
        Func<ModelRecord, ModelLaunchSettings?>? launchProfileForModel = null)
    {
        var allModels = models.ToArray();
        Rows.Clear();
        VariantRows.Clear();
        foreach (var model in allModels.Where(model => !ModelAliasService.IsLaunchAlias(model)))
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

        foreach (var model in allModels.Where(ModelAliasService.IsLaunchAlias))
        {
            var active = isModelActive(model);
            var profile = launchProfileForModel?.Invoke(model);
            VariantRows.Add(new ModelGridRow
            {
                Name = model.Name,
                Quant = "Variant",
                Size = ModelSizeLabel(model.ModelPath),
                BaseModel = ModelAliasService.BaseModelName(model, allModels),
                Port = profile is null ? "Auto" : profile.Port.ToString(CultureInfo.InvariantCulture),
                DeleteAction = "Remove",
                DeleteToolTip = active
                    ? "Unload this saved variant before removing it."
                    : "Remove this saved model variant. The GGUF file is kept.",
                CanDelete = !active,
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
