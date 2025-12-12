using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using HelloGstarCAD.Models;

namespace HelloGstarCAD.Services
{
    [DataContract]
    public class StoredBlockItem
    {
        [DataMember]
        public string BlockName { get; set; }
        
        [DataMember]
        public string AttributeTag { get; set; }
        
        [DataMember]
        public string OriginalAttributeValue { get; set; }
        
        [DataMember]
        public string AttributeValue { get; set; }
        
        [DataMember]
        public int Index { get; set; }
    }

    public class DataStorageService
    {
        private const string StorageFileName = "HelloGstarCAD_SavedData.json";
        private readonly string _storagePath;

        public DataStorageService()
        {
            // 存储在用户的AppData目录下
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "HelloGstarCAD");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            
            _storagePath = Path.Combine(appFolder, StorageFileName);
        }

        // 保存数据
        public bool SaveBlockItems(List<BlockItem> blockItems)
        {
            try
            {
                var storedItems = new List<StoredBlockItem>();
                
                foreach (var item in blockItems)
                {
                    storedItems.Add(new StoredBlockItem
                    {
                        BlockName = item.BlockName,
                        AttributeTag = item.AttributeTag,
                        OriginalAttributeValue = item.OriginalAttributeValue,
                        AttributeValue = item.AttributeValue,
                        Index = item.Index
                    });
                }

                var serializer = new DataContractJsonSerializer(typeof(List<StoredBlockItem>));
                
                using (var stream = new FileStream(_storagePath, FileMode.Create))
                {
                    serializer.WriteObject(stream, storedItems);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                // 记录错误但不影响程序运行
                System.Diagnostics.Debug.WriteLine($"保存数据时出错: {ex.Message}");
                return false;
            }
        }

        // 加载存储的数据项（不包含ExampleBlockId）
        public List<StoredBlockItem> LoadStoredBlockItems()
        {
            try
            {
                if (!File.Exists(_storagePath))
                {
                    return new List<StoredBlockItem>(); // 文件不存在，返回空列表
                }

                var serializer = new DataContractJsonSerializer(typeof(List<StoredBlockItem>));
                
                using (var stream = new FileStream(_storagePath, FileMode.Open))
                {
                    var storedItems = serializer.ReadObject(stream) as List<StoredBlockItem>;
                    return storedItems ?? new List<StoredBlockItem>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载数据时出错: {ex.Message}");
                return new List<StoredBlockItem>();
            }
        }

        // 清空保存的数据
        public bool ClearSavedData()
        {
            try
            {
                if (File.Exists(_storagePath))
                {
                    File.Delete(_storagePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清空保存数据时出错: {ex.Message}");
                return false;
            }
        }
    }
}