using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using ServiceACAD;

namespace AddinsAcad.ServiceTests
{
    /// <summary>
    ///     块服务测试工具类
    /// </summary>
    public static class BlockServiceTestUtils
    {
        private const string NameTestLayer = "TestLayer";
        private const string NameTestLinetype = "TestLinetype";
        private const string EntityIdKey = "TestEntityId";

        /// <summary>
        ///     测试实体信息字典，键为实体标识符，值为实体属性信息
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, object>> TestEntityInfoDict =
            new Dictionary<string, Dictionary<string, object>>
            {
                {
                    "LINE_1_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrLine },
                        { CadServiceManager.StrStartPoint, new Point3d(0, 0, 0) },
                        { CadServiceManager.StrEndPoint, new Point3d(10, 0, 0) },
                        { CadServiceManager.StrLayer, CadServiceManager.Layer0 },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexByBlock },
                        { CadServiceManager.StrLinetype, CadServiceManager.StrByBlock },
                        { CadServiceManager.StrLinetypeScale, 1.0 },
                        { CadServiceManager.StrLineWeight, LineWeight.ByBlock }
                    }
                },
                {
                    "LINE_2_RED", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrLine },
                        { CadServiceManager.StrStartPoint, new Point3d(0, 10, 0) },
                        { CadServiceManager.StrEndPoint, new Point3d(10, 10, 0) },
                        { CadServiceManager.StrLayer, NameTestLayer },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexRed },
                        { CadServiceManager.StrLinetype, NameTestLinetype },
                        { CadServiceManager.StrLinetypeScale, 2.0 },
                        { CadServiceManager.StrLineWeight, LineWeight.LineWeight050 }
                    }
                },
                {
                    "CIRCLE_1_BYLAYER", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrCircle },
                        { CadServiceManager.StrCenter, new Point3d(20, 0, 0) },
                        { CadServiceManager.StrNormal, new Vector3d(0, 0, 1) },
                        { CadServiceManager.StrRadius, 5.0 },
                        { CadServiceManager.StrLayer, CadServiceManager.Layer0 },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexByLayer },
                        { CadServiceManager.StrLinetype, CadServiceManager.StrByBlock },
                        { CadServiceManager.StrLinetypeScale, 1.0 },
                        { CadServiceManager.StrLineWeight, LineWeight.ByBlock }
                    }
                },
                {
                    "CIRCLE_2_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrCircle },
                        { CadServiceManager.StrCenter, new Point3d(20, 10, 0) },
                        { CadServiceManager.StrNormal, new Vector3d(0, 0, 1) },
                        { CadServiceManager.StrRadius, 5.0 },
                        { CadServiceManager.StrLayer, NameTestLayer },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexByBlock },
                        { CadServiceManager.StrLinetype, NameTestLinetype },
                        { CadServiceManager.StrLinetypeScale, 0.5 },
                        { CadServiceManager.StrLineWeight, LineWeight.LineWeight030 }
                    }
                },
                {
                    "TEXT_1_GREEN", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrDbText },
                        { CadServiceManager.StrPosition, new Point3d(30, 0, 0) },
                        { CadServiceManager.StrTextString, "Text1" },
                        { CadServiceManager.StrHeight, 2.5 },
                        { CadServiceManager.StrLayer, CadServiceManager.Layer0 },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexGreen },
                        { CadServiceManager.StrLinetype, CadServiceManager.StrByBlock },
                        { CadServiceManager.StrLinetypeScale, 1.0 },
                        { CadServiceManager.StrLineWeight, LineWeight.ByBlock }
                    }
                },
                {
                    "TEXT_2_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrDbText },
                        { CadServiceManager.StrPosition, new Point3d(30, 10, 0) },
                        { CadServiceManager.StrTextString, "Text2" },
                        { CadServiceManager.StrHeight, 2.5 },
                        { CadServiceManager.StrLayer, NameTestLayer },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexByBlock },
                        { CadServiceManager.StrLinetype, NameTestLinetype },
                        { CadServiceManager.StrLinetypeScale, 1.5 },
                        { CadServiceManager.StrLineWeight, LineWeight.LineWeight070 }
                    }
                },
                {
                    "ATTRIBUTE_1_GREEN", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrAttributeDefinition },
                        { CadServiceManager.StrPosition, new Point3d(40, 0, 0) },
                        { CadServiceManager.StrTextString, "属性值1" },
                        { CadServiceManager.StrTag, "ATTR1" },
                        { CadServiceManager.StrPrompt, "默认值1" },
                        { CadServiceManager.StrHeight, 2.5 },
                        { CadServiceManager.StrLayer, CadServiceManager.Layer0 },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexGreen },
                        { CadServiceManager.StrLinetype, CadServiceManager.StrByBlock },
                        { CadServiceManager.StrLinetypeScale, 1.0 },
                        { CadServiceManager.StrLineWeight, LineWeight.ByBlock },
                        { CadServiceManager.StrTextStyleId, ObjectId.Null }
                    }
                },
                {
                    "ATTRIBUTE_2_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.StrTypeName, CadServiceManager.StrAttributeDefinition },
                        { CadServiceManager.StrPosition, new Point3d(40, 10, 0) },
                        { CadServiceManager.StrTextString, "属性值2" },
                        { CadServiceManager.StrTag, "ATTR2" },
                        { CadServiceManager.StrPrompt, "默认值2" },
                        { CadServiceManager.StrHeight, 5.0 },
                        { CadServiceManager.StrLayer, NameTestLayer },
                        { CadServiceManager.StrColorIndex, CadServiceManager.ColorIndexByBlock },
                        { CadServiceManager.StrLinetype, NameTestLinetype },
                        { CadServiceManager.StrLinetypeScale, 1.0 },
                        { CadServiceManager.StrLineWeight, LineWeight.ByBlock },
                        { CadServiceManager.StrTextStyleId, ObjectId.Null }
                    }
                }
            };

        /// <summary>
        ///     创建用于测试爆炸命令的测试块
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
                var blkDefId = serviceTrans.Block.CreateBlockDef(entities, "TestBlockForExplode");
                var blkRefId = serviceTrans.Block.CreateBlockRefInCurrentSpace(blkDefId, Point3d.Origin,
                    serviceTrans.Style.GetValidLayerName(NameTestLayer),
                    serviceTrans.Style.GetValidColorIndex(CadServiceManager.ColorIndexMagenta),
                    serviceTrans.Style.GetValidLineTypeName(NameTestLinetype));
                return blkRefId;
            }
            catch (Exception ex)
            {
                Logger._.Error($"\n警告: 创建测试块时发生异常: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        ///     创建测试实体
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        /// <param name="entities">实体列表</param>
        private static List<Entity> CreateTestEntities(ITransactionService transactionService)
        {
            var entities = new List<Entity>();

            // 使用信息字典创建所有测试实体
            foreach (var entityEntry in TestEntityInfoDict)
            {
                var entityKey = entityEntry.Key;
                var properties = entityEntry.Value;

                // 获取实体类型
                if (!properties.TryGetValue("TypeName", out var typeNameObj))
                {
                    Logger._.Error($"\n无法获取对象{entityKey}类型");
                    continue;
                }

                var typeName = (string)typeNameObj;

                // 创建实体
                var entity = transactionService.Entity.CreateEntityByTypeAndProperties(typeName, properties);
                if (entity != null)
                {
                    entities.Add(entity);
                    // 添加自定义标识
                    transactionService.Entity.AddCustomIdentity(entity, EntityIdKey, entityKey);
                }
            }

            return entities;
        }
    }
}
