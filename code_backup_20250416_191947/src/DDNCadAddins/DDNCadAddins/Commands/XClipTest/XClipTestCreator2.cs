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
    /// XClip测试创建类的扩展部分，包含更多特殊块创建方法
    /// </summary>
    public partial class XClipTestCreator
    {
        /// <summary>
        /// 创建带特殊属性的测试块
        /// </summary>
        public ObjectId CreateSpecialAttributeBlock(Database db, string blockName, TestLayers layers)
        {
            ObjectId blockId = ObjectId.Null;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                
                // 获取模型空间
                BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                // 创建块定义
                BlockTableRecord blockDef = new BlockTableRecord();
                blockDef.Name = blockName;
                
                // 添加到块表
                bt.UpgradeOpen();
                bt.Add(blockDef);
                tr.AddNewlyCreatedDBObject(blockDef, true);
                
                // 创建BYLAYER颜色的圆
                Circle bylayerCircle = new Circle();
                bylayerCircle.Center = new Point3d(0, 0, 0);
                bylayerCircle.Radius = 1.0;
                bylayerCircle.Layer = layers.RedLayer;
                bylayerCircle.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
                
                // 创建BYBLOCK颜色的线
                Line byblockLine = new Line();
                byblockLine.StartPoint = new Point3d(-1.5, -1.5, 0);
                byblockLine.EndPoint = new Point3d(1.5, 1.5, 0);
                byblockLine.Layer = layers.Layer1;
                byblockLine.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByBlock, 256);
                
                // 创建虚线
                Line dashedLine = new Line();
                dashedLine.StartPoint = new Point3d(-1.5, 1.5, 0);
                dashedLine.EndPoint = new Point3d(1.5, -1.5, 0);
                dashedLine.Layer = layers.Layer1;
                dashedLine.Linetype = "DASHED";
                dashedLine.LinetypeScale = 0.5;
                
                // 创建在冻结图层上的文本
                DBText frozenText = new DBText();
                frozenText.Position = new Point3d(0, 0, 0);
                frozenText.TextString = "冻结图层文本";
                frozenText.Height = 0.2;
                frozenText.Layer = layers.Layer2;
                
                // 添加实体到块定义
                blockDef.AppendEntity(bylayerCircle);
                blockDef.AppendEntity(byblockLine);
                blockDef.AppendEntity(dashedLine);
                blockDef.AppendEntity(frozenText);
                tr.AddNewlyCreatedDBObject(bylayerCircle, true);
                tr.AddNewlyCreatedDBObject(byblockLine, true);
                tr.AddNewlyCreatedDBObject(dashedLine, true);
                tr.AddNewlyCreatedDBObject(frozenText, true);
                
                // 创建块参照 - 设置特定颜色（会影响BYBLOCK实体）
                BlockReference blockRef = new BlockReference(new Point3d(15, 15, 0), blockDef.ObjectId);
                blockRef.Layer = layers.BlueLayer;
                blockRef.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByColor, 3); // 绿色
                
                // 添加到模型空间
                modelSpace.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                blockId = blockRef.ObjectId;
                tr.Commit();
            }
            
            return blockId;
        }
        
        /// <summary>
        /// 创建变换后的块
        /// </summary>
        public ObjectId CreateTransformedBlock(Database db, string blockName, string layerName)
        {
            ObjectId blockId = ObjectId.Null;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 获取块表
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                
                // 获取模型空间
                BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                
                // 创建块定义
                BlockTableRecord blockDef = new BlockTableRecord();
                blockDef.Name = blockName;
                
                // 添加到块表
                bt.UpgradeOpen();
                bt.Add(blockDef);
                tr.AddNewlyCreatedDBObject(blockDef, true);
                
                // 添加一个矩形和一个文本到块定义
                Polyline rectangle = new Polyline();
                rectangle.AddVertexAt(0, new Point2d(-1, -1), 0, 0, 0);
                rectangle.AddVertexAt(1, new Point2d(1, -1), 0, 0, 0);
                rectangle.AddVertexAt(2, new Point2d(1, 1), 0, 0, 0);
                rectangle.AddVertexAt(3, new Point2d(-1, 1), 0, 0, 0);
                rectangle.Closed = true;
                rectangle.Layer = layerName;
                
                DBText text = new DBText();
                text.Position = new Point3d(0, 0, 0);
                text.TextString = "旋转缩放测试";
                text.Height = 0.2;
                text.Layer = layerName;
                
                // 添加实体到块定义
                blockDef.AppendEntity(rectangle);
                blockDef.AppendEntity(text);
                tr.AddNewlyCreatedDBObject(rectangle, true);
                tr.AddNewlyCreatedDBObject(text, true);
                
                // 创建块参照
                BlockReference blockRef = new BlockReference(new Point3d(20, 20, 0), blockDef.ObjectId);
                blockRef.Layer = layerName;
                
                // 应用旋转和非均匀缩放
                Matrix3d rotationMatrix = Matrix3d.Rotation(Math.PI / 4, Vector3d.ZAxis, new Point3d(20, 20, 0)); // 45度旋转
                Matrix3d scaleMatrix = Matrix3d.Scaling(2.5, Point3d.Origin);
                
                blockRef.TransformBy(scaleMatrix);
                blockRef.TransformBy(rotationMatrix);
                
                // 添加到模型空间
                modelSpace.AppendEntity(blockRef);
                tr.AddNewlyCreatedDBObject(blockRef, true);
                
                blockId = blockRef.ObjectId;
                tr.Commit();
            }
            
            return blockId;
        }
        
        /// <summary>
        /// 设置图层冻结状态
        /// </summary>
        public void SetLayerFrozen(Transaction tr, Database db, string layerName, bool frozen)
        {
            // 获取图层表
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            
            if (lt.Has(layerName))
            {
                // 获取图层表记录
                LayerTableRecord ltr = tr.GetObject(lt[layerName], OpenMode.ForWrite) as LayerTableRecord;
                
                // 设置冻结状态
                ltr.IsFrozen = frozen;
            }
        }
        
        /// <summary>
        /// 加载线型
        /// </summary>
        public void LoadLineType(Transaction tr, Database db, string lineTypeName)
        {
            // 获取线型表
            LinetypeTable ltt = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
            
            // 如果线型不存在，则加载它
            if (!ltt.Has(lineTypeName))
            {
                // 升级打开
                ltt.UpgradeOpen();
                
                // 加载线型
                db.LoadLineTypeFile(lineTypeName, "acad.lin");
            }
        }
    }
} 