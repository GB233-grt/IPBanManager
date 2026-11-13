using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IPBanManager
{
    public partial class ConfirmDialog : Window
    {
        public ConfirmDialog()
        {
            InitializeComponent();
            Opacity = 0;
            Loaded += delegate
            {
                DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
                BeginAnimation(OpacityProperty, fade);
            };
        }

        public static bool Show(Window owner, string title, string message,
            string confirmText, string cancelText, bool danger)
        {
            ConfirmDialog dialog = new ConfirmDialog();
            dialog.Title = title;
            dialog.Owner = owner != null && owner.IsVisible ? owner : null;
            dialog.WindowStartupLocation = dialog.Owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner;
            dialog.TitleText.Text = title;
            dialog.MessageText.Text = message;
            dialog.ConfirmButton.Content = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;
            if (string.IsNullOrWhiteSpace(cancelText))
            {
                dialog.CancelButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                dialog.CancelButton.Content = cancelText;
            }

            if (danger)
            {
                dialog.ConfirmButton.Style = (Style)dialog.FindResource("DangerButtonStyle");
                dialog.TitleIconBadge.Background = (Brush)dialog.FindResource("DangerSoftBrush");
                dialog.TitleIcon.Foreground = (Brush)dialog.FindResource("DangerBrush");
                dialog.TitleIcon.Text = "\uE7BA";
            }
            else
            {
                dialog.TitleIconBadge.Background = (Brush)dialog.FindResource("AccentSoftBrush");
                dialog.TitleIcon.Foreground = (Brush)dialog.FindResource("AccentBrush");
                dialog.TitleIcon.Text = "\uE946";
            }

            bool? result = dialog.ShowDialog();
            return result == true;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) DialogResult = false;
            base.OnKeyDown(e);
        }
    }
}
