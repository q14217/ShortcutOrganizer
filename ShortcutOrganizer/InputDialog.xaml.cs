using System.Collections.Generic;
using System.Windows;

namespace ShortcutOrganizer
{
    public partial class InputDialog : Window
    {
        public string Result { get; private set; }

        public InputDialog(string title, string prompt, List<string> existingCategories)
        {
            InitializeComponent();
            Title = title;
            Prompt = prompt;
            ExistingCategories = existingCategories;
            DataContext = this;
        }

        public string Prompt { get; }
        public List<string> ExistingCategories { get; }
        public string InputText { get; set; }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = CategoryComboBox.Text.Trim();
            if (string.IsNullOrEmpty(Result))
            {
                MessageBox.Show("请输入分类名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}