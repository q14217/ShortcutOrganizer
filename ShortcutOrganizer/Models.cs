using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShortcutOrganizer
{
    public class CategoryInfo : INotifyPropertyChanged
    {
        private string _name;
        private int _shortcutCount;
        private int _sortOrder;

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(DisplayName)); } }
        }
        public int ShortcutCount
        {
            get => _shortcutCount;
            set { if (_shortcutCount != value) { _shortcutCount = value; OnPropertyChanged(nameof(ShortcutCount)); OnPropertyChanged(nameof(DisplayName)); } }
        }
        public int SortOrder
        {
            get => _sortOrder;
            set { if (_sortOrder != value) { _sortOrder = value; OnPropertyChanged(nameof(SortOrder)); } }
        }
        public string DisplayName => $"{Name} ({ShortcutCount})";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class ShortcutItem : INotifyPropertyChanged
    {
        private string _name, _targetPath, _arguments, _category, _sourcePath;
        private DateTime _createdTime;
        private BitmapSource _icon;
        private int _openCount;
        private DateTime? _lastOpenedTime;
        private bool _isValid = true;
        private int _sortOrder;
        private bool _isPinned;

        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
        public string TargetPath { get => _targetPath; set { if (_targetPath != value) { _targetPath = value; OnPropertyChanged(nameof(TargetPath)); } } }
        public string Arguments { get => _arguments; set { if (_arguments != value) { _arguments = value; OnPropertyChanged(nameof(Arguments)); } } }
        public string Category { get => _category; set { if (_category != value) { _category = value; OnPropertyChanged(nameof(Category)); } } }
        public string SourcePath { get => _sourcePath; set { if (_sourcePath != value) { _sourcePath = value; OnPropertyChanged(nameof(SourcePath)); } } }
        public DateTime CreatedTime { get => _createdTime; set { if (_createdTime != value) { _createdTime = value; OnPropertyChanged(nameof(CreatedTime)); } } }
        public BitmapSource Icon { get => _icon; set { if (_icon != value) { _icon = value; OnPropertyChanged(nameof(Icon)); } } }
        public int OpenCount { get => _openCount; set { if (_openCount != value) { _openCount = value; OnPropertyChanged(nameof(OpenCount)); } } }
        public DateTime? LastOpenedTime { get => _lastOpenedTime; set { if (_lastOpenedTime != value) { _lastOpenedTime = value; OnPropertyChanged(nameof(LastOpenedTime)); } } }
        public bool IsValid { get => _isValid; set { if (_isValid != value) { _isValid = value; OnPropertyChanged(nameof(IsValid)); } } }
        public int SortOrder { get => _sortOrder; set { if (_sortOrder != value) { _sortOrder = value; OnPropertyChanged(nameof(SortOrder)); } } }
        public bool IsPinned { get => _isPinned; set { if (_isPinned != value) { _isPinned = value; OnPropertyChanged(nameof(IsPinned)); } } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class BackupData
    {
        public List<CategoryInfo> Categories { get; set; } = new List<CategoryInfo>();
        public List<ShortcutItemBackup> Shortcuts { get; set; } = new List<ShortcutItemBackup>();
    }

    public class ShortcutItemBackup
    {
        public string Name { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string Category { get; set; }
        public string SourcePath { get; set; }
        public DateTime CreatedTime { get; set; }
        public int OpenCount { get; set; }
        public DateTime? LastOpenedTime { get; set; }
        public bool IsPinned { get; set; }
    }
}