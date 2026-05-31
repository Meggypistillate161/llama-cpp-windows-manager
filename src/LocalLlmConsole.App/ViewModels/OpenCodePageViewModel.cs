using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class OpenCodePageViewModel
{
    public ObservableCollection<OpenCodeModelEntry> ModelChoices { get; } = new();
    public ObservableCollection<ModelRecord> LocalModelChoices { get; } = new();
    public ObservableCollection<OpenCodeAgentEntry> AgentChoices { get; } = new();

    public void ReplaceChoices(OpenCodePageChoices choices)
    {
        LocalModelChoices.Clear();
        foreach (var model in choices.LocalModels)
            LocalModelChoices.Add(model);

        ModelChoices.Clear();
        foreach (var model in choices.Models)
            ModelChoices.Add(model);

        AgentChoices.Clear();
        foreach (var agent in choices.Agents)
            AgentChoices.Add(agent);
    }

    public void ReplaceLocalModels(IEnumerable<ModelRecord> models)
    {
        LocalModelChoices.Clear();
        foreach (var model in models.OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase))
            LocalModelChoices.Add(model);
    }

    public void ReplaceModels(IEnumerable<OpenCodeModelEntry> models)
    {
        ModelChoices.Clear();
        foreach (var model in models)
            ModelChoices.Add(model);
        ModelChoices.Add(new OpenCodeModelEntry("", "", "", "Add New...", IsAddNew: true));
    }

    public void ReplaceAgents(IEnumerable<OpenCodeAgentEntry> agents)
    {
        AgentChoices.Clear();
        foreach (var agent in agents)
            AgentChoices.Add(agent);
        AgentChoices.Add(new OpenCodeAgentEntry("", "", OpenCodeAgentKind.Config, "", "Add New...", IsAddNew: true));
    }
}
