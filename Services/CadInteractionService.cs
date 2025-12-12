using System;
using System.Collections.Generic;
using System.IO;
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using HelloGstarCAD.Models;

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
                                OriginalAttributeValue = attributeValue,
                                AttributeValue = attributeValue,
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

        public void PlaceNumbersAlongPolyline(ObjectId polylineId, List<BlockItem> blocks, string prefix, string suffix, int startNumber,
            double textHeight = 3.0, string layerName = "0", string textStyle = "Standard", 
            double offsetX = 0, double offsetY = 0, bool autoCreateLayer = true)
        {
            try
            {
                using (var tr = Db.TransactionManager.StartTransaction())
                {
                    var polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                    if (polyline == null) 
                    {
                        Ed.WriteMessage("\n❌ 错误：选择的对象不是有效的曲线。\n");
                        return;
                    }

                    // 处理图层
                    LayerTable layerTable = tr.GetObject(Db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    ObjectId layerId;
                    
                    if (layerTable.Has(layerName))
                    {
                        layerId = layerTable[layerName];
                    }
                    else if (autoCreateLayer)
                    {
                        // 创建新图层 - 修正颜色设置方式
                        var newLayer = new LayerTableRecord
                        {
                            Name = layerName
                            // 将图层颜色索引设置为 7（白色）
                        };
                        
                        // 浩辰CAD可能使用不同的颜色设置方式
                        // 尝试使用 SetColorIndex 方法或者直接设置颜色属性
                        try
                        {
                            // 方法1：尝试使用 SetColorIndex
                            //newLayer.SetColorIndex(7); // 白色
                        }
                        catch
                        {
                            try
                            {
                                // 方法2：尝试直接设置 Color 属性
                                // 浩辰CAD中可能需要使用 Color.FromColorIndex
                                // 如果这行报错，可以注释掉
                                // newLayer.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                            }
                            catch
                            {
                                // 如果都失败，跳过颜色设置
                            }
                        }
                        
                        layerTable.UpgradeOpen();
                        layerId = layerTable.Add(newLayer);
                        tr.AddNewlyCreatedDBObject(newLayer, true);
                        layerTable.DowngradeOpen();
                        
                        Ed.WriteMessage($"\n✅ 已创建新图层: {layerName}\n");
                    }
                    else
                    {
                        Ed.WriteMessage($"\n❌ 图层 '{layerName}' 不存在，请检查图层名称。\n");
                        return;
                    }

                    // 处理文字样式
                    TextStyleTable textStyleTable = tr.GetObject(Db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
                    ObjectId textStyleId;
                    
                    if (textStyleTable.Has(textStyle))
                    {
                        textStyleId = textStyleTable[textStyle];
                    }
                    else
                    {
                        // 使用默认样式
                        textStyleId = textStyleTable["Standard"];
                        Ed.WriteMessage($"\n⚠ 文字样式 '{textStyle}' 不存在，使用默认样式 'Standard'。\n");
                    }
                    
                    var blockTable = tr.GetObject(Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    int currentNumber = startNumber;
                    int createdCount = 0;
                    
                    Ed.WriteMessage($"\n开始沿线编号，共 {blocks.Count} 个图块...\n");
                    
                    foreach (var blockItem in blocks)
                    {
                        // 使用存储的示例实例ID
                        if (blockItem.ExampleBlockId.IsValid)
                        {
                            var blockRef = tr.GetObject(blockItem.ExampleBlockId, OpenMode.ForRead) as BlockReference;
                            if (blockRef != null)
                            {
                                Point3d blockPosition = blockRef.Position;
                                
                                // 调试信息：显示图块位置
                                Ed.WriteMessage($"\n图块 '{blockItem.BlockName}' 位置: X={blockPosition.X:F2}, Y={blockPosition.Y:F2}");
                                
                                try
                                {
                                    // 获取投影点（图块位置在多段线上的最近点）
                                    Point3d projectedPoint = polyline.GetClosestPointTo(blockPosition, false);
                                    
                                    // 应用偏移
                                    Point3d finalPosition = new Point3d(
                                        projectedPoint.X + offsetX,
                                        projectedPoint.Y + offsetY,
                                        projectedPoint.Z);
                                    
                                    // 调试信息
                                    Ed.WriteMessage($"  → 投影点: X={projectedPoint.X:F2}, Y={projectedPoint.Y:F2}");
                                    Ed.WriteMessage($"  → 最终位置: X={finalPosition.X:F2}, Y={finalPosition.Y:F2}");
                                    Ed.WriteMessage($"  (偏移: X={offsetX}, Y={offsetY})");
                                    
                                    // 使用AttributeValue作为编号文本（当前显示标题）
                                    string numberText = $"{prefix}{currentNumber}{blockItem.AttributeValue}{suffix}";
                                    
                                    // 创建DBText对象
                                    var dbText = new DBText
                                    {
                                        TextString = numberText,
                                        Position = finalPosition, // 使用最终位置
                                        Height = textHeight,
                                        Justify = AttachmentPoint.MiddleCenter,
                                        TextStyleId = textStyleId,
                                        LayerId = layerId
                                    };

                                    // 尝试设置文字颜色为红色（颜色索引1）
                                    try
                                    {
                                        // 浩辰CAD中设置颜色的方式可能不同
                                        // 尝试使用 SetColorIndex 方法
                                       // dbText.SetColorIndex(1); // 红色
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            // 或者使用 Color 属性
                                            // dbText.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        }
                                        catch
                                        {
                                            // 如果都失败，跳过颜色设置
                                        }
                                    }

                                    modelSpace.AppendEntity(dbText);
                                    tr.AddNewlyCreatedDBObject(dbText, true);
                                    createdCount++;
                                    currentNumber++;
                                    
                                    Ed.WriteMessage($"  ✅ 创建编号: {numberText} (高度: {textHeight})");
                                }
                                catch (Exception ex)
                                {
                                    Ed.WriteMessage($"  ❌ 投影点计算失败: {ex.Message}");
                                }
                            }
                            else
                            {
                                Ed.WriteMessage($"\n❌ 图块 '{blockItem.BlockName}' 的示例实例无效。\n");
                            }
                        }
                        else
                        {
                            Ed.WriteMessage($"\n❌ 图块 '{blockItem.BlockName}' 的ExampleBlockId无效。\n");
                        }
                    }
                    
                    tr.Commit();
                    
                    if (createdCount > 0)
                    {
                        Ed.WriteMessage($"\n\n✅ 沿线编号完成！共创建 {createdCount} 个编号文本。\n");
                        Ed.WriteMessage($"💡 编号设置：文字高度={textHeight}，图层={layerName}，样式={textStyle}\n");
                        Ed.WriteMessage($"💡 位置偏移：X={offsetX}，Y={offsetY}\n");
                        Ed.WriteMessage("💡 如果看不到编号，请使用ZOOM命令缩放视图到整个图形范围。\n");
                    }
                    else
                    {
                        Ed.WriteMessage($"\n❌ 沿线编号失败，未创建任何编号文本。\n");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("沿线编号时出错", ex);
                Ed.WriteMessage($"\n❌ 沿线编号过程中发生错误: {ex.Message}\n");
            }
        }

        private void LogError(string message, Exception ex)
        {
            try
            {
                string logPath = @"C:\Temp\GstarCAD_Plugin_Log.txt";
                string logContent = $"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                   $"消息: {message}\n" +
                                   $"异常: {ex.Message}\n" +
                                   $"堆栈: {ex.StackTrace}\n" +
                                   new string('=', 80) + "\n\n";
                
                System.IO.File.AppendAllText(logPath, logContent);
                Ed.WriteMessage($"\n⚠ [错误] {message}。详情请查看日志: {logPath}\n");
            }
            catch
            {
                Ed.WriteMessage($"\n⚠ [错误] {message}。详细错误: {ex.Message}\n");
            }
        }
    }
}