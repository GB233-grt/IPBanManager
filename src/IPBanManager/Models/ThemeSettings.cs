using System;

namespace IPBanManager.Models
{
    /// <summary>
    /// 可导入/导出的主题配置。颜色统一使用 #RRGGBB。
    /// </summary>
    public class ThemeSettings
    {
        public ThemeSettings()
        {
            IsDark = false;
            AccentColor = "#4C8DFF";
            BackgroundColor = "#F4F6FB";
            CornerRadius = 14;
            CardOpacity = 0.96;
            BackgroundImagePath = "";
            BackgroundImageOpacity = 0.18;
        }

        public bool IsDark { get; set; }
        public string AccentColor { get; set; }
        public string BackgroundColor { get; set; }
        public double CornerRadius { get; set; }
        public double CardOpacity { get; set; }
        public string BackgroundImagePath { get; set; }
        public double BackgroundImageOpacity { get; set; }

        public static ThemeSettings CreateDefault()
        {
            return new ThemeSettings();
        }

        public void Normalize()
        {
            if (!IsHexColor(AccentColor)) AccentColor = "#4C8DFF";
            else AccentColor = AccentColor.Trim().ToUpperInvariant();

            if (!IsHexColor(BackgroundColor))
                BackgroundColor = IsDark ? "#10141C" : "#F4F6FB";
            else
                BackgroundColor = BackgroundColor.Trim().ToUpperInvariant();

            if (CornerRadius < 0) CornerRadius = 0;
            if (CornerRadius > 24) CornerRadius = 24;
            if (CardOpacity < 0.55) CardOpacity = 0.55;
            if (CardOpacity > 1.0) CardOpacity = 1.0;
            if (BackgroundImageOpacity < 0.03) BackgroundImageOpacity = 0.03;
            if (BackgroundImageOpacity > 0.8) BackgroundImageOpacity = 0.8;
            if (BackgroundImagePath == null) BackgroundImagePath = "";
        }

        private static bool IsHexColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string text = value.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal)) text = text.Substring(1);
            if (text.Length != 3 && text.Length != 6) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        public ThemeSettings Clone()
        {
            return new ThemeSettings
            {
                IsDark = IsDark,
                AccentColor = AccentColor,
                BackgroundColor = BackgroundColor,
                CornerRadius = CornerRadius,
                CardOpacity = CardOpacity,
                BackgroundImagePath = BackgroundImagePath,
                BackgroundImageOpacity = BackgroundImageOpacity
            };
        }
    }
}
