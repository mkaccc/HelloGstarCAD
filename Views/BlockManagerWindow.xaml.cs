using GrxCAD.DatabaseServices;
using GrxCAD.ApplicationServices;
using GrxCAD.EditorInput;
using System;
using System.Windows;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using HelloGstarCAD.Models;
using HelloGstarCAD.Services;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;

namespace HelloGstarCAD.Views
{
    public partial class BlockManagerWindow : Window
    {
        private ObservableCollection<BlockItem> _blockItems;
        private CadInteractionService _cadService;
        private ObjectId? _selectedPolylineId;
        
        // 新增：拖拽相关字段
        private int _dragStartIndex = -1;
        private Point _dragStartPoint;
        private ListBoxItem _lastDragOverItem;

        public BlockManagerWindow()
        {
            InitializeComponent();
            _blockItems = new ObservableCollection<BlockItem>();
            LbBlocks.ItemsSource = _blockItems;
            _cadService = new CadInteractionService();
            _selectedPolylineId = null;
            _lastDragOverItem = null;
            
            // 关联事件处理程序
            BtnSelectBlocks.Click += BtnSelectBlocks_Click;
            BtnClearList.Click += BtnClearList_Click;
            BtnSelectPath.Click += BtnSelectPath_Click;
            BtnStartNumbering.Click += BtnStartNumbering_Click;
            BtnClose.Click += BtnClose_Click;
            
            // 添加双击列表项编辑功能
            LbBlocks.MouseDoubleClick += LbBlocks_MouseDoubleClick;
            
            // 输入框变化时更新预览
            TxtPrefix.TextChanged += (s, e) => UpdateNumberTemplatePreview();
            TxtSuffix.TextChanged += (s, e) => UpdateNumberTemplatePreview();
            TxtStartNum.TextChanged += (s, e) => UpdateNumberTemplatePreview();
            
            UpdateNumberTemplatePreview();
        }

        private void BtnSelectBlocks_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                var newBlocks = _cadService.SelectBlocks("A");
                if (newBlocks.Count > 0)
                {
                    var existingBlockNames = new HashSet<string>(_blockItems.Select(b => b.BlockName));
                    int addedCount = 0;
                    
                    foreach (var block in newBlocks)
                    {
                        if (!existingBlockNames.Contains(block.BlockName))
                        {
                            block.Index = _blockItems.Count;
                            _blockItems.Add(block);
                            existingBlockNames.Add(block.BlockName);
                            addedCount++;
                        }
                    }
                    
                    if (addedCount > 0)
                    {
                        UpdateListIndexes();
                        var ed = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                        ed.WriteMessage($"\n成功添加 {addedCount} 种不重复的图块类型到列表\n");
                    }
                    else
                    {
                        var ed = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                        ed.WriteMessage($"\n所有图块类型都已存在于列表中\n");
                    }
                }
                else
                {
                    var ed = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                    ed.WriteMessage($"\n未选择到有效图块\n");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择图块时出错: {ex.Message}");
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void BtnClearList_Click(object sender, RoutedEventArgs e)
        {
            if (_blockItems.Count > 0)
            {
                var result = MessageBox.Show($"确定要清空列表中的 {_blockItems.Count} 种图块类型吗？", 
                    "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _blockItems.Clear();
                    _selectedPolylineId = null;
                    TxtPrefix.Text = "No.";
                    TxtSuffix.Text = "";
                    TxtStartNum.Text = "1";
                    UpdateNumberTemplatePreview();
                    
                    var ed = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                    ed.WriteMessage("\n图块列表已清空。\n");
                }
            }
        }

        private void LbBlocks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LbBlocks.SelectedItem is BlockItem selectedBlock)
            {
                var inputDialog = new InputDialog(
                    "修改标题",
                    $"修改图块 '{selectedBlock.BlockName}' 的标题（用于编号显示）：",
                    selectedBlock.AttributeValue);
                
                if (inputDialog.ShowDialog() == true)
                {
                    string newValue = inputDialog.ResultText;
                    if (!string.IsNullOrWhiteSpace(newValue) && newValue != selectedBlock.AttributeValue)
                    {
                        selectedBlock.AttributeValue = newValue;
                        UpdateNumberTemplatePreview();
                        
                        var ed = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                        ed.WriteMessage($"\n已将图块 '{selectedBlock.BlockName}' 的标题改为 '{newValue}'\n");
                    }
                }
            }
        }

