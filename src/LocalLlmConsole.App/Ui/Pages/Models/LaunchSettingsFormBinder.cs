using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed class LaunchSettingsFormControls
{
    public WpfTextBox? LaunchPortBox { get; init; }
    public WpfTextBox? ContextSizeBox { get; init; }
    public WpfTextBox? GpuLayersBox { get; init; }
    public WpfTextBox? ParallelSlotsBox { get; init; }
    public WpfTextBox? BatchSizeBox { get; init; }
    public WpfTextBox? MicroBatchSizeBox { get; init; }
    public WpfTextBox? ThreadsBox { get; init; }
    public WpfTextBox? ReasoningBudgetBox { get; init; }
    public WpfTextBox? VisionProjectorPathBox { get; init; }
    public WpfButton? VisionProjectorButton { get; init; }
    public WpfTextBox? VisionImageMinTokensBox { get; init; }
    public WpfTextBox? VisionImageMaxTokensBox { get; init; }
    public WpfTextBox? TemperatureBox { get; init; }
    public WpfTextBox? TopKBox { get; init; }
    public WpfTextBox? TopPBox { get; init; }
    public WpfTextBox? MinPBox { get; init; }
    public WpfTextBox? MaxTokensBox { get; init; }
    public WpfTextBox? SeedBox { get; init; }
    public WpfTextBox? RepeatLastNBox { get; init; }
    public WpfTextBox? RepeatPenaltyBox { get; init; }
    public WpfTextBox? PresencePenaltyBox { get; init; }
    public WpfTextBox? FrequencyPenaltyBox { get; init; }
    public WpfTextBox? RopeScaleBox { get; init; }
    public WpfTextBox? RopeFreqBaseBox { get; init; }
    public WpfTextBox? RopeFreqScaleBox { get; init; }
    public WpfTextBox? SpecDraftModelPathBox { get; init; }
    public WpfTextBox? MtpHeadPathBox { get; init; }
    public WpfButton? MtpHeadButton { get; init; }
    public WpfTextBox? SpecDraftGpuLayersBox { get; init; }
    public WpfTextBox? SpecDraftMinTokensBox { get; init; }
    public WpfTextBox? SpecDraftMaxTokensBox { get; init; }
    public WpfTextBox? SpecDraftPSplitBox { get; init; }
    public WpfTextBox? SpecDraftPMinBox { get; init; }

    public WpfComboBox? MetricsCombo { get; init; }
    public WpfComboBox? ReasoningCombo { get; init; }
    public WpfComboBox? ReasoningFormatCombo { get; init; }
    public WpfComboBox? VisionCombo { get; init; }
    public WpfComboBox? FlashAttentionCombo { get; init; }
    public WpfComboBox? CacheTypeKCombo { get; init; }
    public WpfComboBox? CacheTypeVCombo { get; init; }
    public WpfComboBox? KvOffloadCombo { get; init; }
    public WpfComboBox? KvUnifiedCombo { get; init; }
    public WpfComboBox? ContinuousBatchingCombo { get; init; }
    public WpfComboBox? JinjaCombo { get; init; }
    public WpfComboBox? MmapCombo { get; init; }
    public WpfComboBox? MlockCombo { get; init; }
    public WpfComboBox? RopeScalingCombo { get; init; }
    public WpfComboBox? SpeculativeTypeCombo { get; init; }
    public WpfComboBox? SpecDraftCacheTypeKCombo { get; init; }
    public WpfComboBox? SpecDraftCacheTypeVCombo { get; init; }

    public IEnumerable<WpfTextBox?> TextBoxes =>
    [
        LaunchPortBox, ContextSizeBox, GpuLayersBox, ParallelSlotsBox, BatchSizeBox, MicroBatchSizeBox,
        ThreadsBox, ReasoningBudgetBox, VisionProjectorPathBox, VisionImageMinTokensBox, VisionImageMaxTokensBox,
        TemperatureBox, TopKBox, TopPBox, MinPBox, MaxTokensBox, SeedBox, RepeatLastNBox,
        RepeatPenaltyBox, PresencePenaltyBox, FrequencyPenaltyBox, RopeScaleBox, RopeFreqBaseBox,
        RopeFreqScaleBox, SpecDraftModelPathBox, MtpHeadPathBox, SpecDraftGpuLayersBox, SpecDraftMinTokensBox,
        SpecDraftMaxTokensBox, SpecDraftPSplitBox, SpecDraftPMinBox
    ];

    public IEnumerable<WpfComboBox?> ComboBoxes =>
    [
        MetricsCombo, ReasoningCombo, ReasoningFormatCombo, VisionCombo, FlashAttentionCombo,
        CacheTypeKCombo, CacheTypeVCombo, KvOffloadCombo, KvUnifiedCombo, ContinuousBatchingCombo,
        JinjaCombo, MmapCombo, MlockCombo, RopeScalingCombo, SpeculativeTypeCombo,
        SpecDraftCacheTypeKCombo, SpecDraftCacheTypeVCombo
    ];
}

