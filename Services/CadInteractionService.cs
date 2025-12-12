using System;
using System.Collections.Generic;
using System.IO;
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using HelloGstarCAD.Models;
using HelloGstarCAD.Services;

namespace HelloGstarCAD.Services
{
    public class CadInteractionService
    {
        private Document Doc => Application.DocumentManager.MdiActiveDocument;
        private Database Db => Doc.Database;
        private Editor Ed => Doc.Editor;

        public List<BlockItem> SelectBlocks(string targetAttributeTag = "A")
        {
            var blockList = new List<BlockItem>();
            try
            {
                Ed.WriteMessage($"\n请选择图块（将按图块名称去重）...\n");
                
                TypedValue[] filterList = { new TypedValue((int)DxfCode.Start, "INSERT") };
                var filter = new SelectionFilter(filterList);
                var selResult = Ed.GetSelection(filter);

                if (selResult.Status == PromptStatus.OK)
                {
                    using (var tr = Db.TransactionManager.StartTransaction())
                    {
                        // 用于跟踪已处理的图块名称（按图块名称去重）
                        var processedBlockNames = new HashSet<string>();
                        
                        foreach (var selectedId in selResult.Value.GetObjectIds())
                        {
                            var blockRef = tr.GetObject(selectedId, OpenMode.ForRead) as BlockReference;
                            if (blockRef == null) continue;

                            // 获取块定义名称
                            var blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                            string blockName = blockDef.Name;
                            
                            // 关键：如果已处理过此图块名称，则跳过（实现去重）
                            if (processedBlockNames.Contains(blockName))
                            {
                                continue;
                            }
                            
                            processedBlockNames.Add(blockName);
                            
                            // 查找目标属性
                            string actualAttributeTag = targetAttributeTag;
                            string attributeValue = "未命名";
                            
                            // 查找指定属性
                            foreach (ObjectId attId in blockRef.AttributeCollection)
                            {
                                var attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                if (attRef != null && attRef.Tag.Equals(targetAttributeTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    attributeValue = attRef.TextString;
                                    actualAttributeTag = attRef.Tag;
                                    break;
                                }
                            }
                            
                            // 如果没有找到指定属性，使用第一个属性
                            if (attributeValue == "未命名" && blockRef.AttributeCollection.Count > 0)
                            {
                                var firstAttId = blockRef.AttributeCollection[0];
                                var firstAttRef = tr.GetObject(firstAttId, OpenMode.ForRead) as AttributeReference;
                                if (firstAttRef != null)
                                {
                                    attributeValue = firstAttRef.TextString;
                                    actualAttributeTag = firstAttRef.Tag;
                                }
                            }
                            
                            var blockItem = new BlockItem
                            {
                                BlockName = blockName,
                                AttributeTag = actualAttributeTag,
                                OriginalAttributeValue = attributeValue, // 保存原始值
                                AttributeValue = attributeValue, // 当前值初始化为原始值
                                ExampleBlockId = selectedId
                            };
                            
                            blockList.Add(blockItem);
                        }
                        tr.Commit();
                    }
                    
                    Ed.WriteMessage($"\n成功读取 {blockList.Count} 种不重复的图块类型。\n");
                    
                    // 显示添加的图块列表
                    if (blockList.Count > 0)
                    {
                        Ed.WriteMessage("已添加的图块类型:\n");
                        foreach (var block in blockList)
                        {
                            Ed.WriteMessage($"  • {block.BlockName} = {block.OriginalAttributeValue}\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("选择图块时出错", ex);
            }
            return blockList;
        }

        // 重新关联存储的图块项到CAD中的实际图块
        public List<BlockItem> ReloadAndAssociateBlocks(List<StoredBlockItem> storedItems)
        {
            var blockList = new List<BlockItem>();
            
            try
            {
                if (storedItems == null || storedItems.Count == 0)
                {
                    return blockList;
                }
                
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    // 获取所有块引用，按名称分组
                    var blockRefsByName = new Dictionary<string, ObjectId>();
                    
                    // 遍历模型空间中的所有块引用
                    var blockTable = tr.GetObject(Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    
                    foreach (ObjectId objId in modelSpace)
                    {
                        var blockRef = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                        if (blockRef != null)
                        {
                            var blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                            string blockName = blockDef.Name;
                            
                            // 为每种图块名称存储一个示例ObjectId
                            if (!blockRefsByName.ContainsKey(blockName))
                            {
                                blockRefsByName[blockName] = objId;
                            }
                        }
                    }
                    
                    // 遍历存储的图块项，尝试重新关联
                    foreach (var storedItem in storedItems)
                    {
                        if (blockRefsByName.TryGetValue(storedItem.BlockName, out ObjectId blockId))
                        {
                            // 创建一个新的BlockItem并关联到现有的图块
                            var blockItem = new BlockItem
                            {
                                BlockName = storedItem.BlockName,
                                AttributeTag = storedItem.AttributeTag,
                                OriginalAttributeValue = storedItem.OriginalAttributeValue,
                                AttributeValue = storedItem.AttributeValue,
                                Index = storedItem.Index,
                                ExampleBlockId = blockId
                            };
                            
                            blockList.Add(blockItem);
                        }
                        else
                        {
                            // 如果找不到对应的图块，创建一个没有ExampleBlockId的项
                            // 用户可能已删除该图块，但仍可在列表中看到
                            var blockItem = new BlockItem
                            {
                                BlockName = storedItem.BlockName,
                                AttributeTag = storedItem.AttributeTag,
                                OriginalAttributeValue = storedItem.OriginalAttributeValue,
                                AttributeValue = storedItem.AttributeValue,
                                Index = storedItem.Index,
                                ExampleBlockId = ObjectId.Null
                            };
                            
                            blockList.Add(blockItem);
                        }
                    }
                    
                    tr.Commit();
                }
                
                // 统计关联结果
                int associatedCount = 0;
                int missingCount = 0;
                foreach (var block in blockList)
                {
                    if (block.ExampleBlockId.IsValid)
                    {
                        associatedCount++;
                    }
                    else
                    {
                        missingCount++;
                    }
                }
                
                Ed.WriteMessage($"\n图块重新关联完成：成功关联 {associatedCount} 个，缺失 {missingCount} 个。\n");
                
                // 显示缺失的图块名称
                if (missingCount > 0)
                {
                    Ed.WriteMessage("以下图块在图纸中未找到：\n");
                    foreach (var block in blockList)
                    {
                        if (!block.ExampleBlockId.IsValid)
                        {
                            Ed.WriteMessage($"  • {block.BlockName}\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("重新关联图块时出错", ex);
            }
            
            return blockList;
        }

        // 注意：这个方法不再被调用，但保留在代码中
        public bool UpdateBlockAttributes(string blockName, string attributeTag, string newValue)
        {
            try
            {
                int updatedCount = 0;
                
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    // 获取块表
                    var blockTable = tr.GetObject(Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    
                    // 查找指定名称的图块定义
                    foreach (ObjectId blockDefId in blockTable)
                    {
                        var blockDef = tr.GetObject(blockDefId, OpenMode.ForRead) as BlockTableRecord;
                        if (blockDef.Name == blockName)
                        {
                            // 找到所有此图块的实例
                            var refIds = blockDef.GetBlockReferenceIds(true, false);
                            
                            foreach (ObjectId refId in refIds)
                            {
                                var blockRef = tr.GetObject(refId, OpenMode.ForWrite) as BlockReference;
                                if (blockRef != null)
                                {
                                    // 更新属性
                                    foreach (ObjectId attId in blockRef.AttributeCollection)
                                    {
                                        var attRef = tr.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                                        if (attRef != null && attRef.Tag.Equals(attributeTag, StringComparison.OrdinalIgnoreCase))
                                        {
                                            attRef.TextString = newValue;
                                            updatedCount++;
                                            break;
                                        }
                                    }
                                }
                            }
                            break; // 找到目标图块定义后退出
                        }
                    }
                    
                    tr.Commit();
                    
                    if (updatedCount > 0)
                    {
                        Ed.WriteMessage($"\n已更新图块 '{blockName}' 的 {updatedCount} 个实例，属性 {attributeTag} = {newValue}\n");
                        return true;
                    }
                    else
                    {
                        Ed.WriteMessage($"\n警告：未找到图块 '{blockName}' 或没有可更新的实例\n");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"更新图块 '{blockName}' 属性时出错", ex);
            }
            return false;
        }

        public ObjectId? SelectPolyline()
        {
            try
            {
                Ed.WriteMessage("\n请选择一条多段线作为编号路径...\n");
                
                var peo = new PromptEntityOptions("\n请选择一条多段线作为编号路径: ");
                peo.SetRejectMessage("\n请选择一条多段线。\n");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AddAllowedClass(typeof(Polyline2d), true);
                peo.AddAllowedClass(typeof(Polyline3d), true);

                var per = Ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    return per.ObjectId;
                }
            }
            catch (Exception ex)
            {
                LogError("选择多段线时出错", ex);
            }
            return null;
        }

        public void PlaceNumbersAlongPolyline(ObjectId polylineId, List<BlockItem> blocks, string prefix, string suffix, int startNumber)
        {
            using (var tr = Db.TransactionManager.StartTransaction())
            {
                var polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                if (polyline == null) 
                {
                    Ed.WriteMessage("\n错误：选择的对象不是有效的曲线。\n");
                    return;
                }

                var blockTable = tr.GetObject(Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                var modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                int currentNumber = startNumber;
                int createdCount = 0;
                int skippedCount = 0;
                
                foreach (var blockItem in blocks)
                {
                    // 检查ExampleBlockId是否有效
                    if (blockItem.ExampleBlockId.IsValid)
                    {
                        try
                        {
                            var blockRef = tr.GetObject(blockItem.ExampleBlockId, OpenMode.ForRead) as BlockReference;
                            if (blockRef != null)
                            {
                                Point3d blockPosition = blockRef.Position;
                                Point3d projectedPoint = polyline.GetClosestPointTo(blockPosition, false);

                                // 使用AttributeValue作为编号文本（现在作为标题）
                                string numberText = $"{prefix}{currentNumber}{blockItem.AttributeValue}{suffix}";

                                var dbText = new DBText
                                {
                                    TextString = numberText,
                                    Position = projectedPoint,
                                    Height = 2.5,
                                    Justify = AttachmentPoint.MiddleCenter
                                };

                                modelSpace.AppendEntity(dbText);
                                tr.AddNewlyCreatedDBObject(dbText, true);
                                currentNumber++;
                                createdCount++;
                                
                                Ed.WriteMessage($"\n已创建编号: {numberText} (图块: {blockItem.BlockName})");
                            }
                            else
                            {
                                Ed.WriteMessage($"\n警告: 图块 '{blockItem.BlockName}' 的引用无效，跳过编号。");
                                skippedCount++;
                            }
                        }
                        catch
                        {
                            Ed.WriteMessage($"\n警告: 无法访问图块 '{blockItem.BlockName}'，可能已被删除，跳过编号。");
                            skippedCount++;
                        }
                    }
                    else
                    {
                        Ed.WriteMessage($"\n警告: 图块 '{blockItem.BlockName}' 没有有效的关联，跳过编号。");
                        skippedCount++;
                    }
                }
                
                tr.Commit();
                
                Ed.WriteMessage($"\n\n沿线编号完成，成功创建 {createdCount} 个编号");
                if (skippedCount > 0)
                {
                    Ed.WriteMessage($"，跳过 {skippedCount} 个无法关联的图块");
                }
                Ed.WriteMessage("。\n");
            }
        }

        private void LogError(string message, Exception ex)
        {
            try
            {
                string logPath = @"C:\Temp\GstarCAD_Plugin_Log.txt";
                System.IO.File.AppendAllText(logPath, $"[ERROR] {DateTime.Now}: {message}\n{ex}\n\n");
                Ed.WriteMessage($"\n[错误] {message}。详情请查看日志: {logPath}\n");
            }
            catch
            {
                // 忽略日志记录错误
            }
        }
    }
}