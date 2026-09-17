using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        // ==================== 分类拖拽排序 ====================

        private void CategoryListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _catDragStartPoint = e.GetPosition(null);
        }

        private void CategoryListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            Point pos = e.GetPosition(null);
            Vector diff = _catDragStartPoint - pos;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var lbItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (lbItem == null) return;

            var cat = lbItem.DataContext as CategoryInfo;
            if (cat == null || cat == _favoritesCategory || cat == _recentCategory) return;

            var data = new DataObject(CategoryReorderFormat, cat);
            try
            {
                DragDrop.DoDragDrop(lbItem, data, DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "分类拖拽失败");
            }
        }

        // ==================== 拖拽到分类：高亮 ====================

        private ListBoxItem _lastHighlightedCategoryItem;

        private void CategoryListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(CategoryReorderFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(ShortcutDragFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                HighlightCategoryUnderCursor(e);
                return;
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void CategoryListBox_DragLeave(object sender, DragEventArgs e)
        {
            ClearCategoryHighlight();
        }

        private void HighlightCategoryUnderCursor(DragEventArgs e)
        {
            try
            {
                var lbItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (lbItem == _lastHighlightedCategoryItem) return;

                ClearCategoryHighlight();

                if (lbItem != null)
                {
                    var cat = lbItem.DataContext as CategoryInfo;
                    if (cat == _favoritesCategory || cat == _recentCategory)
                        return;   // 不允许拖到虚拟分类

                    lbItem.SetValue(Control.BorderBrushProperty,
                        (Brush)FindResource("AccentBrush"));
                    lbItem.SetValue(Control.BorderThicknessProperty, new Thickness(2));
                    lbItem.SetValue(Control.BackgroundProperty,
                        (Brush)FindResource("AccentLightBrush"));
                    _lastHighlightedCategoryItem = lbItem;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"分类高亮失败: {ex.Message}");
            }
        }

        private void ClearCategoryHighlight()
        {
            if (_lastHighlightedCategoryItem != null)
            {
                _lastHighlightedCategoryItem.ClearValue(Control.BorderBrushProperty);
                _lastHighlightedCategoryItem.ClearValue(Control.BorderThicknessProperty);
                _lastHighlightedCategoryItem.ClearValue(Control.BackgroundProperty);
                _lastHighlightedCategoryItem = null;
            }
        }

        // ==================== 拖拽到分类：Drop ====================

        private void CategoryListBox_Drop(object sender, DragEventArgs e)
        {
            ClearCategoryHighlight();

            if (e.Data.GetDataPresent(CategoryReorderFormat))
            {
                HandleCategoryReorderDrop(e);
                return;
            }

            if (e.Data.GetDataPresent(ShortcutDragFormat))
            {
                HandleShortcutDropOnCategory(e);
                return;
            }

            e.Handled = true;
        }

        private void HandleCategoryReorderDrop(DragEventArgs e)
        {
            var dragged = e.Data.GetData(CategoryReorderFormat) as CategoryInfo;
            if (dragged == null) { e.Handled = true; return; }

            var lbItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            var target = lbItem?.DataContext as CategoryInfo;

            if (target == null || target == dragged ||
                target == _favoritesCategory || target == _recentCategory ||
                dragged == _favoritesCategory || dragged == _recentCategory)
            {
                e.Handled = true;
                return;
            }

            try
            {
                int draggedIdx = _categories.IndexOf(dragged);
                _categories.RemoveAt(draggedIdx);

                int targetIdx = _categories.IndexOf(target);
                if (targetIdx < 0) targetIdx = _categories.Count;

                // 不能排到虚拟分类前面
                int minIdx = 0;
                if (_favoritesCategory != null)
                    minIdx = _categories.IndexOf(_favoritesCategory) + 1;
                if (_recentCategory != null)
                    minIdx = Math.Max(minIdx, _categories.IndexOf(_recentCategory) + 1);
                if (targetIdx < minIdx) targetIdx = minIdx;

                _categories.Insert(targetIdx, dragged);

                int order = 0;
                foreach (var c in _categories)
                {
                    if (c == _favoritesCategory || c == _recentCategory) continue;
                    c.SortOrder = order++;
                }

                RefreshCurrentView();
                ScheduleSave();
                StatusText.Text = $"分类「{dragged.Name}」已重排";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "分类排序失败");
            }
            e.Handled = true;
        }

        private void HandleShortcutDropOnCategory(DragEventArgs e)
        {
            var dragged = e.Data.GetData(ShortcutDragFormat) as List<ShortcutItem>;
            if (dragged == null || dragged.Count == 0) { e.Handled = true; return; }

            var lbItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            var targetCategory = lbItem?.DataContext as CategoryInfo;

            if (targetCategory == null ||
                targetCategory == _favoritesCategory ||
                targetCategory == _recentCategory)
            {
                e.Handled = true;
                return;
            }

            var toMove = dragged.Where(s => s.Category != targetCategory.Name).ToList();
            if (toMove.Count == 0) { e.Handled = true; return; }

            MoveShortcutsWithUndo(toMove, targetCategory.Name,
                $"拖拽移动 {toMove.Count} 项到「{targetCategory.Name}」");

            UpdateCategoryCounts();
            RefreshCurrentView();
            ScheduleSave();
            StatusText.Text = $"已移动 {toMove.Count} 项到「{targetCategory.Name}」";
            e.Handled = true;
        }

        // ==================== 分类增删改 ====================

        private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddCategoryDialog();
            if (dialog.ShowDialog() == true) AddCategory(dialog.CategoryName);
        }

        private void AddCategoryContextMenu_Click(object sender, RoutedEventArgs e)
            => AddCategoryButton_Click(sender, e);

        private void AddCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return;
            if (_categories.Any(c => c != _favoritesCategory && c != _recentCategory &&
                c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("分类名称已存在。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int maxOrder = _categories
                .Where(c => c != _favoritesCategory && c != _recentCategory)
                .Select(c => c.SortOrder)
                .DefaultIfEmpty(-1).Max();

            _categories.Add(new CategoryInfo
            {
                Name = categoryName,
                ShortcutCount = 0,
                SortOrder = maxOrder + 1
            });
            ScheduleSave();
            StatusText.Text = $"已创建分类: {categoryName}";
        }

        private void RenameCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(CategoryListBox.SelectedItem is CategoryInfo category)) return;

            if (category == _favoritesCategory || category == _recentCategory)
            {
                MessageBox.Show("虚拟分类不能重命名。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new RenameDialog(category.Name);
            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.NewName;
                if (string.IsNullOrWhiteSpace(newName) || newName == category.Name) return;

                if (_categories.Any(c => c != category && c != _favoritesCategory &&
                    c != _recentCategory &&
                    c.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("分类名称已存在。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string oldName = category.Name;
                foreach (var s in _allShortcuts.Where(s => s.Category == oldName))
                    s.Category = newName;
                category.Name = newName;

                RefreshCurrentView();
                ScheduleSave();
                StatusText.Text = $"已重命名为: {newName}";
            }
        }

        private void DeleteCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(CategoryListBox.SelectedItem is CategoryInfo category)) return;

            if (category == _favoritesCategory || category == _recentCategory)
            {
                MessageBox.Show("虚拟分类不能删除。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var r = MessageBox.Show(
                $"确定要删除分类 \"{category.Name}\" 吗？\n该分类下的 {category.ShortcutCount} 个快捷方式将被移到'未分类'。",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                foreach (var s in _allShortcuts.Where(s => s.Category == category.Name))
                    s.Category = "未分类";
                if (!_categories.Any(c => c.Name == "未分类"))
                    _categories.Add(new CategoryInfo { Name = "未分类", ShortcutCount = 0 });
                _categories.Remove(category);
                RefreshCurrentView();
                ScheduleSave();
                StatusText.Text = $"已删除分类: {category.Name}";
            }
        }

        private void BatchDeleteCategoriesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = CategoryListBox.SelectedItems.Cast<CategoryInfo>()
                .Where(c => c != _favoritesCategory && c != _recentCategory)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show("请先选择要删除的分类（不含虚拟分类）。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(
                $"确定要删除选中的 {selected.Count} 个分类吗？\n分类下的快捷方式将移到“未分类”。",
                "确认批量删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var cat in selected)
                {
                    foreach (var s in _allShortcuts.Where(s => s.Category == cat.Name).ToList())
                        s.Category = "未分类";
                    _categories.Remove(cat);
                }
                if (!_categories.Any(c => c.Name == "未分类"))
                    _categories.Add(new CategoryInfo { Name = "未分类", ShortcutCount = 0 });

                UpdateCategoryCounts();
                RefreshCurrentView();
                ScheduleSave();
            }
        }
    }
}