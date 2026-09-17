using System;
using System.Windows;

namespace ShortcutOrganizer
{
    public partial class AddCategoryDialog : Window
    {
        public string CategoryName { get; private set; }

        public AddCategoryDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CategoryNameTextBox.Text))
            {
                MessageBox.Show("请输入分类名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CategoryName = CategoryNameTextBox.Text.Trim();
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