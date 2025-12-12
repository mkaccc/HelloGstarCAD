using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using HelloGstarCAD.Models;
using HelloGstarCAD.Services;

namespace HelloGstarCAD.ViewModels
{
    public class BlockManagerViewModel : INotifyPropertyChanged
    {
        private readonly CadInteractionService _cadService;
        private string _searchText;
        private ObservableCollection<BlockItem> _blockItems;

        // 关键：必须声明此事件以满足 INotifyPropertyChanged 接口
        public event PropertyChangedEventHandler PropertyChanged;

        public BlockManagerViewModel()
        {
            _cadService = new CadInteractionService();
            BlockItems = new ObservableCollection<BlockItem>();
            LoadBlocksFromDrawing();

            // 初始化排序命令
            ReorderCommand = new RelayCommand<Tuple<int, int>>(ExecuteReorder);
        }

        public ObservableCollection<BlockItem> BlockItems
        {
            get => _blockItems;
            set
            {
                if (_blockItems != value)
                {
                    _blockItems = value;
                    OnPropertyChanged(nameof(BlockItems));
                    OnPropertyChanged(nameof(BlockCountString));
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                }
            }
        }

        // 绿色提示文本的属性
        public string BlockCountString => $"（已添加{BlockItems?.Count ?? 0}种图块）";

        // 排序命令
        public ICommand ReorderCommand { get; }

        private void LoadBlocksFromDrawing()
        {
            var blocks = _cadService.GetBlocksFromDrawing();
            if (blocks != null)
            {
                BlockItems = new ObservableCollection<BlockItem>(blocks);
            }
        }

        public void SearchBlocks(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                LoadBlocksFromDrawing();
                return;
            }

            var allBlocks = _cadService.GetBlocksFromDrawing();
            if (allBlocks != null)
            {
                var filtered = allBlocks
                    .Where(b => b.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                BlockItems = new ObservableCollection<BlockItem>(filtered);
            }
            else
            {
                BlockItems.Clear();
            }
        }

        public void AddSelectedBlocks()
        {
            // 调用浩辰CAD服务方法
            _cadService.AddSelectedBlocksToModel(BlockItems);
        }

        // 执行排序的方法
        private void ExecuteReorder(Tuple<int, int> indices)
        {
            if (indices == null || 
                indices.Item1 < 0 || indices.Item1 >= BlockItems.Count ||
                indices.Item2 < 0 || indices.Item2 >= BlockItems.Count ||
                indices.Item1 == indices.Item2)
                return;

            var movedItem = BlockItems[indices.Item1];
            // 在 ObservableCollection 中移动项
            BlockItems.RemoveAt(indices.Item1);
            BlockItems.Insert(indices.Item2, movedItem);
        }

        // 辅助方法：触发属性变更通知
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 简单的命令实现
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }
    }
}