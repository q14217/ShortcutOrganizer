using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Diagnostics;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        // ==================== 撤销系统 ====================

        private const int MaxUndoSteps = 30;
        private readonly Stack<UndoStep> _undoStack = new Stack<UndoStep>();

        public class UndoStep
        {
            public string Description { get; set; }
            public Action Undo { get; set; }
            public DateTime Time { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// 记录一次可撤销的操作
        /// </summary>
        private void PushUndo(string description, Action undoAction)
        {
            if (undoAction == null) return;

            _undoStack.Push(new UndoStep
            {
                Description = description,
                Undo = undoAction
            });

            // 控制栈深度
            if (_undoStack.Count > MaxUndoSteps)
            {
                var arr = _undoStack.ToArray();   // [0]=栈顶
                _undoStack.Clear();
                for (int i = MaxUndoSteps - 1; i >= 0; i--)
                    _undoStack.Push(arr[i]);
            }

            Logger.Info($"[Undo] push: {description}, 栈深 {_undoStack.Count}");
        }

        /// <summary>
        /// 执行撤销
        /// </summary>
        private void PerformUndo()
        {
            if (_undoStack.Count == 0)
            {
                if (StatusText != null) StatusText.Text = "没有可撤销的操作";
                return;
            }

            var step = _undoStack.Pop();
            try
            {
                step.Undo?.Invoke();
                UpdateCategoryCounts();
                RefreshCurrentView();
                ScheduleSave();
                if (StatusText != null)
                    StatusText.Text = $"已撤销：{step.Description}";
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "撤销失败");
                MessageBox.Show($"撤销失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 工具栏"撤销"按钮（如果 XAML 中加了）
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            PerformUndo();
        }

        // ==================== 具体的撤销包装方法 ====================

        /// <summary>
        /// 记录并执行"删除快捷方式"操作
        /// </summary>
        private void DeleteShortcutsWithUndo(List<ShortcutItem> toDelete, string description)
        {
            if (toDelete == null || toDelete.Count == 0) return;

            // 记录每个被删项的原索引
            var withIndex = toDelete
                .Select(s => new { Item = s, Index = _allShortcuts.IndexOf(s) })
                .OrderBy(x => x.Index)
                .ToList();

            foreach (var x in withIndex)
                _allShortcuts.Remove(x.Item);

            PushUndo(description, () =>
            {
                foreach (var x in withIndex)
                {
                    if (_allShortcuts.Contains(x.Item)) continue;

                    if (x.Index >= 0 && x.Index <= _allShortcuts.Count)
                        _allShortcuts.Insert(x.Index, x.Item);
                    else
                        _allShortcuts.Add(x.Item);
                }
            });
        }

        /// <summary>
        /// 记录并执行"移动快捷方式到分类"操作
        /// </summary>
        private void MoveShortcutsWithUndo(List<ShortcutItem> toMove, string targetCategory, string description)
        {
            if (toMove == null || toMove.Count == 0) return;

            // 记录原分类
            var original = toMove.ToDictionary(s => s, s => s.Category);

            foreach (var s in toMove)
                s.Category = targetCategory;

            PushUndo(description, () =>
            {
                foreach (var kv in original)
                    kv.Key.Category = kv.Value;
            });
        }

        /// <summary>
        /// 记录并执行"重命名快捷方式"操作
        /// </summary>
        private void RenameShortcutInternal(ShortcutItem item, string newName)
        {
            if (item == null || string.IsNullOrWhiteSpace(newName)) return;
            if (item.Name == newName) return;

            string oldName = item.Name;
            item.Name = newName;

            PushUndo($"重命名「{oldName}」为「{newName}」", () =>
            {
                item.Name = oldName;
            });

            RefreshCurrentView();
            ScheduleSave();
            if (StatusText != null) StatusText.Text = $"已重命名为：{newName}";
        }
    }
}