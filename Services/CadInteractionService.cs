using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HelloGstarCAD.Models;

// 浩辰CAD (GstarCAD) .NET API 的核心命名空间
using GrxCAD.ApplicationServices;
using GrxCAD.DatabaseServices;
using GrxCAD.EditorInput;
using GrxCAD.Geometry;
using GrxCAD.Runtime;

namespace HelloGstarCAD.Services
{
    public class CadInteractionService
    {
        /// <summary>
        /// 从当前图纸获取所有图块定义（模型空间和布局中的块表记录）
        /// </summary>
        public List<BlockItem> GetBlocksFromDrawing()
        {
            List<BlockItem> blockList = new List<BlockItem>();

            try
            {
                // 获取当前文档和数据库
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    return blockList;
                }

                Database db = doc.Database;

                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 打开块表
                    BlockTable blockTable = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);

                    foreach (ObjectId blockRecordId in blockTable)
                    {
                        BlockTableRecord blockRecord = (BlockTableRecord)trans.GetObject(blockRecordId, OpenMode.ForRead);

                        // 排除 *Model_Space, *Paper_Space 等布局
                        if (!blockRecord.IsLayout && !blockRecord.Name.StartsWith("*"))
                        {
                            BlockItem item = new BlockItem
                            {
                                // 块表记录名即为图块名
                                Name = blockRecord.Name,
                                // 此处示例：使用块的原点或边界信息作为位置描述
                                // 实际可根据需要获取插入点、边界框等
                                Location = GetBlockLocationDescription(blockRecord)
                            };
                            blockList.Add(item);
                        }
                    }
                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                // 实际开发中应使用更完善的日志记录
                System.Diagnostics.Debug.WriteLine($"[浩辰CAD服务] 获取图块列表时出错: {ex.Message}");
            }

            return blockList;
        }

        /// <summary>
        /// 辅助方法：生成图块的位置描述信息
        /// </summary>
        private string GetBlockLocationDescription(BlockTableRecord btr)
        {
            // 示例：返回块的原点
            // 实际可能需计算边界框或获取其他特征点
            if (btr != null && btr.Bounds.HasValue)
            {
                var bounds = btr.Bounds.Value;
                var minPoint = bounds.MinPoint;
                return $"({minPoint.X:F1}, {minPoint.Y:F1}, {minPoint.Z:F1})";
            }
            return "(原点)";
        }

        /// <summary>
        /// 从当前编辑器选择集获取图块参照（INSERT）实例
        /// </summary>
        public List<BlockItem> GetSelectedBlocks()
        {
            List<BlockItem> selectedBlocks = new List<BlockItem>();

            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return selectedBlocks;

                Editor editor = doc.Editor;

                // 构建一个选择过滤器，仅选择图块参照（INSERT）
                TypedValue[] filterValues = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                };
                SelectionFilter filter = new SelectionFilter(filterValues);

                // 提示用户在CAD界面中选择
                PromptSelectionResult selectionResult = editor.GetSelection(filter);
                if (selectionResult.Status == PromptStatus.OK)
                {
                    using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject selectedObj in selectionResult.Value)
                        {
                            if (selectedObj != null)
                            {
                                // 获取被选中的图块参照
                                BlockReference blockRef = trans.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as BlockReference;
                                if (blockRef != null)
                                {
                                    BlockItem item = new BlockItem
                                    {
                                        // BlockReference 的 Name 即为其对应的块表记录名
                                        Name = blockRef.Name,
                                        Location = $"({blockRef.Position.X:F1}, {blockRef.Position.Y:F1}, {blockRef.Position.Z:F1})"
                                    };
                                    selectedBlocks.Add(item);
                                }
                            }
                        }
                        trans.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[浩辰CAD服务] 获取选择集时出错: {ex.Message}");
            }

            return selectedBlocks;
        }

        /// <summary>
        /// 将当前选择集中的图块添加到提供的集合中
        /// </summary>
        public void AddSelectedBlocksToModel(ObservableCollection<BlockItem> currentCollection)
        {
            if (currentCollection == null) return;

            var selected = GetSelectedBlocks();
            foreach (var block in selected)
            {
                // 简单的重复判断：名称和位置均相同视为同一图块实例
                if (!currentCollection.Any(b => b.Name == block.Name && b.Location == block.Location))
                {
                    currentCollection.Add(block);
                }
            }
        }
    }
}