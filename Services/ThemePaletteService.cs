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
            bool OutlineFocusedPane = false,
            Color Text = default,
            Color SecondaryText = default,
            Color Hover = default,
            Color Pressed = default,
            Color Disabled = default,
            Color Focus = default);

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
        internal static void ApplyResources(
            FrameworkElement root,
            string? theme,
            bool highContrast = false,
            ElementTheme actualTheme = ElementTheme.Default)
        {
            var palette = ResolvePalette(theme, highContrast, actualTheme);
            var text = GetToken(palette.Text, palette, Token.Text);
            var secondaryText = GetToken(palette.SecondaryText, palette, Token.SecondaryText);
            var hover = GetToken(palette.Hover, palette, Token.Hover);
            var pressed = GetToken(palette.Pressed, palette, Token.Pressed);
            var disabled = GetToken(palette.Disabled, palette, Token.Disabled);
            var focus = GetToken(palette.Focus, palette, Token.Focus);
            // Remove による親辞書へのフォールバックは、ThemeResource が以前のブラシを
            // 保持する場合がある。切替のたびに全役割を新しい Brush で明示的に置換する。
            root.Resources["AppSurfaceBrush"] = new SolidColorBrush(palette.Surface);
            root.Resources["AppChromeBrush"] = new SolidColorBrush(palette.Chrome);
            root.Resources["AppBorderBrush"] = new SolidColorBrush(palette.Border);
            root.Resources["AppAccentBrush"] = new SolidColorBrush(palette.Accent);
            root.Resources["AppAccentTextBrush"] = new SolidColorBrush(palette.AccentText);
            root.Resources["AppTextBrush"] = new SolidColorBrush(text);
            root.Resources["AppSecondaryTextBrush"] = new SolidColorBrush(secondaryText);
            root.Resources["AppHoverBrush"] = new SolidColorBrush(hover);
            root.Resources["AppPressedBrush"] = new SolidColorBrush(pressed);
            root.Resources["AppDisabledBrush"] = new SolidColorBrush(disabled);
            root.Resources["AppFocusBrush"] = new SolidColorBrush(focus);
            root.Resources["TimelinePaneBackgroundBrush"] = new SolidColorBrush(palette.Surface);
            root.Resources["TimelinePaneBorderBrush"] = new SolidColorBrush(palette.Border);
            root.Resources["TimelineHeaderBackgroundBrush"] = new SolidColorBrush(palette.Header);
            root.Resources["TimelineHeaderFocusedBackgroundBrush"] = new SolidColorBrush(palette.FocusedHeader);
        }

        private enum Token
        {
            Text,
            SecondaryText,
            Hover,
            Pressed,
            Disabled,
            Focus,
        }

        private static Palette ResolvePalette(
            string? theme,
            bool highContrast,
            ElementTheme actualTheme = ElementTheme.Default)
        {
            if (highContrast)
            {
                var hc = (ResourceDictionary)Application.Current.Resources.ThemeDictionaries["HighContrast"];
                return new(
                    ((SolidColorBrush)hc["AppSurfaceBrush"]).Color,
                    ((SolidColorBrush)hc["AppChromeBrush"]).Color,
                    ((SolidColorBrush)hc["AppBorderBrush"]).Color,
                    ((SolidColorBrush)hc["TimelineHeaderBackgroundBrush"]).Color,
                    ((SolidColorBrush)hc["TimelineHeaderFocusedBackgroundBrush"]).Color,
                    ((SolidColorBrush)hc["AppAccentBrush"]).Color,
                    ((SolidColorBrush)hc["AppAccentTextBrush"]).Color,
                    ElementTheme.Default,
                    Text: ((SolidColorBrush)hc["AppTextBrush"]).Color,
                    SecondaryText: ((SolidColorBrush)hc["AppSecondaryTextBrush"]).Color,
                    Hover: ((SolidColorBrush)hc["AppHoverBrush"]).Color,
                    Pressed: ((SolidColorBrush)hc["AppPressedBrush"]).Color,
                    Disabled: ((SolidColorBrush)hc["AppDisabledBrush"]).Color,
                    Focus: ((SolidColorBrush)hc["AppFocusBrush"]).Color);
            }
            if (TryGetPalette(theme, out var custom)) return custom;
            var useLight = theme == "Light"
                || (theme is null or "Default"
                    && (actualTheme == ElementTheme.Light
                        || actualTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Light));
            return useLight
                ? new(
                    ColorHelper.FromArgb(255, 247, 247, 247), Colors.White,
                    ColorHelper.FromArgb(255, 210, 210, 210), ColorHelper.FromArgb(255, 235, 235, 240),
                    ColorHelper.FromArgb(255, 0, 120, 212), ColorHelper.FromArgb(255, 0, 120, 212),
                    Colors.White, ElementTheme.Light,
                    Text: ColorHelper.FromArgb(255, 32, 32, 32),
                    SecondaryText: ColorHelper.FromArgb(255, 92, 92, 92),
                    Hover: ColorHelper.FromArgb(255, 242, 242, 242),
                    Pressed: ColorHelper.FromArgb(255, 229, 229, 229),
                    Disabled: ColorHelper.FromArgb(150, 32, 32, 32),
                    Focus: ColorHelper.FromArgb(255, 0, 120, 212))
                : new(
                    ColorHelper.FromArgb(255, 32, 32, 32), ColorHelper.FromArgb(255, 40, 40, 40),
                    ColorHelper.FromArgb(255, 70, 70, 70), ColorHelper.FromArgb(255, 55, 55, 60),
                    ColorHelper.FromArgb(255, 29, 78, 137), ColorHelper.FromArgb(255, 96, 205, 255),
                    ColorHelper.FromArgb(255, 7, 16, 24), ElementTheme.Dark,
                    Text: ColorHelper.FromArgb(255, 246, 246, 246),
                    SecondaryText: ColorHelper.FromArgb(255, 190, 190, 190),
                    Hover: ColorHelper.FromArgb(255, 55, 55, 55),
                    Pressed: ColorHelper.FromArgb(255, 70, 70, 70),
                    Disabled: ColorHelper.FromArgb(150, 246, 246, 246),
                    Focus: ColorHelper.FromArgb(255, 96, 205, 255));
        }

        internal static Brush GetResizeBrush(string? theme, bool highContrast)
            => new SolidColorBrush(ResolvePalette(theme, highContrast).Border);

        internal static Brush GetAppBrush(
            string? theme,
            string role,
            ElementTheme actualTheme,
            bool highContrast = false)
        {
            var palette = ResolvePalette(theme, highContrast, actualTheme);
            var color = role switch
            {
                "AppSurfaceBrush" => palette.Surface,
                "AppChromeBrush" => palette.Chrome,
                "AppBorderBrush" => palette.Border,
                "AppAccentBrush" => palette.Accent,
                "AppAccentTextBrush" => palette.AccentText,
                "AppTextBrush" => GetToken(palette.Text, palette, Token.Text),
                "AppSecondaryTextBrush" => GetToken(palette.SecondaryText, palette, Token.SecondaryText),
                "AppHoverBrush" => GetToken(palette.Hover, palette, Token.Hover),
                "AppPressedBrush" => GetToken(palette.Pressed, palette, Token.Pressed),
                "AppDisabledBrush" => GetToken(palette.Disabled, palette, Token.Disabled),
                "AppFocusBrush" => GetToken(palette.Focus, palette, Token.Focus),
                _ => Colors.Transparent,
            };
            return new SolidColorBrush(color);
        }

        internal static Brush GetPaneBrush(
            string? theme,
            string role,
            ResourceDictionary fallback,
            ElementTheme actualTheme = ElementTheme.Default)
        {
            var palette = ResolvePalette(theme, false, actualTheme);
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

        private static Color GetToken(Color value, Palette palette, Token token)
            => value.A != 0 ? value : token switch
            {
                Token.Text => palette.BaseTheme == ElementTheme.Light
                    ? ColorHelper.FromArgb(255, 32, 32, 32)
                    : ColorHelper.FromArgb(255, 246, 246, 246),
                Token.SecondaryText => palette.BaseTheme == ElementTheme.Light
                    ? ColorHelper.FromArgb(255, 92, 92, 92)
                    : ColorHelper.FromArgb(255, 190, 190, 190),
                Token.Hover => Blend(palette.Chrome, GetToken(palette.Text, palette, Token.Text), .08),
                Token.Pressed => Blend(palette.Chrome, GetToken(palette.Text, palette, Token.Text), .16),
                Token.Disabled => WithAlpha(GetToken(palette.Text, palette, Token.Text), 150),
                Token.Focus => palette.Accent,
                _ => palette.Text,
            };

        private static Color WithAlpha(Color color, byte alpha)
            => ColorHelper.FromArgb(alpha, color.R, color.G, color.B);

        private static Color Blend(Color from, Color to, double amount)
            => ColorHelper.FromArgb(
                255,
                (byte)(from.R + (to.R - from.R) * amount),
                (byte)(from.G + (to.G - from.G) * amount),
                (byte)(from.B + (to.B - from.B) * amount));
    }
}
