using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using IPBanManager.Models;
using IPBanManager.Services;

namespace IPBanManager.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool && (bool)value;
            if (string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase)) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }

    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool && (bool)value ? 1.0 : 0.55;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class IpDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            BanRule rule = value as BanRule;
            if (rule == null) return "";
            if (string.Equals(rule.IpMode, "custom", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrEmpty(rule.CustomIp) ? "自定义" : rule.CustomIp;
            return "自动 · 0.0.0.0 + 127.0.0.1";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class GroupNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            BanRule rule = value as BanRule;
            if (rule == null || App.State == null) return "";
            return App.State.GetGroupNameWithState(rule.GroupId);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class RuleStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            BanRule rule = value as BanRule;
            if (rule == null || App.State == null) return "";
            if (!rule.Enabled) return "已禁用";
            if (!App.State.GetEffectiveEnabled(rule)) return "分组暂停";
            return "已启用";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class RuleStatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            BanRule rule = value as BanRule;
            string key = "MutedTextBrush";
            if (rule != null && App.State != null)
            {
                if (!rule.Enabled) key = "MutedTextBrush";
                else if (!App.State.GetEffectiveEnabled(rule)) key = "WarningBrush";
                else key = "SuccessBrush";
            }
            if (Application.Current != null && Application.Current.Resources.Contains(key))
                return Application.Current.Resources[key];
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is DateTime)) return "";
            DateTime time = (DateTime)value;
            TimeSpan span = DateTime.Now - time;
            if (span.TotalSeconds < 60) return "刚刚";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " 分钟前";
            if (span.TotalHours < 24) return (int)span.TotalHours + " 小时前";
            if (span.TotalDays < 7) return (int)span.TotalDays + " 天前";
            return time.ToString("yyyy-MM-dd HH:mm");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class HexBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Color fallback = Color.FromRgb(0x4C, 0x8D, 0xFF);
            Color color = ColorUtil.Parse(value as string, fallback);
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class OperationGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string kind = value as string ?? "";
            switch (kind)
            {
                case "add": return "\uE710";
                case "delete": return "\uE74D";
                case "edit": return "\uE70F";
                case "toggle": return "\uE7E8";
                case "apply": return "\uE83D";
                case "dns": return "\uE72C";
                case "backup": return "\uE81C";
                case "restore": return "\uE777";
                case "group": return "\uE716";
                case "theme": return "\uE790";
                case "settings": return "\uE713";
                default: return "\uE946";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class OperationBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool success = value is bool && (bool)value;
            string key = success ? "SuccessBrush" : "DangerBrush";
            if (Application.Current != null && Application.Current.Resources.Contains(key))
                return Application.Current.Resources[key];
            return success ? Brushes.SeaGreen : Brushes.IndianRed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class OperationSoftBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool success = value is bool && (bool)value;
            string key = success ? "SuccessSoftBrush" : "DangerSoftBrush";
            if (Application.Current != null && Application.Current.Resources.Contains(key))
                return Application.Current.Resources[key];
            return success ? Brushes.Honeydew : Brushes.MistyRose;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool empty = string.IsNullOrWhiteSpace(value as string);
            bool invert = string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase);
            if (invert) empty = !empty;
            return empty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