public static class LaunchSettingsFormBinder
{
    public static AppSettings Read(AppSettings baseSettings, LaunchSettingsFormControls controls)
    {
        var next = baseSettings with
        {
            Port = ReadInt(controls.LaunchPortBox, "Port", min: 1, max: 65535),
            ContextSize = ReadContextSize(controls.ContextSizeBox),
            GpuLayers = ReadInt(controls.GpuLayersBox, "GPU layers", min: 0),
            ParallelSlots = ReadInt(controls.ParallelSlotsBox, "Parallel slots", min: 1),
            BatchSize = ReadInt(controls.BatchSizeBox, "Batch size", min: 1),
            MicroBatchSize = ReadInt(controls.MicroBatchSizeBox, "Micro batch size", min: 1),
            Threads = ReadInt(controls.ThreadsBox, "Threads", min: 0),
            ReasoningMode = ComboValue(controls.ReasoningCombo),
            ReasoningFormat = ComboValue(controls.ReasoningFormatCombo),
            ReasoningBudget = ReadInt(controls.ReasoningBudgetBox, "Reasoning budget", min: -1),
            VisionMode = ComboValue(controls.VisionCombo),
            VisionProjectorPath = controls.VisionProjectorPathBox?.Text.Trim() ?? "",
            VisionImageMinTokens = ReadInt(controls.VisionImageMinTokensBox, "Image min tokens", min: 0),
            VisionImageMaxTokens = ReadInt(controls.VisionImageMaxTokensBox, "Image max tokens", min: 0),
            FlashAttention = ComboValue(controls.FlashAttentionCombo),
            CacheTypeK = ComboValue(controls.CacheTypeKCombo),
            CacheTypeV = ComboValue(controls.CacheTypeVCombo),
            KvOffload = ComboValue(controls.KvOffloadCombo),
            KvUnified = ComboValue(controls.KvUnifiedCombo),
            ContinuousBatching = ComboValue(controls.ContinuousBatchingCombo),
            JinjaMode = ComboValue(controls.JinjaCombo),
            MmapMode = ComboValue(controls.MmapCombo),
            MlockMode = ComboValue(controls.MlockCombo),
            EnableMetrics = ComboValue(controls.MetricsCombo) == "on",
            Temperature = ReadDouble(controls.TemperatureBox, "Temperature", min: 0),
            TopK = ReadInt(controls.TopKBox, "Top K", min: 0),
            TopP = ReadDouble(controls.TopPBox, "Top P", min: 0, max: 1),
            MinP = ReadDouble(controls.MinPBox, "Min P", min: 0, max: 1),
            MaxTokens = ReadInt(controls.MaxTokensBox, "Max tokens", min: -1),
            Seed = ReadInt(controls.SeedBox, "Seed", min: -1),
            RepeatLastN = ReadInt(controls.RepeatLastNBox, "Repeat window", min: -1),
            RepeatPenalty = ReadDouble(controls.RepeatPenaltyBox, "Repeat penalty", min: 0),
            PresencePenalty = ReadDouble(controls.PresencePenaltyBox, "Presence penalty", min: -10, max: 10),
            FrequencyPenalty = ReadDouble(controls.FrequencyPenaltyBox, "Frequency penalty", min: -10, max: 10),
            RopeScaling = ComboValue(controls.RopeScalingCombo),
            RopeScale = ReadDouble(controls.RopeScaleBox, "RoPE scale", min: 0),
            RopeFreqBase = ReadDouble(controls.RopeFreqBaseBox, "RoPE base", min: 0),
            RopeFreqScale = ReadDouble(controls.RopeFreqScaleBox, "RoPE frequency scale", min: 0),
            SpeculativeType = ComboValue(controls.SpeculativeTypeCombo),
            SpecDraftModelPath = controls.SpecDraftModelPathBox?.Text.Trim() ?? "",
            MtpHeadPath = controls.MtpHeadPathBox?.Text.Trim() ?? "",
            SpecDraftGpuLayers = ReadInt(controls.SpecDraftGpuLayersBox, "Draft GPU layers", min: -1),
            SpecDraftMinTokens = ReadInt(controls.SpecDraftMinTokensBox, "Draft min tokens", min: 0),
            SpecDraftMaxTokens = ReadInt(controls.SpecDraftMaxTokensBox, "Draft max tokens", min: 0),
            SpecDraftPSplit = ReadDouble(controls.SpecDraftPSplitBox, "Draft split probability", min: -1, max: 1),
            SpecDraftPMin = ReadDouble(controls.SpecDraftPMinBox, "Draft min probability", min: -1, max: 1),
            SpecDraftCacheTypeK = ComboValue(controls.SpecDraftCacheTypeKCombo),
            SpecDraftCacheTypeV = ComboValue(controls.SpecDraftCacheTypeVCombo)
        };

        ValidateCrossFieldRules(next);
        return next;
    }

