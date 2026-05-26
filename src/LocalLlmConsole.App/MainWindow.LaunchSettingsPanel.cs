using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfTextBox = System.Windows.Controls.TextBox;
namespace LocalLlmConsole;

public partial class MainWindow
{
    private UIElement CreateLaunchSettingsPanel()
    {
        _launchSettingElements.Clear();
        _advancedLaunchSections.Clear();
        var panel = new StackPanel();

        var runtimeGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        runtimeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
        runtimeGrid.ColumnDefinitions.Add(new ColumnDefinition());
        runtimeGrid.Children.Add(new TextBlock
        {
            Text = "Runtime",
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 2)
        });
        _runtimeCombo = new WpfComboBox
        {
            ItemsSource = _viewModel.LaunchSettings.RuntimeChoices,
            DisplayMemberPath = nameof(RuntimeChoice.Label),
            SelectedValuePath = nameof(RuntimeChoice.Id),
            MinHeight = 29,
            Margin = new Thickness(0, 0, 4, 2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("llama.cpp runtime used when starting or restarting the selected model.")
        };
        _runtimeCombo.SelectionChanged += (_, _) =>
        {
            UpdateLaunchControlVisibility();
            UpdateLaunchSaveButtonState();
        };
        Grid.SetColumn(_runtimeCombo, 1);
        runtimeGrid.Children.Add(_runtimeCombo);
        panel.Children.Add(runtimeGrid);

        _modelCapabilityText = Text("No model selected", 12, false, true);
        _modelCapabilityText.TextWrapping = TextWrapping.NoWrap;
        _modelCapabilityText.TextTrimming = TextTrimming.CharacterEllipsis;
        _modelCapabilityText.Margin = new Thickness(0, 0, 0, 4);
        panel.Children.Add(_modelCapabilityText);

        _advancedLaunchSettingsToggle = new WpfCheckBox
        {
            Content = "Advanced settings",
            IsChecked = _showAdvancedLaunchSettings,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"],
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = TooltipText("Shows tuning controls for memory, RoPE, speculative/MTP decoding, and sampling.")
        };
        _advancedLaunchSettingsToggle.Checked += (_, _) =>
        {
            _showAdvancedLaunchSettings = true;
            UpdateLaunchControlVisibility();
        };
        _advancedLaunchSettingsToggle.Unchecked += (_, _) =>
        {
            _showAdvancedLaunchSettings = false;
            UpdateLaunchControlVisibility();
        };
        panel.Children.Add(_advancedLaunchSettingsToggle);

        var basicGrid = LaunchSettingsGrid();
        _contextSizeBox = LaunchTextBox(_settings.ContextSize);
        AddLaunchSetting(basicGrid, "Context size", _contextSizeBox);
        _threadsBox = LaunchTextBox(_settings.Threads);
        AddLaunchSetting(basicGrid, "Threads", _threadsBox);
        _gpuLayersBox = LaunchTextBox(_settings.GpuLayers);
        AddLaunchSetting(basicGrid, "GPU layers", _gpuLayersBox);
        panel.Children.Add(LaunchSection("Basic Launch", basicGrid));

        var memoryGrid = LaunchSettingsGrid();
        _batchSizeBox = LaunchTextBox(_settings.BatchSize);
        AddLaunchSetting(memoryGrid, "Batch size", _batchSizeBox);
        _microBatchSizeBox = LaunchTextBox(_settings.MicroBatchSize);
        AddLaunchSetting(memoryGrid, "Micro batch", _microBatchSizeBox);
        _flashAttentionCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        AddLaunchSetting(memoryGrid, "Flash attention", _flashAttentionCombo);
        _cacheTypeKCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        AddLaunchSetting(memoryGrid, "K cache", _cacheTypeKCombo);
        _cacheTypeVCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        AddLaunchSetting(memoryGrid, "V cache", _cacheTypeVCombo);
        _kvOffloadCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        AddAdvancedLaunchSetting(memoryGrid, "KV offload", _kvOffloadCombo);
        _kvUnifiedCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        AddAdvancedLaunchSetting(memoryGrid, "Unified KV", _kvUnifiedCombo);
        _mmapCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        AddAdvancedLaunchSetting(memoryGrid, "Memory map", _mmapCombo);
        _mlockCombo = LaunchCombo(LaunchSettingMetadataService.OffOnOptions);
        AddAdvancedLaunchSetting(memoryGrid, "Memory lock", _mlockCombo);
        panel.Children.Add(LaunchSection("Performance & Memory", memoryGrid));

        var speculativeGrid = LaunchSettingsGrid();
        _speculativeTypeCombo = LaunchCombo(LaunchSettingMetadataService.SpeculativeTypeOptions);
        AddLaunchSetting(speculativeGrid, "Spec type", _speculativeTypeCombo);
        _specDraftModelPathBox = LaunchTextBox(_settings.SpecDraftModelPath);
        AddLaunchSetting(speculativeGrid, "Draft model", _specDraftModelPathBox);
        _specDraftCacheTypeKCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        AddLaunchSetting(speculativeGrid, "Draft K cache", _specDraftCacheTypeKCombo);
        _specDraftCacheTypeVCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        AddLaunchSetting(speculativeGrid, "Draft V cache", _specDraftCacheTypeVCombo);
        _specDraftMaxTokensBox = LaunchTextBox(_settings.SpecDraftMaxTokens);
        AddLaunchSetting(speculativeGrid, "Draft max", _specDraftMaxTokensBox);
        _specDraftMinTokensBox = LaunchTextBox(_settings.SpecDraftMinTokens);
        AddLaunchSetting(speculativeGrid, "Draft min", _specDraftMinTokensBox);
        _specDraftGpuLayersBox = LaunchTextBox(_settings.SpecDraftGpuLayers);
        AddAdvancedLaunchSetting(speculativeGrid, "Draft GPU", _specDraftGpuLayersBox);
        _specDraftPSplitBox = LaunchTextBox(_settings.SpecDraftPSplit);
        AddAdvancedLaunchSetting(speculativeGrid, "Split prob", _specDraftPSplitBox);
        _specDraftPMinBox = LaunchTextBox(_settings.SpecDraftPMin);
        AddAdvancedLaunchSetting(speculativeGrid, "Min prob", _specDraftPMinBox);
        panel.Children.Add(LaunchSection("Speculative / MTP", speculativeGrid));

        var chatGrid = LaunchSettingsGrid();
        _reasoningCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        AddLaunchSetting(chatGrid, "Reasoning", _reasoningCombo);
        _reasoningFormatCombo = LaunchCombo(LaunchSettingMetadataService.ReasoningFormatOptions);
        AddLaunchSetting(chatGrid, "Reason format", _reasoningFormatCombo);
        _reasoningBudgetBox = LaunchTextBox(_settings.ReasoningBudget);
        AddLaunchSetting(chatGrid, "Reason budget", _reasoningBudgetBox);
        _jinjaCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        AddLaunchSetting(chatGrid, "Jinja chat", _jinjaCombo);
        _visionCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        AddLaunchSetting(chatGrid, "Vision", _visionCombo);
        _visionImageMinTokensBox = LaunchTextBox(_settings.VisionImageMinTokens);
        AddLaunchSetting(chatGrid, "Image min", _visionImageMinTokensBox);
        _visionImageMaxTokensBox = LaunchTextBox(_settings.VisionImageMaxTokens);
        AddLaunchSetting(chatGrid, "Image max", _visionImageMaxTokensBox);
        panel.Children.Add(LaunchSection("Chat & Model Capabilities", chatGrid));

        var generationGrid = LaunchSettingsGrid();
        _temperatureBox = LaunchTextBox(_settings.Temperature);
        AddLaunchSetting(generationGrid, "Temperature", _temperatureBox);
        _topKBox = LaunchTextBox(_settings.TopK);
        AddLaunchSetting(generationGrid, "Top K", _topKBox);
        _topPBox = LaunchTextBox(_settings.TopP);
        AddLaunchSetting(generationGrid, "Top P", _topPBox);
        _minPBox = LaunchTextBox(_settings.MinP);
        AddLaunchSetting(generationGrid, "Min P", _minPBox);
        _maxTokensBox = LaunchTextBox(_settings.MaxTokens);
        AddAdvancedLaunchSetting(generationGrid, "Max tokens", _maxTokensBox);
        _seedBox = LaunchTextBox(_settings.Seed);
        AddAdvancedLaunchSetting(generationGrid, "Seed", _seedBox);
        _repeatLastNBox = LaunchTextBox(_settings.RepeatLastN);
        AddAdvancedLaunchSetting(generationGrid, "Repeat window", _repeatLastNBox);
        _repeatPenaltyBox = LaunchTextBox(_settings.RepeatPenalty);
        AddAdvancedLaunchSetting(generationGrid, "Repeat pen", _repeatPenaltyBox);
        _presencePenaltyBox = LaunchTextBox(_settings.PresencePenalty);
        AddAdvancedLaunchSetting(generationGrid, "Presence", _presencePenaltyBox);
        _frequencyPenaltyBox = LaunchTextBox(_settings.FrequencyPenalty);
        AddAdvancedLaunchSetting(generationGrid, "Frequency", _frequencyPenaltyBox);
        panel.Children.Add(LaunchSection("Generation Defaults", generationGrid));

        var ropeGrid = LaunchSettingsGrid();
        _ropeScalingCombo = LaunchCombo(LaunchSettingMetadataService.RopeScalingOptions);
        AddLaunchSetting(ropeGrid, "RoPE scaling", _ropeScalingCombo);
        _ropeScaleBox = LaunchTextBox(_settings.RopeScale);
        AddLaunchSetting(ropeGrid, "RoPE scale", _ropeScaleBox);
        _ropeFreqBaseBox = LaunchTextBox(_settings.RopeFreqBase);
        AddLaunchSetting(ropeGrid, "RoPE base", _ropeFreqBaseBox);
        _ropeFreqScaleBox = LaunchTextBox(_settings.RopeFreqScale);
        AddLaunchSetting(ropeGrid, "RoPE freq", _ropeFreqScaleBox);
        var ropeSection = LaunchSection("Context Extension", ropeGrid);
        _advancedLaunchSections.Add(ropeSection);
        panel.Children.Add(ropeSection);

        var serverGrid = LaunchSettingsGrid();
        _parallelSlotsBox = LaunchTextBox(_settings.ParallelSlots);
        AddLaunchSetting(serverGrid, "Parallel slots", _parallelSlotsBox);
        _continuousBatchingCombo = LaunchCombo(LaunchSettingMetadataService.OnOffOptions);
        AddLaunchSetting(serverGrid, "Continuous batch", _continuousBatchingCombo);
        _metricsCombo = LaunchCombo(LaunchSettingMetadataService.OnOffOptions);
        AddLaunchSetting(serverGrid, "Metrics", _metricsCombo);
        var serverSection = LaunchSection("Server", serverGrid);
        _advancedLaunchSections.Add(serverSection);
        panel.Children.Add(serverSection);

        var actions = Bar();
        _saveModelLaunchSettingsButton = Button("Save For Model", async (_, _) => await SaveLaunchSettingsForSelectedModelAsync());
        actions.Children.Add(_saveModelLaunchSettingsButton);
        actions.Children.Add(Button("Save As Default", async (_, _) => await SaveLaunchDefaultsFromControlsAsync()));
        actions.Children.Add(Button("Reset Defaults", (_, _) => ResetLaunchSettingsToDefaults()));
        panel.Children.Add(actions);

        AttachLaunchSettingsChangeHandlers();
        ApplyLaunchSettingsToControls();
        RunBackground(() => RenderSelectedModelLaunchSettingsAsync(), "Launch settings refresh failed");
        return new Border
        {
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["InputBack"],
            BorderBrush = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0),
            MinHeight = 220,
            Child = Scroll(panel, new Thickness(9, 8, 7, 8))
        };
    }
}
