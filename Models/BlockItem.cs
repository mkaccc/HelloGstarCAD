using System.ComponentModel;
using System.Runtime.CompilerServices;
using GrxCAD.Geometry;
using GrxCAD.DatabaseServices;

namespace HelloGstarCAD.Models
{
    public class BlockItem : INotifyPropertyChanged
    {
        private string _attributeValue;
        private int _index;
        private string _originalAttributeValue;

        // 关键：使用图块名称作为唯一标识
        public string UniqueKey => BlockName;
        
        public string BlockName { get; set; }
        
        // 块属性相关
        public string AttributeTag { get; set; } = "A";
        
        // 从CAD读取的原始属性值（不可修改）
        public string OriginalAttributeValue
        {
            get => _originalAttributeValue;
            set
            {
                if (_originalAttributeValue != value)
                {
                    _originalAttributeValue = value;
                    OnPropertyChanged();
                }
            }
        }
        
        // 当前显示的值（可作为标题修改）
        public string AttributeValue
        {
            get => _attributeValue;
            set
            {
                if (_attributeValue != value)
                {
                    _attributeValue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }
        
        // 显示控制
        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }
        
        // 显示序号 + 当前值（可作为标题修改）
        public string DisplayName => $"{Index + 1}. {AttributeValue}";
        
        // 图块类型提示（用于颜色转换器）
        public string BlockTypeHint 
        { 
            get
            {
                if (string.IsNullOrEmpty(BlockName)) return "未知";
                string name = BlockName.ToLower();
                if (name.Contains("door")) return "门";
                if (name.Contains("window")) return "窗";
                if (name.Contains("chair") || name.Contains("desk")) return "家具";
                if (name.Contains("equip") || name.Contains("device")) return "设备";
                return "图块";
            }
        }

        // 存储一个ObjectId示例，用于修改属性时参考
        public ObjectId ExampleBlockId { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}