    public static void Apply(LaunchSettingsFormControls controls, AppSettings settings)
    {
        SetText(controls.LaunchPortBox, settings.Port);
        SetText(controls.ContextSizeBox, settings.ContextSize);
        SetText(controls.GpuLayersBox, settings.GpuLayers);
        SetText(controls.ParallelSlotsBox, settings.ParallelSlots);
        SetText(controls.BatchSizeBox, settings.BatchSize);
        SetText(controls.MicroBatchSizeBox, settings.MicroBatchSize);
        SetText(controls.ThreadsBox, settings.Threads);
        SetText(controls.ReasoningBudgetBox, settings.ReasoningBudget);
        SetText(controls.VisionProjectorPathBox, settings.VisionProjectorPath);
        SetText(controls.VisionImageMinTokensBox, settings.VisionImageMinTokens);
        SetText(controls.VisionImageMaxTokensBox, settings.VisionImageMaxTokens);
        SetText(controls.TemperatureBox, settings.Temperature);
        SetText(controls.TopKBox, settings.TopK);
        SetText(controls.TopPBox, settings.TopP);
        SetText(controls.MinPBox, settings.MinP);
        SetText(controls.MaxTokensBox, settings.MaxTokens);
        SetText(controls.SeedBox, settings.Seed);
        SetText(controls.RepeatLastNBox, settings.RepeatLastN);
        SetText(controls.RepeatPenaltyBox, settings.RepeatPenalty);
        SetText(controls.PresencePenaltyBox, settings.PresencePenalty);
        SetText(controls.FrequencyPenaltyBox, settings.FrequencyPenalty);
        SetText(controls.RopeScaleBox, settings.RopeScale);
        SetText(controls.RopeFreqBaseBox, settings.RopeFreqBase);
        SetText(controls.RopeFreqScaleBox, settings.RopeFreqScale);
        SetText(controls.SpecDraftModelPathBox, settings.SpecDraftModelPath);
        SetText(controls.MtpHeadPathBox, settings.MtpHeadPath);
        SetText(controls.SpecDraftGpuLayersBox, settings.SpecDraftGpuLayers);
        SetText(controls.SpecDraftMinTokensBox, settings.SpecDraftMinTokens);
        SetText(controls.SpecDraftMaxTokensBox, settings.SpecDraftMaxTokens);
        SetText(controls.SpecDraftPSplitBox, settings.SpecDraftPSplit);
        SetText(controls.SpecDraftPMinBox, settings.SpecDraftPMin);
        SetCombo(controls.MetricsCombo, settings.EnableMetrics ? "on" : "off");
        SetCombo(controls.ReasoningCombo, settings.ReasoningMode);
        SetCombo(controls.ReasoningFormatCombo, settings.ReasoningFormat);
        SetCombo(controls.VisionCombo, settings.VisionMode);
        SetCombo(controls.FlashAttentionCombo, settings.FlashAttention);
        SetCombo(controls.CacheTypeKCombo, settings.CacheTypeK);
        SetCombo(controls.CacheTypeVCombo, settings.CacheTypeV);
        SetCombo(controls.KvOffloadCombo, settings.KvOffload);
        SetCombo(controls.KvUnifiedCombo, settings.KvUnified);
        SetCombo(controls.ContinuousBatchingCombo, settings.ContinuousBatching);
        SetCombo(controls.JinjaCombo, settings.JinjaMode);
        SetCombo(controls.MmapCombo, settings.MmapMode);
        SetCombo(controls.MlockCombo, settings.MlockMode);
        SetCombo(controls.RopeScalingCombo, settings.RopeScaling);
        SetCombo(controls.SpeculativeTypeCombo, LocalLlmConsole.Services.LaunchSettingMetadataService.NormalizeSpeculativeType(settings.SpeculativeType));
        SetCombo(controls.SpecDraftCacheTypeKCombo, settings.SpecDraftCacheTypeK);
        SetCombo(controls.SpecDraftCacheTypeVCombo, settings.SpecDraftCacheTypeV);
    }

