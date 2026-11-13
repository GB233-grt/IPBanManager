using System;
using System.Windows;
using System.Windows.Media;
using IPBanManager.Models;

namespace IPBanManager.Services
{
    /// <summary>
    /// 主题运行时应用。所有页面使用 DynamicResource 引用这里的画刷，
    /// 因此滑条拖动时无需重建页面即可实时变化。
    /// </summary>
    public static class ThemeManager
    {
        public static ThemeSettings Current { get; private set; }

        public static ThemeSettings Apply(ThemeSettings theme)
        {
            if (theme == null) theme = ThemeSettings.CreateDefault();
            theme.Normalize();
            Current = theme;

            if (Application.Current == null) return theme;

            Color accent = ColorUtil.Parse(theme.AccentColor, Color.FromRgb(0x4C, 0x8D, 0xFF));
            Color background = ColorUtil.Parse(theme.BackgroundColor,
                theme.IsDark ? Color.FromRgb(0x10, 0x14, 0x1C) : Color.FromRgb(0xF4, 0xF6, 0xFB));
            Color surface = theme.IsDark ? Color.FromRgb(0x1A, 0x21, 0x30) : Color.FromRgb(0xFF, 0xFF, 0xFF);
            Color text = theme.IsDark ? Color.FromRgb(0xED, 0xF2, 0xFA) : Color.FromRgb(0x20, 0x28, 0x36);
            Color subText = theme.IsDark ? Color.FromRgb(0xA3, 0xB0, 0xC4) : Color.FromRgb(0x68, 0x74, 0x86);
            Color muted = theme.IsDark ? Color.FromRgb(0x72, 0x80, 0x95) : Color.FromRgb(0x9A, 0xA4, 0xB2);
            Color border = theme.IsDark ? Color.FromRgb(0x2C, 0x36, 0x47) : Color.FromRgb(0xE4, 0xEA, 0xF2);
            Color input = theme.IsDark ? Color.FromRgb(0x14, 0x1B, 0x28) : Color.FromRgb(0xF7, 0xF9, 0xFC);
            Color accentText = ColorUtil.Luminance(accent) > 0.62
                ? Color.FromRgb(0x18, 0x22, 0x2D)
                : Colors.White;

            Color accentHover = theme.IsDark ? ColorUtil.Lighten(accent, 0.10) : ColorUtil.Darken(accent, 0.08);
            Color accentPressed = theme.IsDark ? ColorUtil.Lighten(accent, 0.18) : ColorUtil.Darken(accent, 0.16);
            Color accentSoft = ColorUtil.Blend(surface, accent, 0.13);
            Color rowHover = ColorUtil.Blend(surface, accent, 0.055);
            Color danger = theme.IsDark ? Color.FromRgb(0xFF, 0x7A, 0x85) : Color.FromRgb(0xD9, 0x50, 0x5B);
            Color success = theme.IsDark ? Color.FromRgb(0x4C, 0xD3, 0x9A) : Color.FromRgb(0x22, 0x96, 0x62);
            Color warning = theme.IsDark ? Color.FromRgb(0xF3, 0xB1, 0x5B) : Color.FromRgb(0xC4, 0x7B, 0x1D);

            byte cardAlpha = (byte)Math.Round(255 * theme.CardOpacity);
            Color surfaceWithAlpha = Color.FromArgb(cardAlpha, surface.R, surface.G, surface.B);
            Color navWithAlpha = Color.FromArgb(
                (byte)Math.Min(255, Math.Round(cardAlpha * 0.90)),
                surface.R, surface.G, surface.B);

            ResourceDictionary resources = Application.Current.Resources;
            Set(resources, "AppBackgroundBrush", Solid(background));
            Set(resources, "SurfaceBrush", Solid(surfaceWithAlpha));
            Set(resources, "SurfaceSolidBrush", Solid(surface));
            Set(resources, "NavBackgroundBrush", Solid(navWithAlpha));
            Set(resources, "InputBackgroundBrush", Solid(input));
            Set(resources, "TextBrush", Solid(text));
            Set(resources, "SubTextBrush", Solid(subText));
            Set(resources, "MutedTextBrush", Solid(muted));
            Set(resources, "BorderBrush", Solid(border));
            Set(resources, "DividerBrush", Solid(ColorUtil.Blend(background, border, 0.8)));
            Set(resources, "AccentBrush", Solid(accent));
            Set(resources, "AccentHoverBrush", Solid(accentHover));
            Set(resources, "AccentPressedBrush", Solid(accentPressed));
            Set(resources, "AccentSoftBrush", Solid(accentSoft));
            Set(resources, "AccentTextBrush", Solid(accentText));
            Set(resources, "RowHoverBrush", Solid(rowHover));
            Set(resources, "DangerBrush", Solid(danger));
            Set(resources, "DangerSoftBrush", Solid(ColorUtil.Blend(surface, danger, 0.12)));
            Set(resources, "SuccessBrush", Solid(success));
            Set(resources, "SuccessSoftBrush", Solid(ColorUtil.Blend(surface, success, 0.12)));
            Set(resources, "WarningBrush", Solid(warning));
            Set(resources, "WarningSoftBrush", Solid(ColorUtil.Blend(surface, warning, 0.13)));
            Set(resources, "ScrollThumbBrush", Solid(ColorUtil.Blend(background, subText, 0.35)));
            Set(resources, "ScrollTrackBrush", Solid(ColorUtil.Blend(background, border, 0.5)));
            Set(resources, "WindowBorderBrush", Solid(ColorUtil.Blend(background, border, 0.9)));
            Set(resources, "IconSoftBrush", Solid(ColorUtil.Blend(surface, accent, 0.10)));
            resources["ShadowColor"] = Color.FromArgb(40, 14, 22, 36);

            resources["CardRadius"] = new CornerRadius(theme.CornerRadius);
            resources["ControlRadius"] = new CornerRadius(Math.Max(5, theme.CornerRadius - 4));
            resources["SmallRadius"] = new CornerRadius(Math.Max(4, theme.CornerRadius - 8));
            resources["IsDarkTheme"] = theme.IsDark;

            return theme;
        }

        public static void SetBool(string key, bool value)
        {
            if (Application.Current != null) Application.Current.Resources[key] = value;
        }

        private static void Set(ResourceDictionary resources, string key, Brush brush)
        {
            resources[key] = brush;
        }

        private static SolidColorBrush Solid(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    public static class ColorUtil
    {
        public static Color Parse(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            string value = hex.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal)) value = "#" + value;
            try
            {
                object converted = ColorConverter.ConvertFromString(value);
                if (converted is Color) return (Color)converted;
            }
            catch
            {
                // 返回回退色。
            }
            return fallback;
        }

        public static string ToHex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        public static Color Blend(Color from, Color to, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (byte)Math.Round(from.A + (to.A - from.A) * amount),
                (byte)Math.Round(from.R + (to.R - from.R) * amount),
                (byte)Math.Round(from.G + (to.G - from.G) * amount),
                (byte)Math.Round(from.B + (to.B - from.B) * amount));
        }

        public static Color Lighten(Color color, double amount)
        {
            return Blend(color, Colors.White, amount);
        }

        public static Color Darken(Color color, double amount)
        {
            return Blend(color, Colors.Black, amount);
        }

        public static double Luminance(Color color)
        {
            return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        }
    }
}
