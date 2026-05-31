using System.Windows.Controls;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public static partial class LaunchSettingsPanelFactory
{
    private static LaunchSettingsFormControls AddLaunchSections(StackPanel panel, LaunchSettingsPanelBuilder builder, LaunchSettingsPanelRequest request, WpfTextBox launchPortBox)
    {
        var settings = request.Settings;
        var basicGrid = LaunchSettingsGrid();
        var contextSizeBox = LaunchTextBox(settings.ContextSize);
        builder.AddLaunchSetting(basicGrid, "Context size", contextSizeBox);
        var threadsBox = LaunchTextBox(settings.Threads);
        builder.AddLaunchSetting(basicGrid, "Threads", threadsBox);
        var gpuLayersBox = LaunchTextBox(settings.GpuLayers);
        builder.AddLaunchSetting(basicGrid, "GPU layers", gpuLayersBox);
        panel.Children.Add(LaunchSection("Basic Launch", basicGrid));

        var memoryGrid = LaunchSettingsGrid();
        var batchSizeBox = LaunchTextBox(settings.BatchSize);
        builder.AddLaunchSetting(memoryGrid, "Batch size", batchSizeBox);
        var microBatchSizeBox = LaunchTextBox(settings.MicroBatchSize);
        builder.AddLaunchSetting(memoryGrid, "Micro batch", microBatchSizeBox);
        var flashAttentionCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        builder.AddLaunchSetting(memoryGrid, "Flash attention", flashAttentionCombo);
        var cacheTypeKCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        builder.AddLaunchSetting(memoryGrid, "K cache", cacheTypeKCombo);
        var cacheTypeVCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        builder.AddLaunchSetting(memoryGrid, "V cache", cacheTypeVCombo);
        var kvOffloadCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        builder.AddAdvancedLaunchSetting(memoryGrid, "KV offload", kvOffloadCombo);
        var kvUnifiedCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        builder.AddAdvancedLaunchSetting(memoryGrid, "Unified KV", kvUnifiedCombo);
        var mmapCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        builder.AddAdvancedLaunchSetting(memoryGrid, "Memory map", mmapCombo);
        var mlockCombo = LaunchCombo(LaunchSettingMetadataService.OffOnOptions);
        builder.AddAdvancedLaunchSetting(memoryGrid, "Memory lock", mlockCombo);
        panel.Children.Add(LaunchSection("Performance & Memory", memoryGrid));

        var speculativeGrid = LaunchSettingsGrid();
        var speculativeTypeCombo = LaunchCombo(LaunchSettingMetadataService.SpeculativeTypeOptions);
        builder.AddLaunchSetting(speculativeGrid, "Spec type", speculativeTypeCombo);
        var specDraftModelPathBox = LaunchTextBox(settings.SpecDraftModelPath);
        builder.AddLaunchSetting(speculativeGrid, "Draft model", specDraftModelPathBox);
        var mtpHeadPathBox = LaunchTextBox(settings.MtpHeadPath);
        var mtpHeadPicker = MtpHeadPicker(mtpHeadPathBox, request.ChooseMtpHeadAsync, out var mtpHeadButton);
        builder.AddLaunchSetting(speculativeGrid, "MTP head", mtpHeadPicker);
        var specDraftCacheTypeKCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        builder.AddLaunchSetting(speculativeGrid, "Draft K cache", specDraftCacheTypeKCombo);
        var specDraftCacheTypeVCombo = LaunchCombo(LaunchSettingMetadataService.CacheTypeOptions);
        builder.AddLaunchSetting(speculativeGrid, "Draft V cache", specDraftCacheTypeVCombo);
        var specDraftMaxTokensBox = LaunchTextBox(settings.SpecDraftMaxTokens);
        builder.AddLaunchSetting(speculativeGrid, "Draft max", specDraftMaxTokensBox);
        var specDraftMinTokensBox = LaunchTextBox(settings.SpecDraftMinTokens);
        builder.AddLaunchSetting(speculativeGrid, "Draft min", specDraftMinTokensBox);
        var specDraftGpuLayersBox = LaunchTextBox(settings.SpecDraftGpuLayers);
        builder.AddAdvancedLaunchSetting(speculativeGrid, "Draft GPU", specDraftGpuLayersBox);
        var specDraftPSplitBox = LaunchTextBox(settings.SpecDraftPSplit);
        builder.AddAdvancedLaunchSetting(speculativeGrid, "Split prob", specDraftPSplitBox);
        var specDraftPMinBox = LaunchTextBox(settings.SpecDraftPMin);
        builder.AddAdvancedLaunchSetting(speculativeGrid, "Min prob", specDraftPMinBox);
        panel.Children.Add(LaunchSection("Speculative / MTP", speculativeGrid));

        var chatGrid = LaunchSettingsGrid();
        var reasoningCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        builder.AddLaunchSetting(chatGrid, "Reasoning", reasoningCombo);
        var reasoningFormatCombo = LaunchCombo(LaunchSettingMetadataService.ReasoningFormatOptions);
        builder.AddLaunchSetting(chatGrid, "Reason format", reasoningFormatCombo);
        var reasoningBudgetBox = LaunchTextBox(settings.ReasoningBudget);
        builder.AddLaunchSetting(chatGrid, "Reason budget", reasoningBudgetBox);
        var jinjaCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        builder.AddLaunchSetting(chatGrid, "Jinja chat", jinjaCombo);
        var visionCombo = LaunchCombo(LaunchSettingMetadataService.AutoOnOffOptions);
        builder.AddLaunchSetting(chatGrid, "Vision", visionCombo);
        var visionProjectorPathBox = LaunchTextBox(settings.VisionProjectorPath);
        var visionProjectorPicker = VisionProjectorPicker(visionProjectorPathBox, request.ChooseVisionProjectorAsync, out var visionProjectorButton);
        builder.AddLaunchSetting(chatGrid, "Vision head", visionProjectorPicker);
        var visionImageMinTokensBox = LaunchTextBox(settings.VisionImageMinTokens);
        builder.AddLaunchSetting(chatGrid, "Image min", visionImageMinTokensBox);
        var visionImageMaxTokensBox = LaunchTextBox(settings.VisionImageMaxTokens);
        builder.AddLaunchSetting(chatGrid, "Image max", visionImageMaxTokensBox);
        panel.Children.Add(LaunchSection("Chat & Model Capabilities", chatGrid));

        var generationGrid = LaunchSettingsGrid();
        var temperatureBox = LaunchTextBox(settings.Temperature);
        builder.AddLaunchSetting(generationGrid, "Temperature", temperatureBox);
        var topKBox = LaunchTextBox(settings.TopK);
        builder.AddLaunchSetting(generationGrid, "Top K", topKBox);
        var topPBox = LaunchTextBox(settings.TopP);
        builder.AddLaunchSetting(generationGrid, "Top P", topPBox);
        var minPBox = LaunchTextBox(settings.MinP);
        builder.AddLaunchSetting(generationGrid, "Min P", minPBox);
        var maxTokensBox = LaunchTextBox(settings.MaxTokens);
        builder.AddAdvancedLaunchSetting(generationGrid, "Max tokens", maxTokensBox);
        var seedBox = LaunchTextBox(settings.Seed);
        builder.AddAdvancedLaunchSetting(generationGrid, "Seed", seedBox);
        var repeatLastNBox = LaunchTextBox(settings.RepeatLastN);
        builder.AddAdvancedLaunchSetting(generationGrid, "Repeat window", repeatLastNBox);
        var repeatPenaltyBox = LaunchTextBox(settings.RepeatPenalty);
        builder.AddAdvancedLaunchSetting(generationGrid, "Repeat pen", repeatPenaltyBox);
        var presencePenaltyBox = LaunchTextBox(settings.PresencePenalty);
        builder.AddAdvancedLaunchSetting(generationGrid, "Presence", presencePenaltyBox);
        var frequencyPenaltyBox = LaunchTextBox(settings.FrequencyPenalty);
        builder.AddAdvancedLaunchSetting(generationGrid, "Frequency", frequencyPenaltyBox);
        panel.Children.Add(LaunchSection("Generation Defaults", generationGrid));

        var ropeGrid = LaunchSettingsGrid();
        var ropeScalingCombo = LaunchCombo(LaunchSettingMetadataService.RopeScalingOptions);
        builder.AddLaunchSetting(ropeGrid, "RoPE scaling", ropeScalingCombo);
        var ropeScaleBox = LaunchTextBox(settings.RopeScale);
        builder.AddLaunchSetting(ropeGrid, "RoPE scale", ropeScaleBox);
        var ropeFreqBaseBox = LaunchTextBox(settings.RopeFreqBase);
        builder.AddLaunchSetting(ropeGrid, "RoPE base", ropeFreqBaseBox);
        var ropeFreqScaleBox = LaunchTextBox(settings.RopeFreqScale);
        builder.AddLaunchSetting(ropeGrid, "RoPE freq", ropeFreqScaleBox);
        var ropeSection = LaunchSection("Context Extension", ropeGrid);
        builder.AddAdvancedSection(ropeSection);
        panel.Children.Add(ropeSection);

        var serverGrid = LaunchSettingsGrid();
        var parallelSlotsBox = LaunchTextBox(settings.ParallelSlots);
        builder.AddLaunchSetting(serverGrid, "Parallel slots", parallelSlotsBox);
        var continuousBatchingCombo = LaunchCombo(LaunchSettingMetadataService.OnOffOptions);
        builder.AddLaunchSetting(serverGrid, "Continuous batch", continuousBatchingCombo);
        var metricsCombo = LaunchCombo(LaunchSettingMetadataService.OnOffOptions);
        builder.AddLaunchSetting(serverGrid, "Metrics", metricsCombo);
        var serverSection = LaunchSection("Server", serverGrid);
        builder.AddAdvancedSection(serverSection);
        panel.Children.Add(serverSection);

        return new LaunchSettingsFormControls
        {
            LaunchPortBox = launchPortBox,
            ContextSizeBox = contextSizeBox,
            GpuLayersBox = gpuLayersBox,
            ParallelSlotsBox = parallelSlotsBox,
            BatchSizeBox = batchSizeBox,
            MicroBatchSizeBox = microBatchSizeBox,
            ThreadsBox = threadsBox,
            ReasoningBudgetBox = reasoningBudgetBox,
            VisionProjectorPathBox = visionProjectorPathBox,
            VisionImageMinTokensBox = visionImageMinTokensBox,
            VisionImageMaxTokensBox = visionImageMaxTokensBox,
            TemperatureBox = temperatureBox,
            TopKBox = topKBox,
            TopPBox = topPBox,
            MinPBox = minPBox,
            MaxTokensBox = maxTokensBox,
            SeedBox = seedBox,
            RepeatLastNBox = repeatLastNBox,
            RepeatPenaltyBox = repeatPenaltyBox,
            PresencePenaltyBox = presencePenaltyBox,
            FrequencyPenaltyBox = frequencyPenaltyBox,
            RopeScaleBox = ropeScaleBox,
            RopeFreqBaseBox = ropeFreqBaseBox,
            RopeFreqScaleBox = ropeFreqScaleBox,
            SpecDraftModelPathBox = specDraftModelPathBox,
            MtpHeadPathBox = mtpHeadPathBox,
            MtpHeadButton = mtpHeadButton,
            SpecDraftGpuLayersBox = specDraftGpuLayersBox,
            SpecDraftMinTokensBox = specDraftMinTokensBox,
            SpecDraftMaxTokensBox = specDraftMaxTokensBox,
            SpecDraftPSplitBox = specDraftPSplitBox,
            SpecDraftPMinBox = specDraftPMinBox,
            MetricsCombo = metricsCombo,
            ReasoningCombo = reasoningCombo,
            ReasoningFormatCombo = reasoningFormatCombo,
            VisionCombo = visionCombo,
            VisionProjectorButton = visionProjectorButton,
            FlashAttentionCombo = flashAttentionCombo,
            CacheTypeKCombo = cacheTypeKCombo,
            CacheTypeVCombo = cacheTypeVCombo,
            KvOffloadCombo = kvOffloadCombo,
            KvUnifiedCombo = kvUnifiedCombo,
            ContinuousBatchingCombo = continuousBatchingCombo,
            JinjaCombo = jinjaCombo,
            MmapCombo = mmapCombo,
            MlockCombo = mlockCombo,
            RopeScalingCombo = ropeScalingCombo,
            SpeculativeTypeCombo = speculativeTypeCombo,
            SpecDraftCacheTypeKCombo = specDraftCacheTypeKCombo,
            SpecDraftCacheTypeVCombo = specDraftCacheTypeVCombo
        };
    }
}