        private void BtnSelectPath_Click(object sender, RoutedEventArgs e)
        {
            if (_blockItems.Count == 0)
            {
                MessageBox.Show("请先选择至少一种图块。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            this.Hide();
            try
            {
                var polylineId = _cadService.SelectPolyline();
                if (polylineId.HasValue)
                {
                    _selectedPolylineId = polylineId.Value;
                    var ed = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                    ed.WriteMessage("\n编号路径选择成功！\n");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择多段线时出错: {ex.Message}");
            }
            finally
            {
                this.ShowDialog();
            }
        }

        private void BtnStartNumbering_Click(object sender, RoutedEventArgs e)
        {
            if (_blockItems.Count == 0)
            {
                MessageBox.Show("图块列表为空，请先选择图块。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!_selectedPolylineId.HasValue)
            {
                MessageBox.Show("请先选择一条编号路径。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(TxtStartNum.Text, out int startNumber) || startNumber < 0)
            {
                MessageBox.Show("起始数字必须是一个有效的非负整数。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string previewTitle = _blockItems[0].AttributeValue;
                var confirmResult = MessageBox.Show($"将为 {_blockItems.Count} 种图块创建沿线编号。\n格式: {TxtPrefix.Text}{startNumber}{previewTitle}{TxtSuffix.Text}\n\n是否继续？", 
                    "确认沿线编号", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (confirmResult == MessageBoxResult.Yes)
                {
                    string prefix = TxtPrefix.Text;
                    string suffix = TxtSuffix.Text;
                    
                    _cadService.PlaceNumbersAlongPolyline(_selectedPolylineId.Value, 
                        _blockItems.ToList(), prefix, suffix, startNumber);
                    
                    MessageBox.Show($"✅ 沿线编号完成！\n共为 {_blockItems.Count} 种图块创建了编号。", 
                        "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编号过程中发生错误:\n{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void UpdateListIndexes()
        {
            for (int i = 0; i < _blockItems.Count; i++)
            {
                _blockItems[i].Index = i;
            }
            var tempList = new ObservableCollection<BlockItem>(_blockItems);
            LbBlocks.ItemsSource = null;
            LbBlocks.ItemsSource = _blockItems;
            UpdateNumberTemplatePreview();
        }

        private void UpdateNumberTemplatePreview()
        {
            if (int.TryParse(TxtStartNum.Text, out int startNum))
            {
                string titleValue = _blockItems.Count > 0 ? _blockItems[0].AttributeValue : "标题";
                TxtPreview.Text = $"预览: {TxtPrefix.Text}{startNum}{titleValue}{TxtSuffix.Text}";
            }
            else
            {
                TxtPreview.Text = "预览: 起始数字无效";
            }
        }

        // ========== 新增：拖拽排序相关方法 ==========

        private void LbBlocks_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            
            // 使用修复后的FindVisualParent方法，避免Run元素问题
            var item = FindVisualParent<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                _dragStartIndex = LbBlocks.ItemContainerGenerator.IndexFromContainer(item);
                LbBlocks.SelectedIndex = _dragStartIndex;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (e.LeftButton == MouseButtonState.Pressed && _dragStartIndex >= 0)
            {
                Point currentPoint = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var item = LbBlocks.ItemContainerGenerator.ContainerFromIndex(_dragStartIndex) as ListBoxItem;
                    if (item != null)
                    {
                        var dragData = new DataObject(DataFormats.StringFormat, _dragStartIndex.ToString());
                        dragData.SetData("SourceIndex", _dragStartIndex);
                        
                        DragDrop.DoDragDrop(item, dragData, DragDropEffects.Move);
                        
                        _dragStartIndex = -1;
                    }
                }
            }
        }

        private void LbBlocks_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            
            // 使用修复后的FindVisualParent方法
            var currentItem = FindVisualParent<ListBoxItem>((DependencyObject)e.OriginalSource);
            
            if (currentItem != null && currentItem != _lastDragOverItem)
            {
                if (_lastDragOverItem != null)
                {
                    ClearDragEffect(_lastDragOverItem);
                }
                
                ApplyDragEffect(currentItem);
                _lastDragOverItem = currentItem;
            }
            else if (currentItem == null && _lastDragOverItem != null)
            {
                ClearDragEffect(_lastDragOverItem);
                _lastDragOverItem = null;
            }
        }

        private void LbBlocks_DragLeave(object sender, DragEventArgs e)
        {
            if (_lastDragOverItem != null)
            {
                ClearDragEffect(_lastDragOverItem);
                _lastDragOverItem = null;
            }
        }

        private void LbBlocks_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("SourceIndex")) return;
            
            int sourceIndex = (int)e.Data.GetData("SourceIndex");
            // 使用修复后的FindVisualParent方法
            var targetItem = FindVisualParent<ListBoxItem>((DependencyObject)e.OriginalSource);
            
            if (targetItem != null)
            {
                int targetIndex = LbBlocks.ItemContainerGenerator.IndexFromContainer(targetItem);
                
                if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
                {
                    ReorderBlockList(sourceIndex, targetIndex);
                    
                    var ed = GrxCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                    ed.WriteMessage($"\n已重新排序：将第 {sourceIndex + 1} 项移动到第 {targetIndex + 1} 位\n");
                }
            }
            
            if (_lastDragOverItem != null)
            {
                ClearDragEffect(_lastDragOverItem);
                _lastDragOverItem = null;
            }
        }

        private void ReorderBlockList(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= _blockItems.Count ||
                newIndex < 0 || newIndex >= _blockItems.Count)
                return;
            
            var itemToMove = _blockItems[oldIndex];
            _blockItems.RemoveAt(oldIndex);
            _blockItems.Insert(newIndex, itemToMove);
            UpdateListIndexes();
            LbBlocks.SelectedIndex = newIndex;
        }

        private void ApplyDragEffect(ListBoxItem item)
        {
            var border = FindVisualChild<Border>(item, "Bd");
            if (border != null)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(80, 255, 241, 118));
                border.BorderBrush = Brushes.Orange;
                border.BorderThickness = new Thickness(1);
            }
        }

        private void ClearDragEffect(ListBoxItem item)
        {
            var border = FindVisualChild<Border>(item, "Bd");
            if (border != null)
            {
                border.ClearValue(Border.BackgroundProperty);
                border.ClearValue(Border.BorderBrushProperty);
                border.ClearValue(Border.BorderThicknessProperty);
            }
        }

        // 修复的FindVisualParent方法：处理Run等非Visual元素
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            // 向上查找，直到找到指定类型的父元素或到达根元素
            while (child != null)
            {
                // 检查当前元素是否为目标类型
                if (child is T parent)
                    return parent;

                // 尝试获取视觉父元素
                DependencyObject visualParent = null;
                
                // 对于Run等非Visual元素，先尝试获取逻辑父元素
                if (child is Run)
                {
                    var frameworkContentElement = child as FrameworkContentElement;
                    if (frameworkContentElement != null)
                    {
                        visualParent = frameworkContentElement.Parent;
                    }
                }
                
                // 如果无法通过上述方式获取父元素，则使用VisualTreeHelper
                if (visualParent == null)
                {
                    // 只有Visual元素才能使用VisualTreeHelper
                    if (child is Visual)
                    {
                        visualParent = VisualTreeHelper.GetParent(child);
                    }
                    else
                    {
                        // 对于其他非Visual元素，尝试使用LogicalTreeHelper
                        visualParent = LogicalTreeHelper.GetParent(child);
                    }
                }
                
                child = visualParent;
            }
            
            return null;
        }

        private static T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T result && (childName == null || (child is FrameworkElement fe && fe.Name == childName)))
                    return result;
                
                var childResult = FindVisualChild<T>(child, childName);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }
    }

    // 输入对话框类
    public class InputDialog : Window
    {
        public string ResultText { get; private set; }
        private TextBox textBox;
        
        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            this.Title = title;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.ResizeMode = ResizeMode.NoResize;
            
            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300
            });
            
            textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 15),
                Width = 250,
                MinHeight = 25
            };
            textBox.Focus();
            textBox.SelectAll();
            stackPanel.Children.Add(textBox);
            
            var buttonPanel = new StackPanel { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right 
            };
            var okButton = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "取消", Width = 80 };
            
            okButton.Click += (s, e) => { ResultText = textBox.Text; DialogResult = true; };
            cancelButton.Click += (s, e) => { DialogResult = false; };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);
            
            this.Content = stackPanel;
            
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    ResultText = textBox.Text;
                    DialogResult = true;
                }
            };
        }
    }
}