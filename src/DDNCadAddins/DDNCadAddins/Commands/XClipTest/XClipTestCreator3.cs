using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Models;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands.XClipTest
{
    /// <summary>
    /// XClip测试创建类的扩展部分，包含块搜索和清理方法
    /// </summary>
    public partial class XClipTestCreator
    {
        /// <summary>
        /// 查找嵌套块参照
        /// </summary>
        public ObjectId FindNestedBlockReference(Database db, string blockName)
        {
            ObjectId blockRefId = ObjectId.Null;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                
                // 遍历所有块定义
                foreach (ObjectId btrId in bt)
                {
                    BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                    
                    // 跳过模型空间和图纸空间
                    if (btr.IsLayout)
                        continue;
                    
                    // 遍历块定义中的所有实体
                    foreach (ObjectId entId in btr)
                    {
                        Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                        if (ent is BlockReference)
                        {
                            BlockReference blockRef = ent as BlockReference;
                            BlockTableRecord nestedBtr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                            
                            // 检查块名称是否匹配
                            if (nestedBtr.Name == blockName)
                            {
                                blockRefId = blockRef.ObjectId;
                                break;
                            }
                        }
                    }
                    
                    if (blockRefId != ObjectId.Null)
                        break;
                }
                
                tr.Commit();
            }
            
            return blockRefId;
        }
        
        /// <summary>
        /// 查找深层嵌套的块参照
        /// </summary>
        public ObjectId FindDeepNestedBlockReference(Database db, string blockName)
        {
            ObjectId foundId = ObjectId.Null;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 在深层嵌套中递归查找
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                
                // 遍历所有顶层实体
                foreach (ObjectId id in ms)
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForRead);
                    if (obj is BlockReference)
                    {
                        // 递归查找嵌套块
                        ObjectId matchId = SearchNestedBlock(tr, obj as BlockReference, blockName, 0, 5);
                        if (matchId != ObjectId.Null)
                        {
                            foundId = matchId;
                            break;
                        }
                    }
                }
                
                tr.Commit();
            }
            
            return foundId;
        }
        
        /// <summary>
        /// 递归搜索嵌套块
        /// </summary>
        private ObjectId SearchNestedBlock(Transaction tr, BlockReference blockRef, string targetName, int currentLevel, int maxLevel)
        {
            // 防止递归过深
            if (currentLevel >= maxLevel)
                return ObjectId.Null;
                
            BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            
            // 检查当前块名
            if (blockDef.Name == targetName)
                return blockRef.ObjectId;
                
            // 递归检查当前块中的所有嵌套块
            foreach (ObjectId entId in blockDef)
            {
                Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent is BlockReference)
                {
                    BlockReference nestedRef = ent as BlockReference;
                    ObjectId matchId = SearchNestedBlock(tr, nestedRef, targetName, currentLevel + 1, maxLevel);
                    if (matchId != ObjectId.Null)
                        return matchId;
                }
            }
            
            return ObjectId.Null;
        }
        
        /// <summary>
        /// 创建测试环境并返回所有测试数据
        /// </summary>
        public TestData CreateTestEnvironment()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            var testData = new TestData();
            
            try
            {
                // 创建测试图层
                var layers = CreateTestLayers();
                testData.Layers = layers;
                
                // 1. 创建顶层XClipped图块
                ObjectId topLevelBlockId = CreateSimpleBlock(db, "DDNTest_TopLevel_Auto", layers.RedLayer);
                testData.AddBlock("顶层块", topLevelBlockId);
                
                // 2. 创建各种测试场景的块
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 加载必要的线型
                    LoadLineType(tr, db, "DASHED");
                    
                    // 先确保图层2未冻结
                    SetLayerFrozen(tr, db, layers.Layer2, false);
                    
                    // 2.1 BYLAYER颜色测试块
                    ObjectId byLayerBlockId = CreateBlockWithLayerColor(tr, db, "DDNTest_ByLayer_Auto", layers.RedLayer);
                    ObjectId byLayerRefId = CreateBlockReference(tr, db, byLayerBlockId, new Point3d(15, 15, 0), layers.RedLayer);
                    ObjectId byLayerNestedId = CreateNestedBlock(tr, db, "DDNTest_ByLayer_Nested_Auto", byLayerRefId, layers.BlueLayer);
                    testData.AddBlock("BYLAYER颜色测试", byLayerRefId);
                    
                    // 2.2 BYBLOCK颜色测试块
                    ObjectId byBlockDefId = CreateBlockWithByBlockColor(tr, db, "DDNTest_ByBlock_Auto");
                    ObjectId byBlockGreenId = CreateColoredBlockReference(tr, db, byBlockDefId, "DDNTest_ByBlock_Green_Auto", 
                        layers.GreenLayer, Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3));
                    ObjectId byBlockNestedId = CreateNestedBlock(tr, db, "DDNTest_ByBlock_Nested_Auto", byBlockGreenId, layers.YellowLayer);
                    testData.AddBlock("BYBLOCK颜色测试", byBlockGreenId);
                    
                    // 2.3 多图层测试块
                    ObjectId multiLayerBlockId = CreateBlockWithMultiLayerEntities(tr, db, "DDNTest_MultiLayer_Auto", 
                        layers.Layer1, layers.Layer2);
                    ObjectId multiLayerRefId = CreateBlockReference(tr, db, multiLayerBlockId, new Point3d(25, 25, 0), layers.Layer1);
                    ObjectId multiLayerNestedId = CreateNestedBlock(tr, db, "DDNTest_MultiLayer_Nested_Auto", multiLayerRefId, layers.RedLayer);
                    testData.AddBlock("多图层测试", multiLayerRefId);
                    
                    // 2.4 线型测试块
                    ObjectId lineTypeBlockId = CreateBlockWithLineType(tr, db, "DDNTest_Linetype_Auto", "DASHED", 0.5);
                    ObjectId lineTypeRefId = CreateBlockReference(tr, db, lineTypeBlockId, new Point3d(35, 35, 0), layers.BlueLayer);
                    ObjectId lineTypeNestedId = CreateNestedBlock(tr, db, "DDNTest_Linetype_Nested_Auto", lineTypeRefId, layers.BlueLayer);
                    testData.AddBlock("线型测试", lineTypeRefId);
                    
                    // 2.5 多级嵌套测试
                    ObjectId blockDId = CreateTestBlock(tr, db, "DDNTest_D_Auto", layers.GreenLayer);
                    ObjectId blockCId = CreateNestedBlock(tr, db, "DDNTest_C_Auto", blockDId, layers.BlueLayer);
                    ObjectId blockBId = CreateNestedBlock(tr, db, "DDNTest_B_Auto", blockCId, layers.YellowLayer);
                    ObjectId blockAId = CreateNestedBlock(tr, db, "DDNTest_A_Auto", blockBId, layers.RedLayer);
                    testData.AddBlock("深度嵌套测试", blockDId);
                    
                    tr.Commit();
                }
                
                // 冻结图层2
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    SetLayerFrozen(tr, db, layers.Layer2, true);
                    tr.Commit();
                }
                
                // 3. 对所有块进行XClip
                // 先处理顶层块
                var xclipTopLevelResult = _xclipBlockService.AutoXClipBlock(db, topLevelBlockId);
                if (!xclipTopLevelResult.Success)
                    ed.WriteMessage($"\n警告: 顶层块XClip操作失败: {xclipTopLevelResult.ErrorMessage}");
                
                // 找到并处理所有嵌套块
                var nestedBlockIds = new Dictionary<string, ObjectId>();
                
                // 添加空值检查
                ObjectId byLayerAutoId = FindNestedBlockReference(db, "DDNTest_ByLayer_Auto");
                if (byLayerAutoId != ObjectId.Null)
                    nestedBlockIds.Add("BYLAYER测试块", byLayerAutoId);
                
                ObjectId byBlockGreenAutoId = FindNestedBlockReference(db, "DDNTest_ByBlock_Green_Auto");
                if (byBlockGreenAutoId != ObjectId.Null)
                    nestedBlockIds.Add("BYBLOCK测试块", byBlockGreenAutoId);
                
                ObjectId multiLayerAutoId = FindNestedBlockReference(db, "DDNTest_MultiLayer_Auto");
                if (multiLayerAutoId != ObjectId.Null)
                    nestedBlockIds.Add("多图层测试块", multiLayerAutoId);
                
                ObjectId lineTypeAutoId = FindNestedBlockReference(db, "DDNTest_Linetype_Auto");
                if (lineTypeAutoId != ObjectId.Null)
                    nestedBlockIds.Add("线型测试块", lineTypeAutoId);
                
                ObjectId deepNestedId = FindDeepNestedBlockReference(db, "DDNTest_D_Auto");
                if (deepNestedId != ObjectId.Null)
                    nestedBlockIds.Add("深度嵌套块", deepNestedId);
                
                ed.WriteMessage($"\n找到 {nestedBlockIds.Count} 个嵌套块用于XClip测试");
                
                foreach (var pair in nestedBlockIds)
                {
                    if (pair.Value != ObjectId.Null)
                    {
                        var result = _xclipBlockService.AutoXClipBlock(db, pair.Value);
                        if (result.Success)
                        {
                            testData.AddBlock(pair.Key, pair.Value);
                            ed.WriteMessage($"\n成功对{pair.Key}执行XClip");
                        }
                        else
                        {
                            ed.WriteMessage($"\n警告: {pair.Key}的XClip操作失败: {result.ErrorMessage}");
                        }
                    }
                    else
                    {
                        ed.WriteMessage($"\n警告: 未找到{pair.Key}");
                    }
                }
                
                return testData;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n创建测试环境时出错: {ex.Message}");
                return testData; // 返回可能部分创建的测试数据
            }
        }
        
        /// <summary>
        /// 清理测试数据
        /// </summary>
        public void CleanupTests()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            ed.WriteMessage("\n===== 开始清理测试数据 =====");
            
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 获取块表
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    
                    // 获取模型空间
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    
                    // 清理所有测试块
                    List<ObjectId> blocksToErase = new List<ObjectId>();
                    foreach (ObjectId entId in modelSpace)
                    {
                        Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                        if (ent is BlockReference)
                        {
                            BlockReference blockRef = ent as BlockReference;
                            BlockTableRecord btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                            
                            if (btr.Name.StartsWith("DDNTest_"))
                            {
                                blocksToErase.Add(entId);
                            }
                        }
                    }
                    
                    // 删除找到的块
                    foreach (ObjectId id in blocksToErase)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        ent.Erase();
                    }
                    
                    // 清理所有测试块定义
                    List<ObjectId> blockDefsToErase = new List<ObjectId>();
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                        if (btr.Name.StartsWith("DDNTest_") && !btr.IsLayout)
                        {
                            blockDefsToErase.Add(btrId);
                        }
                    }
                    
                    // 从块表中删除块定义(如果没有引用)
                    foreach (ObjectId id in blockDefsToErase)
                    {
                        BlockTableRecord btr = tr.GetObject(id, OpenMode.ForWrite) as BlockTableRecord;
                        try
                        {
                            btr.Erase();
                        }
                        catch
                        {
                            // 如果块定义仍有引用，则忽略错误
                        }
                    }
                    
                    // 删除测试图层
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    
                    List<ObjectId> layersToErase = new List<ObjectId>();
                    foreach (ObjectId ltrId in lt)
                    {
                        LayerTableRecord ltr = tr.GetObject(ltrId, OpenMode.ForRead) as LayerTableRecord;
                        if (ltr.Name.StartsWith("DDNTest_"))
                        {
                            layersToErase.Add(ltrId);
                        }
                    }
                    
                    // 尝试删除测试图层
                    int erasedLayers = 0;
                    foreach (ObjectId id in layersToErase)
                    {
                        LayerTableRecord ltr = tr.GetObject(id, OpenMode.ForWrite) as LayerTableRecord;
                        try
                        {
                            ltr.Erase();
                            erasedLayers++;
                        }
                        catch
                        {
                            // 如果图层仍有引用或是当前图层，则无法删除
                        }
                    }
                    
                    tr.Commit();
                    
                    ed.WriteMessage($"\n已清理 {blocksToErase.Count} 个测试块, {blockDefsToErase.Count} 个块定义, {erasedLayers} 个测试图层");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n清理测试数据时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建块引用
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="db">数据库</param>
        /// <param name="blockDefId">块定义ID</param>
        /// <param name="position">插入点</param>
        /// <param name="layerName">图层名称</param>
        /// <returns>创建的块引用ID</returns>
        public ObjectId CreateBlockReference(Transaction tr, Database db, ObjectId blockDefId, Point3d position, string layerName)
        {
            try
            {
                // 获取模型空间
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                // 创建块引用
                BlockReference blockRef = new BlockReference(position, blockDefId);
                
                // 设置图层
                blockRef.LayerId = GetLayerId(tr, db, layerName);
                
                // 添加到模型空间
                ms.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                return blockRef.ObjectId;
            }
            catch (System.Exception)
            {
                return ObjectId.Null;
            }
        }
    }
} 