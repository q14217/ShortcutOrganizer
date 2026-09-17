using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShortcutOrganizer
{
    public partial class ShortcutsHelpWindow : Window
    {
        private class Entry
        {
            public string Group { get; set; }
            public string Keys { get; set; }
            public string Desc { get; set; }
        }

        public ShortcutsHelpWindow()
        {
            InitializeComponent();
            BuildList();
        }

        private void BuildList()
        {
            var entries = new List<Entry>
            {
                new Entry { Group = "通用", Keys = "Ctrl + Alt + S", Desc = "全局呼出主窗口（可在设置中修改）" },
                new Entry { Group = "通用", Keys = "F1",             Desc = "显示此快捷键说明" },
                new Entry { Group = "通用", Keys = "Esc",            Desc = "清空搜索框" },
                new Entry { Group = "分类", Keys = "Ctrl + N",       Desc = "新建分类" },
                new Entry { Group = "搜索", Keys = "Ctrl + F",       Desc = "聚焦搜索框" },
                new Entry { Group = "列表", Keys = "Ctrl + A",       Desc = "全选当前列表" },
                new Entry { Group = "列表", Keys = "Delete",         Desc = "删除选中项" },
                new Entry { Group = "列表", Keys = "F2",             Desc = "重命名选中项" },
                new Entry { Group = "列表", Keys = "Enter",          Desc = "打开选中项" },
                new Entry { Group = "列表", Keys = "F5",             Desc = "扫描失效项" },
                null,
                new Entry { Group = "撤销", Keys = "Ctrl + Z",       Desc = "撤销上一步操作" },
                new Entry { Group = "视图", Keys = "Ctrl + L",       Desc = "切换卡片/列表视图" },
            };

            string lastGroup = null;
            foreach (var e in entries)
            {
                if (e == null)
                {
                    ShortcutList.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
                    continue;
                }

                if (e.Group != lastGroup)
                {
                    var title = new TextBlock
                    {
                        Text = e.Group,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 8, 0, 4),
                        Foreground = (Brush)FindResource("AccentBrush")
                    };
                    ShortcutList.Children.Add(title);
                    lastGroup = e.Group;
                }

                var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var keyBox = new Border
                {
                    Background = (Brush)FindResource("SurfaceBrush"),
                    BorderBrush = (Brush)FindResource("LineBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock
                    {
                        Text = e.Keys,
                        FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                        FontSize = 11.5
                    }
                };
                Grid.SetColumn(keyBox, 0);
                grid.Children.Add(keyBox);

                var desc = new TextBlock
                {
                    Text = e.Desc,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                Grid.SetColumn(desc, 1);
                grid.Children.Add(desc);

                ShortcutList.Children.Add(grid);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}