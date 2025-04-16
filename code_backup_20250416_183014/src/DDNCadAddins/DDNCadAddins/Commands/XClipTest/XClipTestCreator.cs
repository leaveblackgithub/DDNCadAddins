using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Models;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands.XClipTest
{
    /// <summary>
    /// XClip测试对象创建类 - 负责创建所有测试对象
    /// </summary>
    public partial class XClipTestCreator
    {
        private readonly IAcadService _acadService;
        private readonly IXClipBlockService _xclipBlockService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口</param>
        /// <param name="xclipBlockService">XClip块服务接口</param>
        public XClipTestCreator(IAcadService acadService, IXClipBlockService xclipBlockService)
        {
            _acadService = acadService ?? throw new ArgumentNullException(nameof(acadService));
            _xclipBlockService = xclipBlockService ?? throw new ArgumentNullException(nameof(xclipBlockService));
        }
        
        /// <summary>
        /// 创建测试所需的图层
        /// </summary>
        /// <returns>包含创建的图层名称</returns>
        public TestLayers CreateTestLayers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            
            var layers = new TestLayers
            {
                Layer1 = "DDNTest_Layer1",
                Layer2 = "DDNTest_Layer2",
                RedLayer = "DDNTest_Red",
                BlueLayer = "DDNTest_Blue",
                GreenLayer = "DDNTest_Green",
                YellowLayer = "DDNTest_Yellow"
            };
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 获取图层表
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                
                // 创建图层表记录
                CreateLayerIfNotExists(tr, lt, layers.Layer1, 7); // 白色
                CreateLayerIfNotExists(tr, lt, layers.Layer2, 7); // 白色
                CreateLayerIfNotExists(tr, lt, layers.RedLayer, 1); // 红色
                CreateLayerIfNotExists(tr, lt, layers.BlueLayer, 5); // 蓝色
                CreateLayerIfNotExists(tr, lt, layers.GreenLayer, 3); // 绿色
                CreateLayerIfNotExists(tr, lt, layers.YellowLayer, 2); // 黄色
                
                tr.Commit();
            }
            
            return layers;
        }
        
        /// <summary>
        /// 如果图层不存在则创建
        /// </summary>
        public void CreateLayerIfNotExists(Transaction tr, LayerTable lt, string layerName, int colorIndex)
        {
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = layerName;
                ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)colorIndex);
                
                lt.UpgradeOpen();
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }
        
        /// <summary>
        /// 创建简单的测试块
        /// </summary>
        public ObjectId CreateSimpleBlock(Database db, string blockName, string layerName)
        {
            ObjectId blockId = ObjectId.Null;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 创建一个简单块
                blockId = CreateTestBlock(tr, db, blockName, layerName);
                
                tr.Commit();
            }
            
            return blockId;
        }
        
        /// <summary>
        /// 创建简单的测试块（在事务内部）
        /// </summary>
        public ObjectId CreateTestBlock(Transaction tr, Database db, string blockName, string layerName)
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
            
            // 添加一个圆和一条线到块定义
            Circle circle = new Circle();
            circle.Center = new Point3d(0, 0, 0);
            circle.Radius = 1.0;
            circle.Layer = layerName;
            
            Line line = new Line();
            line.StartPoint = new Point3d(-1.5, -1.5, 0);
            line.EndPoint = new Point3d(1.5, 1.5, 0);
            line.Layer = layerName;
            
            // 添加实体到块定义
            blockDef.AppendEntity(circle);
            blockDef.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(circle, true);
            tr.AddNewlyCreatedDBObject(line, true);
            
            // 创建块参照
            BlockReference blockRef = new BlockReference(new Point3d(5, 5, 0), blockDef.ObjectId);
            blockRef.Layer = layerName;
            
            // 添加到模型空间
            modelSpace.AppendEntity(blockRef);
            tr.AddNewlyCreatedDBObject(blockRef, true);
            
            return blockRef.ObjectId;
        }
        
        /// <summary>
        /// 创建嵌套块（在事务内部）
        /// </summary>
        public ObjectId CreateNestedBlock(Transaction tr, Database db, string blockName, ObjectId innerBlockId, string layerName)
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
            
            // 获取内部块引用
            BlockReference innerBlockRef = tr.GetObject(innerBlockId, OpenMode.ForRead) as BlockReference;
            
            // 在块定义中创建内部块的新引用
            BlockReference nestedBlockRef = new BlockReference(new Point3d(1, 1, 0), innerBlockRef.BlockTableRecord);
            nestedBlockRef.Layer = layerName;
            
            // 添加嵌套块引用到块定义
            blockDef.AppendEntity(nestedBlockRef);
            tr.AddNewlyCreatedDBObject(nestedBlockRef, true);
            
            // 创建外部块参照
            BlockReference outerBlockRef = new BlockReference(new Point3d(10, 10, 0), blockDef.ObjectId);
            outerBlockRef.Layer = layerName;
            
            // 添加到模型空间
            modelSpace.AppendEntity(outerBlockRef);
            tr.AddNewlyCreatedDBObject(outerBlockRef, true);
            
            return outerBlockRef.ObjectId;
        }
    }
} 