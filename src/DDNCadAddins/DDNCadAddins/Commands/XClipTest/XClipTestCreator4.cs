using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace DDNCadAddins.Commands.XClipTest
{
    /// <summary>
    /// XClip测试创建类的第四部分，包含更复杂的块创建方法
    /// </summary>
    public partial class XClipTestCreator
    {
        /// <summary>
        /// 创建带有BYLAYER颜色设置的实体的块
        /// </summary>
        public ObjectId CreateBlockWithLayerColor(Transaction tr, Database db, string blockName, string layerName)
        {
            // 获取块表
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            
            // 创建新的块表记录
            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = blockName;
            
            // 打开块表进行写入
            bt.UpgradeOpen();
            
            // 添加块表记录
            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            
            // 创建线段实体（使用BYLAYER颜色）
            Line line = new Line(new Point3d(0, 0, 0), new Point3d(10, 10, 0));
            line.LayerId = GetLayerId(tr, db, layerName);
            line.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
            
            // 创建圆形实体（同样使用BYLAYER颜色）
            Circle circle = new Circle(new Point3d(5, 5, 0), Vector3d.ZAxis, 3);
            circle.LayerId = GetLayerId(tr, db, layerName);
            circle.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
            
            // 将实体添加到块中
            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
            
            return btrId;
        }
        
        /// <summary>
        /// 获取图层ID
        /// </summary>
        public ObjectId GetLayerId(Transaction tr, Database db, string layerName)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt.Has(layerName))
            {
                return lt[layerName];
            }
            return lt["0"]; // 如果找不到指定图层，返回0图层
        }
        
        /// <summary>
        /// 获取线型ID
        /// </summary>
        public ObjectId GetLineTypeId(Transaction tr, Database db, string lineTypeName)
        {
            LinetypeTable ltt = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
            if (ltt.Has(lineTypeName))
            {
                return ltt[lineTypeName];
            }
            return ltt["Continuous"]; // 如果找不到指定线型，返回连续线型
        }
        
        /// <summary>
        /// 创建带有BYBLOCK颜色设置的实体的块
        /// </summary>
        public ObjectId CreateBlockWithByBlockColor(Transaction tr, Database db, string blockName)
        {
            // 获取块表
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            
            // 创建新的块表记录
            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = blockName;
            
            // 打开块表进行写入
            bt.UpgradeOpen();
            
            // 添加块表记录
            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            
            // 创建线段实体（使用BYBLOCK颜色）
            Line line = new Line(new Point3d(0, 0, 0), new Point3d(10, 10, 0));
            line.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByBlock, 0);
            
            // 创建圆形实体，使用BYBLOCK颜色
            Circle circle = new Circle(new Point3d(5, 5, 0), Vector3d.ZAxis, 3);
            circle.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByBlock, 0);
            
            // 将实体添加到块中
            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
            
            return btrId;
        }
        
        /// <summary>
        /// 创建指定颜色的块参照
        /// </summary>
        public ObjectId CreateColoredBlockReference(Transaction tr, Database db, ObjectId blockDefId, 
            string blockName, string layerName, Autodesk.AutoCAD.Colors.Color color)
        {
            // 获取模型空间
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
            
            // 创建块参照
            BlockReference blockRef = new BlockReference(new Point3d(0, 0, 0), blockDefId);
            blockRef.LayerId = GetLayerId(tr, db, layerName);
            blockRef.Color = color; // 设置特定颜色
            
            // 将块参照添加到模型空间
            ms.AppendEntity(blockRef);
            tr.AddNewlyCreatedDBObject(blockRef, true);
            
            return blockRef.ObjectId;
        }
        
        /// <summary>
        /// 创建包含多个图层实体的块
        /// </summary>
        public ObjectId CreateBlockWithMultiLayerEntities(Transaction tr, Database db, 
            string blockName, string layer1, string layer2)
        {
            // 获取块表
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            
            // 创建新的块表记录
            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = blockName;
            
            // 打开块表进行写入
            bt.UpgradeOpen();
            
            // 添加块表记录
            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            
            // 创建Layer1上的线段
            Line line1 = new Line(new Point3d(0, 0, 0), new Point3d(10, 10, 0));
            line1.LayerId = GetLayerId(tr, db, layer1);
            
            // 创建Layer2上的圆
            Circle circle = new Circle(new Point3d(5, 5, 0), Vector3d.ZAxis, 3);
            circle.LayerId = GetLayerId(tr, db, layer2);
            
            // 将实体添加到块中
            btr.AppendEntity(line1);
            tr.AddNewlyCreatedDBObject(line1, true);
            
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);
            
            return btrId;
        }
        
        /// <summary>
        /// 创建使用特定线型的块
        /// </summary>
        public ObjectId CreateBlockWithLineType(Transaction tr, Database db, 
            string blockName, string lineTypeName, double lineTypeScale)
        {
            // 获取块表
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            
            // 创建新的块表记录
            BlockTableRecord btr = new BlockTableRecord();
            btr.Name = blockName;
            
            // 打开块表进行写入
            bt.UpgradeOpen();
            
            // 添加块表记录
            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            
            // 获取线型ID
            ObjectId lineTypeId = GetLineTypeId(tr, db, lineTypeName);
            
            // 创建使用特定线型的线段
            Line line = new Line(new Point3d(0, 0, 0), new Point3d(10, 10, 0));
            line.LinetypeId = lineTypeId;
            line.LinetypeScale = lineTypeScale;
            
            // 将实体添加到块中
            btr.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
            
            return btrId;
        }
        
        /// <summary>
        /// 创建复合块，包含多个内部块
        /// </summary>
        public ObjectId CreateCompositeBlock(Transaction tr, Database db, string blockName, ObjectId[] innerBlockIds)
        {
            try
            {
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                
                // 创建新的块表记录
                BlockTableRecord btr = new BlockTableRecord();
                btr.Name = blockName;
                
                // 打开块表进行写入
                bt.UpgradeOpen();
                
                // 添加块表记录
                ObjectId btrId = bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);
                
                // 创建一个间隔，每个块之间的间隔
                double spacing = 15.0;
                double xPos = 0;
                
                // 依次添加每个块参照
                foreach (ObjectId innerBlockId in innerBlockIds)
                {
                    if (innerBlockId == ObjectId.Null || !innerBlockId.IsValid)
                        continue;
                        
                    // 创建块参照
                    BlockReference blockRef = new BlockReference(new Point3d(xPos, 0, 0), innerBlockId);
                    
                    // 将块参照添加到块定义中
                    btr.AppendEntity(blockRef);
                    tr.AddNewlyCreatedDBObject(blockRef, true);
                    
                    // 更新下一个块的位置
                    xPos += spacing;
                }
                
                // 获取模型空间
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                // 创建块参照
                BlockReference mainBlockRef = new BlockReference(new Point3d(0, 0, 0), btrId);
                
                // 将块参照添加到模型空间
                ms.AppendEntity(mainBlockRef);
                tr.AddNewlyCreatedDBObject(mainBlockRef, true);
                
                return mainBlockRef.ObjectId;
            }
            catch (System.Exception)
            {
                return ObjectId.Null;
            }
        }
    }
} 