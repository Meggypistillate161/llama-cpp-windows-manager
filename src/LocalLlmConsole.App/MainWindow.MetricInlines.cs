using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    private static TextBlock MetricPlainValueBlock(string text, bool compact)
    {
        var block = new TextBlock
        {
            FontSize = compact ? 13 : 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"],
            TextWrapping = TextWrapping.Wrap,
            LineHeight = compact ? 16 : 17,
            Margin = new Thickness(0, 0, 0, 2)
        };
        block.Inlines.Add(new Run(string.IsNullOrWhiteSpace(text) ? "..." : text));
        return block;
    }

    private static TextBlock MetricStatusNameBlock(string statusPrefix, string emphasizedName)
    {
        var block = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"],
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 17,
            Margin = new Thickness(0, 0, 0, 2)
        };
        if (!string.IsNullOrWhiteSpace(statusPrefix))
        {
            block.Inlines.Add(new Run(statusPrefix)
            {
                Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"]
            });
        }

        block.Inlines.Add(new Run(emphasizedName)
        {
            FontWeight = FontWeights.Bold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["AccentStrong"]
        });
        return block;
    }

    private static void AddMetricValueInlines(TextBlock block, string text, bool emphasizeWholeLine = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            block.Inlines.Add(new Run("..."));
            return;
        }

        if (emphasizeWholeLine)
        {
            block.Inlines.Add(new Run(text)
            {
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["AccentStrong"]
            });
            return;
        }

        var index = 0;
        foreach (Match match in MetricImportantValuePattern.Matches(text))
        {
            if (match.Index > index)
                block.Inlines.Add(new Run(text[index..match.Index])
                {
                    Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"]
                });

            var valueRun = new Run(match.Value)
            {
                FontFamily = MetricValueFont,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["AccentStrong"]
            };
            Typography.SetNumeralAlignment(valueRun, FontNumeralAlignment.Tabular);
            block.Inlines.Add(valueRun);
            index = match.Index + match.Length;
        }

        if (index < text.Length)
        {
            block.Inlines.Add(new Run(text[index..])
            {
                Foreground = index == 0
                    ? (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"]
                    : (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"]
            });
        }
    }
}
