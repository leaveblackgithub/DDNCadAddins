using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using ServiceACAD;
using System;
namespace AddinsAcad.ServiceTests
{
    /// <summary>
    /// 块服务测试工具类
    /// </summary>
    public static class BlockServiceTestUtils
    {
        private const string NameTestLayer = "TestLayer";
        private const string NameTestLinetype = "TestLinetype";

        /// <summary>
        /// 创建用于测试爆炸命令的测试块
        /// </summary>
        /// <param name="serviceTrans">事务服务</param>
        /// <returns>创建的测试块的ObjectId</returns>
        public static ObjectId CreateTestBlockForExplodeCommand(ITransactionService serviceTrans)
        {
            try
            {
                // 创建测试实体
                var entities = CreateTestEntities(serviceTrans);

                // 使用事务服务创建块
                var blkDefId = serviceTrans.CreateBlockDef(entities, "TestBlockForExplode");
                var blkRefId = serviceTrans.CreateBlockRefInCurrentSpace(blkDefId, Point3d.Origin,
                    serviceTrans.GetValidLayerName(NameTestLayer),
                    serviceTrans.GetValidColorIndex(CadServiceManager.ColorIndexMagenta),
                    serviceTrans.GetValidLineTypeName(NameTestLinetype));
                return blkRefId;
            }
            catch (Exception ex)
            {
                Logger._.Error($"\n警告: 创建测试块时发生异常: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        /// 创建测试实体
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        /// <param name="entities">实体列表</param>
        private static List<Entity> CreateTestEntities(ITransactionService transactionService)
        {
            var entities = new List<Entity>();
            // 1. 直线1：0图层，BYBLOCK颜色，BYBLOCK线型，默认线型比例，BYBLOCK线宽
            var line1 = new Line(new Point3d(0, 0, 0), new Point3d(10, 0, 0));
            line1.Layer = CadServiceManager.Layer0;
            line1.ColorIndex = CadServiceManager.ColorIndexByBlock; // BYBLOCK
            line1.Linetype = CadServiceManager.StrByBlock;
            line1.LinetypeScale = 1.0;
            line1.LineWeight = LineWeight.ByBlock;
            entities.Add(line1);

            // 2. 直线2：非0图层，特定颜色，特定线型，自定义线型比例，特定线宽
            var line2 = new Line(new Point3d(0, 10, 0), new Point3d(10, 10, 0));

            line2.Layer = transactionService.GetValidLayerName(NameTestLayer);
            line2.ColorIndex = CadServiceManager.ColorIndexRed; // 红色
            line2.Linetype = transactionService.GetValidLineTypeName(NameTestLinetype);
            line2.LinetypeScale = 2.0;
            line2.LineWeight = LineWeight.LineWeight050;
            entities.Add(line2);

            // 3. 圆1：0图层，BYLAYER颜色，BYBLOCK线型，默认线型比例，BYBLOCK线宽
            var circle1 = new Circle(new Point3d(20, 0, 0), Vector3d.ZAxis, 5);
            circle1.Layer = CadServiceManager.Layer0;
            circle1.ColorIndex = CadServiceManager.ColorIndexByLayer; // BYLAYER
            circle1.Linetype = CadServiceManager.StrByBlock;
            circle1.LinetypeScale = 1.0;
            circle1.LineWeight = LineWeight.ByBlock;
            entities.Add(circle1);

            // 4. 圆2：非0图层，BYBLOCK颜色，特定线型，自定义线型比例，特定线宽
            var circle2 = new Circle(new Point3d(20, 10, 0), Vector3d.ZAxis, 5);
            circle2.Layer = transactionService.GetValidLayerName(NameTestLayer);
            circle2.ColorIndex = CadServiceManager.ColorIndexByBlock; // BYBLOCK
            circle2.Linetype = transactionService.GetValidLineTypeName(NameTestLinetype);
            circle2.LinetypeScale = 0.5;
            circle2.LineWeight = LineWeight.LineWeight030;
            entities.Add(circle2);

            // 5. 文本1：0图层，特定颜色，BYBLOCK线型，默认线型比例，BYBLOCK线宽
            var text1 = new DBText();
            text1.Position = new Point3d(30, 0, 0);
            text1.TextString = "Text1";
            text1.Height = 2.5;
            text1.Layer = CadServiceManager.Layer0;
            text1.ColorIndex = CadServiceManager.ColorIndexGreen; // 绿色
            text1.Linetype = CadServiceManager.StrByBlock;
            text1.LinetypeScale = 1.0;
            text1.LineWeight = LineWeight.ByBlock;
            entities.Add(text1);

            // 6. 文本2：非0图层，BYBLOCK颜色，特定线型，自定义线型比例，特定线宽
            var text2 = new DBText();
            text2.Position = new Point3d(30, 10, 0);
            text2.TextString = "Text2";
            text2.Height = 2.5;
            text2.Layer = transactionService.GetValidLayerName(NameTestLayer);
            text2.ColorIndex = CadServiceManager.ColorIndexByBlock; // BYBLOCK
            text2.Linetype = transactionService.GetValidLineTypeName(NameTestLinetype);
            text2.LinetypeScale = 1.5;
            text2.LineWeight = LineWeight.LineWeight070;
            entities.Add(text2);

            var tag1 = "ATTR1";
            var attr1 = new AttributeDefinition(new Point3d(40, 0, 0), "属性值1", tag1, "默认值1", ObjectId.Null)
            {
                Height=2.5,
                Layer=CadServiceManager.Layer0,
                ColorIndex=CadServiceManager.ColorIndexGreen,
                Linetype=CadServiceManager.StrByBlock
            };
            entities.Add(attr1);
            var tag2 = "ATTR2";
            var attr2 = new AttributeDefinition(new Point3d(40, 10, 0), "属性值2", tag2, "默认值2", ObjectId.Null)
            {
                Height=5,
                Layer=transactionService.GetValidLayerName(NameTestLayer),
                ColorIndex=CadServiceManager.ColorIndexByBlock,
                Linetype=transactionService.GetValidLineTypeName(NameTestLinetype)
            };
            entities.Add(attr2);
            return entities;
        }

    
}
} 
