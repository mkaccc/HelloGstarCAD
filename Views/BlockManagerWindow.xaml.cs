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
// 添加必要的 using 指令
using System.Windows.Controls;  // 包含 TextBox 等控件
using System.Windows.Input;     // 包含 Key 枚举

namespace HelloGstarCAD.Views
{
    public partial class BlockManagerWindow : Window
    {
        private ObservableCollection<BlockItem> _blockItems;
        private CadInteractionService _cadService;
        private ObjectId? _selectedPolylineId;

        public BlockManagerWindow()
        {
            InitializeComponent();
            _blockItems = new ObservableCollection<BlockItem>();
            LbBlocks.ItemsSource = _blockItems;
            _cadService = new CadInteractionService();
            _selectedPolylineId = null;
            
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
                var newBlocks = _cadService.SelectBlocks("A"); // 读取属性"A"
                if (newBlocks.Count > 0)
                {
                    // 使用图块名称进行去重
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
                // 创建输入对话框
                var inputDialog = new InputDialog(
                    "修改标题",
                    $"修改图块 '{selectedBlock.BlockName}' 的标题（用于编号显示）：",
                    selectedBlock.AttributeValue);  // 修改的是AttributeValue（作为标题）
                
                if (inputDialog.ShowDialog() == true)
                {
                    string newValue = inputDialog.ResultText;
                    if (!string.IsNullOrWhiteSpace(newValue) && newValue != selectedBlock.AttributeValue)
                    {
                        // 更新AttributeValue（作为标题）
                        selectedBlock.AttributeValue = newValue;
                        UpdateNumberTemplatePreview();
                        
                        // 显示提示信息
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
                // 使用AttributeValue（作为标题）作为预览
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
            // 刷新列表显示
            var tempList = new ObservableCollection<BlockItem>(_blockItems);
            LbBlocks.ItemsSource = null;
            LbBlocks.ItemsSource = _blockItems;
            UpdateNumberTemplatePreview();
        }

        private void UpdateNumberTemplatePreview()
        {
            if (int.TryParse(TxtStartNum.Text, out int startNum))
            {
                // 使用AttributeValue（作为标题）作为预览
                string titleValue = _blockItems.Count > 0 ? _blockItems[0].AttributeValue : "标题";
                TxtPreview.Text = $"预览: {TxtPrefix.Text}{startNum}{titleValue}{TxtSuffix.Text}";
            }
            else
            {
                TxtPreview.Text = "预览: 起始数字无效";
            }
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
            
            // 提示文本
            stackPanel.Children.Add(new TextBlock 
            { 
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300
            });
            
            // 输入框
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
            
            // 按钮
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
            
            // 按回车确认
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