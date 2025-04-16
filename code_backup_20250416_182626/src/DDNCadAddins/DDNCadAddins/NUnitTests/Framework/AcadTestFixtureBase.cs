using System;
using NUnit.Framework;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Services;
using DDNCadAddins.Infrastructure;

namespace DDNCadAddins.NUnitTests.Framework
{
    /// <summary>
    /// AutoCAD测试夹具基类 - 为一组相关测试提供共同的设置和清理
    /// </summary>
    [TestFixture]
    public abstract class AcadTestFixtureBase : AcadNUnitTestBase
    {
        /// <summary>
        /// XClip图块服务
        /// </summary>
        protected IXClipBlockService XClipBlockService { get; private set; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        protected AcadTestFixtureBase()
            : base()
        {
            XClipBlockService = new XClipBlockService(AcadService, Logger);
        }
        
        /// <summary>
        /// 在测试类执行前设置测试环境
        /// </summary>
        [OneTimeSetUp]
        public virtual void FixtureSetup()
        {
            // 初始化日志
            string fixtureName = GetType().Name;
            Logger.Initialize($"TestFixture_{fixtureName}");
            Logger.Log($"准备测试夹具: {fixtureName}");
            
            // 创建测试环境
            SetupTestEnvironment();
        }
        
        /// <summary>
        /// 在测试类执行后清理测试环境
        /// </summary>
        [OneTimeTearDown]
        public virtual void FixtureTearDown()
        {
            // 清理测试环境
            CleanupTestEnvironment();
            
            Logger.Log("测试夹具清理完成");
            Logger.Close();
        }
        
        /// <summary>
        /// 设置测试环境 - 子类可以重写此方法添加额外的设置
        /// </summary>
        protected virtual void SetupTestEnvironment()
        {
            // 基本实现不做任何事情
        }
        
        /// <summary>
        /// 清理测试环境 - 子类可以重写此方法添加额外的清理
        /// </summary>
        protected virtual void CleanupTestEnvironment()
        {
            // 基本实现不做任何事情
        }
        
        /// <summary>
        /// 创建临时图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <param name="color">图层颜色</param>
        /// <returns>图层ID</returns>
        protected ObjectId CreateTestLayer(string layerName, int colorIndex = 1)
        {
            Database db = GetDatabase();
            if (db == null) 
                throw new InvalidOperationException("没有活动的AutoCAD文档");
            
            ObjectId layerId = ObjectId.Null;
            
            WithTransaction(tr => {
                // 获取图层表
                LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                
                // 检查图层是否已存在
                if (lt.Has(layerName))
                {
                    layerId = lt[layerName];
                    return;
                }
                
                // 创建新图层
                LayerTableRecord layer = new LayerTableRecord();
                layer.Name = layerName;
                layer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)colorIndex);
                
                // 添加到图层表
                lt.UpgradeOpen();
                layerId = lt.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
            });
            
            return layerId;
        }
        
        /// <summary>
        /// 确保测试块不存在
        /// </summary>
        /// <param name="blockName">块名称</param>
        protected void EnsureTestBlockDeleted(string blockName)
        {
            Database db = GetDatabase();
            if (db == null) return;
            
            WithTransaction(tr => {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                
                if (bt.Has(blockName))
                {
                    // 删除块定义
                    bt.UpgradeOpen();
                    BlockTableRecord btr = tr.GetObject(bt[blockName], OpenMode.ForWrite) as BlockTableRecord;
                    
                    // 如果不是布局块，可以删除
                    if (!btr.IsLayout)
                    {
                        // 先清除所有实体
                        foreach (ObjectId entId in btr)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForWrite) as Entity;
                            if (ent != null)
                            {
                                ent.Erase();
                            }
                        }
                        
                        // 删除块定义
                        btr.Erase();
                    }
                }
            });
        }
    }
} 