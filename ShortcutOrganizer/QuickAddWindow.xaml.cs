using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace ShortcutOrganizer
{
    public partial class QuickAddWindow : Window
    {
        public string SelectedCategory { get; private set; }

        public QuickAddWindow(List<string> categories, string fileName)
        {
            InitializeComponent();

            FileNameText.Text = string.IsNullOrEmpty(fileName)
                ? "（无文件）"
                : $"文件：{Path.GetFileName(fileName)}";

            CategoryListBox.ItemsSource = categories;

            if (categories != null && categories.Count > 0)
                CategoryListBox.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryListBox.SelectedItem == null)
            {
                MessageBox.Show("请选择一个分类。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedCategory = CategoryListBox.SelectedItem as string;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}