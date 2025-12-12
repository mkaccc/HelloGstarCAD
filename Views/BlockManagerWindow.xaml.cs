using System.Windows;
using HelloGstarCAD.ViewModels;

namespace HelloGstarCAD.Views
{
    public partial class BlockManagerWindow : Window
    {
        public BlockManagerWindow()
        {
            InitializeComponent();
            // 初始化 ViewModel 并设置为数据上下文
            this.DataContext = new BlockManagerViewModel();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = (BlockManagerViewModel)DataContext;
            viewModel.SearchBlocks(SearchTextBox.Text);
        }

        private void AddSelectedBlocksButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = (BlockManagerViewModel)DataContext;
            viewModel.AddSelectedBlocks();

            // 注意：原更新“已选: X种图块”的代码已移除
            // StatusTextBlock.Text = $"已选: {viewModel.BlockItems.Count}种图块";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}