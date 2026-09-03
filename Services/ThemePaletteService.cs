using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace XTimelineViewer.Services
{
    /// <summary>
    /// アプリ独自テーマの色を一か所で管理する。
    /// X のページ本体には色を注入せず、アプリの枠・ツールバー・列ヘッダーだけを着色する。
    /// </summary>
    internal static class ThemePaletteService
    {
        internal sealed record Palette(
            Color Surface,
            Color Chrome,
            Color Border,
            Color Header,
            Color FocusedHeader,
            Color Accent,
            Color AccentText,
            ElementTheme BaseTheme,
            bool OutlineFocusedPane = false);

        internal static bool TryGetPalette(string? theme, out Palette palette)
        {
            palette = theme switch
            {
                "Cyberpunk" => new(
                    ColorHelper.FromArgb(255, 5, 3, 10),
                    ColorHelper.FromArgb(255, 13, 6, 24),
                    ColorHelper.FromArgb(255, 255, 43, 214),
                    ColorHelper.FromArgb(255, 19, 9, 38),
                    ColorHelper.FromArgb(255, 76, 12, 112),
                    ColorHelper.FromArgb(255, 0, 240, 255),
                    ColorHelper.FromArgb(255, 1, 15, 18),
                    ElementTheme.Dark),
                "NeonContrast" => new(
                    Colors.Black,
                    ColorHelper.FromArgb(255, 2, 5, 7),
                    ColorHelper.FromArgb(255, 73, 210, 245),
                    ColorHelper.FromArgb(255, 0, 0, 0),
                    ColorHelper.FromArgb(255, 255, 145, 0),
                    ColorHelper.FromArgb(255, 73, 210, 245),
                    ColorHelper.FromArgb(255, 0, 12, 16),
                    ElementTheme.Dark,
                    OutlineFocusedPane: true),
                "Ocean" => new(
                    ColorHelper.FromArgb(255, 5, 23, 37),
                    ColorHelper.FromArgb(255, 8, 38, 58),
                    ColorHelper.FromArgb(255, 30, 89, 113),
                    ColorHelper.FromArgb(255, 11, 48, 70),
                    ColorHelper.FromArgb(255, 14, 116, 144),
                    ColorHelper.FromArgb(255, 34, 211, 238),
                    ColorHelper.FromArgb(255, 3, 18, 28),
                    ElementTheme.Dark),
                "Forest" => new(
                    ColorHelper.FromArgb(255, 10, 25, 18),
                    ColorHelper.FromArgb(255, 17, 42, 29),
                    ColorHelper.FromArgb(255, 55, 104, 73),
                    ColorHelper.FromArgb(255, 24, 58, 40),
                    ColorHelper.FromArgb(255, 39, 121, 77),
                    ColorHelper.FromArgb(255, 74, 222, 128),
                    ColorHelper.FromArgb(255, 4, 24, 14),
                    ElementTheme.Dark),
                "Sakura" => new(
                    ColorHelper.FromArgb(255, 255, 248, 251),
                    ColorHelper.FromArgb(255, 255, 239, 245),
                    ColorHelper.FromArgb(255, 226, 171, 191),
                    ColorHelper.FromArgb(255, 250, 224, 234),
                    ColorHelper.FromArgb(255, 190, 55, 105),
                    ColorHelper.FromArgb(255, 190, 55, 105),
                    Colors.White,
                    ElementTheme.Light),
                _ => null!,
            };
            return palette is not null;
        }

        internal static ElementTheme GetBaseTheme(string? theme) => theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ when TryGetPalette(theme, out var palette) => palette.BaseTheme,
            _ => ElementTheme.Default,
        };

        /// <summary>ThemeResource が参照するアプリ専用ブラシをウィンドウ単位で差し替える。</summary>
        internal static void ApplyResources(FrameworkElement root, string? theme, bool highContrast = false)
        {
            if (!highContrast && TryGetPalette(theme, out var palette))
            {
                root.Resources["AppSurfaceBrush"] = new SolidColorBrush(palette.Surface);
                root.Resources["AppChromeBrush"] = new SolidColorBrush(palette.Chrome);
                root.Resources["AppBorderBrush"] = new SolidColorBrush(palette.Border);
                root.Resources["AppAccentBrush"] = new SolidColorBrush(palette.Accent);
                root.Resources["AppAccentTextBrush"] = new SolidColorBrush(palette.AccentText);
                return;
            }

            // 独自テーマから標準テーマへ戻したときは、親の ThemeDictionary を再び参照させる。
            root.Resources.Remove("AppSurfaceBrush");
            root.Resources.Remove("AppChromeBrush");
            root.Resources.Remove("AppBorderBrush");
            root.Resources.Remove("AppAccentBrush");
            root.Resources.Remove("AppAccentTextBrush");
        }

        internal static Brush GetPaneBrush(string? theme, string role, ResourceDictionary fallback)
        {
            if (!TryGetPalette(theme, out var palette)) return (Brush)fallback[role];
            var color = role switch
            {
                "TimelinePaneBackgroundBrush" => palette.Surface,
                "TimelinePaneBorderBrush" => palette.Border,
                "TimelineHeaderBackgroundBrush" => palette.Header,
                "TimelineHeaderFocusedBackgroundBrush" => palette.FocusedHeader,
                _ => Colors.Transparent,
            };
            return new SolidColorBrush(color);
        }

        internal static bool UsesOutlineFocus(string? theme)
            => TryGetPalette(theme, out var palette) && palette.OutlineFocusedPane;
    }
}
