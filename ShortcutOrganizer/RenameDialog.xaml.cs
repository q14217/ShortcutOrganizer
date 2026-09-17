using System;
using System.Windows;

namespace ShortcutOrganizer
{
    public partial class RenameDialog : Window
    {
        public string NewName { get; private set; }

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            NewNameTextBox.Text = currentName;
            NewNameTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewNameTextBox.Text))
            {
                MessageBox.Show("请输入名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            NewName = NewNameTextBox.Text.Trim();
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