using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

// 关键修改：针对 .NET Framework 项目，使用此命名空间
using System.Windows.Interactivity;

namespace HelloGstarCAD.Views
{
    /// <summary>
    /// 为 ListView 启用长按拖动排序的行为。
    /// </summary>
    public class ListViewDragDropBehavior : Behavior<ListView>
    {
        private object _draggedItem;
        private Point _startPoint;
        private bool _isMouseDown;
        private DispatcherTimer _longPressTimer;
        private const int LongPressDurationMs = 500; // 长按判定时间（毫秒）

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            AssociatedObject.Drop += OnDrop;
            AssociatedObject.AllowDrop = true;

            // 初始化长按计时器
            _longPressTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(LongPressDurationMs)
            };
            _longPressTimer.Tick += OnLongPressTimerTick;
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            _startPoint = e.GetPosition(null);

            // 获取鼠标下的列表项
            var hitTest = VisualTreeHelper.HitTest(AssociatedObject, e.GetPosition(AssociatedObject));
            if (hitTest?.VisualHit is DependencyObject element)
            {
                var container = FindAncestor<ListViewItem>(element);
                if (container != null)
                {
                    _draggedItem = container.DataContext;
                    // 启动长按计时器
                    _longPressTimer.Start();
                    e.Handled = true;
                    return;
                }
            }
            _draggedItem = null;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || _draggedItem == null) return;

            var currentPoint = e.GetPosition(null);
            var diff = _startPoint - currentPoint;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                _longPressTimer.Stop(); // 移动距离达标，停止计时器
                StartDragDrop();
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ResetState();
        }

        private void OnLongPressTimerTick(object sender, EventArgs e)
        {
            _longPressTimer.Stop();
            if (_isMouseDown && _draggedItem != null)
            {
                StartDragDrop();
            }
        }

        private void StartDragDrop()
        {
            if (_draggedItem == null) return;

            var data = new DataObject(typeof(object), _draggedItem);
            DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);
            ResetState();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null) return;

            // 获取目标位置
            var targetItem = FindListViewItemUnderMouse(AssociatedObject, e.GetPosition(AssociatedObject));
            if (targetItem?.DataContext == null || targetItem.DataContext == _draggedItem)
            {
                ResetState();
                return;
            }

            var sourceIndex = AssociatedObject.Items.IndexOf(_draggedItem);
            var targetIndex = AssociatedObject.Items.IndexOf(targetItem.DataContext);

            if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
            {
                // 获取 ViewModel 并执行排序命令
                if (AssociatedObject.DataContext is ViewModels.BlockManagerViewModel viewModel)
                {
                    viewModel.ReorderCommand.Execute(new Tuple<int, int>(sourceIndex, targetIndex));
                }
            }

            e.Handled = true;
            ResetState();
        }

        // 辅助方法：在视觉树中查找特定类型的父容器
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
        }

        private static ListViewItem FindListViewItemUnderMouse(ListView listView, Point position)
        {
            var hitTest = VisualTreeHelper.HitTest(listView, position);
            return hitTest?.VisualHit != null ? FindAncestor<ListViewItem>(hitTest.VisualHit) : null;
        }

        private void ResetState()
        {
            _isMouseDown = false;
            _draggedItem = null;
            _longPressTimer.Stop();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            AssociatedObject.Drop -= OnDrop;

            if (_longPressTimer != null)
            {
                _longPressTimer.Stop();
                _longPressTimer.Tick -= OnLongPressTimerTick;
                _longPressTimer = null;
            }
        }
    }
}