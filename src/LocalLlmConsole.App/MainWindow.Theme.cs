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
    private static void ApplyTheme(string mode)
    {
        var dark = AppPreferenceService.ThemeMode(mode) switch
        {
            "light" => false,
            "dark" => true,
            _ => IsSystemDarkTheme()
        };

        foreach (var (key, color) in dark ? DarkThemeColors() : LightThemeColors())
        {
            var resolved = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
            WpfApplication.Current.Resources[key] = new SolidColorBrush(resolved);
        }
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return true;
        }
    }

    private static (string Key, string Color)[] DarkThemeColors() =>
    [
        ("AppBack", "#0A0D12"),
        ("PanelBack", "#171C24"),
        ("PanelBackAlt", "#1D2530"),
        ("PanelBorder", "#2B3542"),
        ("PanelBorderStrong", "#3B4656"),
        ("ControlBack", "#202936"),
        ("ControlHover", "#293443"),
        ("ControlPressed", "#15212B"),
        ("InputBack", "#10161F"),
        ("ReadOnlyBack", "#121923"),
        ("GridRowBack", "#121923"),
        ("GridRowAlt", "#151E29"),
        ("TextMain", "#F2F5F8"),
        ("TextMuted", "#9DAAB8"),
        ("TextSoft", "#C8D0D9"),
        ("Accent", "#4FD1A5"),
        ("AccentStrong", "#35B98D"),
        ("AccentSoft", "#253E3A"),
        ("InfoSoft", "#23334A"),
        ("Warning", "#F5B84B")
    ];

    private static (string Key, string Color)[] LightThemeColors() =>
    [
        ("AppBack", "#E5ECF3"),
        ("PanelBack", "#FFFFFF"),
        ("PanelBackAlt", "#F0F5FA"),
        ("PanelBorder", "#B7C4D2"),
        ("PanelBorderStrong", "#8799AC"),
        ("ControlBack", "#F4F8FC"),
        ("ControlHover", "#E1EAF3"),
        ("ControlPressed", "#CBD9E8"),
        ("InputBack", "#FFFFFF"),
        ("ReadOnlyBack", "#EAF1F8"),
        ("GridRowBack", "#FFFFFF"),
        ("GridRowAlt", "#EDF4FA"),
        ("TextMain", "#111B25"),
        ("TextMuted", "#536170"),
        ("TextSoft", "#223245"),
        ("Accent", "#126F5B"),
        ("AccentStrong", "#0B5748"),
        ("AccentSoft", "#C9E9E1"),
        ("InfoSoft", "#D7E5F2"),
        ("Warning", "#8A5100")
    ];
}
