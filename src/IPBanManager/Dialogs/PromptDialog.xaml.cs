using System.Windows;
using System.Windows.Input;

namespace IPBanManager
{
    public partial class PromptDialog : Window
    {
        public PromptDialog()
        {
            InitializeComponent();
            Loaded += delegate
            {
                InputBox.Focus();
                InputBox.SelectAll();
            };
        }

        public static string Show(Window owner, string title, string label, string initial, string hint)
        {
            PromptDialog dialog = new PromptDialog();
            dialog.Title = title;
            dialog.TitleText.Text = title;
            dialog.LabelText.Text = label;
            dialog.InputBox.Text = initial ?? "";
            if (!string.IsNullOrWhiteSpace(hint))
                dialog.LabelText.Text = label + "　" + hint;
            dialog.Owner = owner != null && owner.IsVisible ? owner : null;
            dialog.WindowStartupLocation = dialog.Owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner;
            bool? result = dialog.ShowDialog();
            return result == true ? dialog.InputBox.Text.Trim() : null;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputBox.Text))
            {
                ErrorText.Text = "内容不能为空";
                ErrorText.Visibility = Visibility.Visible;
                InputBox.Focus();
                return;
            }
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