    public static void AttachChangeHandlers(LaunchSettingsFormControls controls, Action changed, RoutedEventHandler contextSizeLostFocus)
    {
        if (controls.ContextSizeBox is not null)
            controls.ContextSizeBox.LostFocus += contextSizeLostFocus;

        foreach (var box in controls.TextBoxes.Where(box => box is not null))
            box!.TextChanged += (_, _) => changed();

        foreach (var combo in controls.ComboBoxes.Where(combo => combo is not null))
            combo!.SelectionChanged += (_, _) => changed();
    }

    public static void ValidateCrossFieldRules(AppSettings next)
    {
        if (next.SpecDraftPSplit < 0 && Math.Abs(next.SpecDraftPSplit + 1) > 0.000_001)
            throw new InvalidOperationException("Draft split probability must be -1 for default or between 0 and 1.");
        if (next.SpecDraftPMin < 0 && Math.Abs(next.SpecDraftPMin + 1) > 0.000_001)
            throw new InvalidOperationException("Draft min probability must be -1 for default or between 0 and 1.");
        if (next.SpecDraftMaxTokens > 0 && next.SpecDraftMinTokens > next.SpecDraftMaxTokens)
            throw new InvalidOperationException("Draft min tokens cannot be larger than draft max tokens.");
        if (next.VisionImageMaxTokens > 0 && next.VisionImageMinTokens > next.VisionImageMaxTokens)
            throw new InvalidOperationException("Image min tokens cannot be larger than image max tokens.");
    }

    private static void SetText(WpfTextBox? box, int value) => SetText(box, value.ToString(CultureInfo.InvariantCulture));

    private static void SetText(WpfTextBox? box, double value) => SetText(box, value.ToString("0.###", CultureInfo.InvariantCulture));

    private static void SetText(WpfTextBox? box, string value)
    {
        if (box is not null) box.Text = value;
    }

    private static void SetCombo(WpfComboBox? combo, string value)
    {
        if (combo is null) return;
        var match = combo.Items.Cast<object>().Select(item => item.ToString() ?? "").FirstOrDefault(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
        combo.SelectedItem = string.IsNullOrWhiteSpace(match) ? combo.Items[0] : match;
    }

    private static string ComboValue(WpfComboBox? combo)
        => (combo?.SelectedItem?.ToString() ?? combo?.Text ?? "").Trim().ToLowerInvariant();

    private static int ReadContextSize(WpfTextBox? box)
        => LaunchSettingParser.ReadContextSize(box?.Text.Trim() ?? "");

    private static int ReadInt(WpfTextBox? box, string label, int min, int? max = null)
        => LaunchSettingParser.ReadInt(box?.Text.Trim() ?? "", label, min, max);

    private static double ReadDouble(WpfTextBox? box, string label, double min, double? max = null)
        => LaunchSettingParser.ReadDouble(box?.Text.Trim() ?? "", label, min, max);
}
