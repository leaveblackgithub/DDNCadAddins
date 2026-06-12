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
        public const string NameTestLayer = "TestLayer";
        public const string NameTestLinetype = "TestLinetype";
        public const string EntityIdKey = "TestEntityId";
        public const string TestBlockName = "TestBlockForExplode";
        public const string StrValue1 = "属性值1";
        public const string StrValue2 = "属性值2";

        /// <summary>
        ///     测试实体信息字典，键为实体标识符，值为实体属性信息
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, object>> TestEntityInfoDict =
            new Dictionary<string, Dictionary<string, object>>
            {
                {
                    "LINE_1_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.Line },
                        { CadServiceManager.PropNames.StartPoint, new Point3d(0, 0, 0) },
                        { CadServiceManager.PropNames.EndPoint, new Point3d(10, 0, 0) },
                        { CadServiceManager.PropNames.Layer, CadServiceManager.Layers.Default },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.ByBlock },
                        { CadServiceManager.PropNames.Linetype, CadServiceManager.Linetypes.ByBlock },
                        { CadServiceManager.PropNames.LinetypeScale, 1.0 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.ByBlock }
                    }
                },
                {
                    "LINE_2_RED", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.Line },
                        { CadServiceManager.PropNames.StartPoint, new Point3d(0, 10, 0) },
                        { CadServiceManager.PropNames.EndPoint, new Point3d(10, 10, 0) },
                        { CadServiceManager.PropNames.Layer, NameTestLayer },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.Red },
                        { CadServiceManager.PropNames.Linetype, NameTestLinetype },
                        { CadServiceManager.PropNames.LinetypeScale, 2.0 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.LineWeight050 }
                    }
                },
                {
                    "CIRCLE_1_BYLAYER", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.Circle },
                        { CadServiceManager.PropNames.Center, new Point3d(20, 0, 0) },
                        { CadServiceManager.PropNames.Normal, new Vector3d(0, 0, 1) },
                        { CadServiceManager.PropNames.Radius, 5.0 },
                        { CadServiceManager.PropNames.Layer, CadServiceManager.Layers.Default },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.ByLayer },
                        { CadServiceManager.PropNames.Linetype, CadServiceManager.Linetypes.ByBlock },
                        { CadServiceManager.PropNames.LinetypeScale, 1.0 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.ByBlock }
                    }
                },
                {
                    "CIRCLE_2_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.Circle },
                        { CadServiceManager.PropNames.Center, new Point3d(20, 10, 0) },
                        { CadServiceManager.PropNames.Normal, new Vector3d(0, 0, 1) },
                        { CadServiceManager.PropNames.Radius, 5.0 },
                        { CadServiceManager.PropNames.Layer, NameTestLayer },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.ByBlock },
                        { CadServiceManager.PropNames.Linetype, NameTestLinetype },
                        { CadServiceManager.PropNames.LinetypeScale, 0.5 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.LineWeight030 }
                    }
                },
                {
                    "TEXT_1_GREEN", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.DbText },
                        { CadServiceManager.PropNames.Position, new Point3d(30, 0, 0) },
                        { CadServiceManager.PropNames.TextString, "Text1" },
                        { CadServiceManager.PropNames.Height, 2.5 },
                        { CadServiceManager.PropNames.Layer, CadServiceManager.Layers.Default },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.Green },
                        { CadServiceManager.PropNames.Linetype, CadServiceManager.Linetypes.ByBlock },
                        { CadServiceManager.PropNames.LinetypeScale, 1.0 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.ByBlock }
                    }
                },
                {
                    "TEXT_2_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.DbText },
                        { CadServiceManager.PropNames.Position, new Point3d(30, 10, 0) },
                        { CadServiceManager.PropNames.TextString, "Text2" },
                        { CadServiceManager.PropNames.Height, 2.5 },
                        { CadServiceManager.PropNames.Layer, NameTestLayer },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.ByBlock },
                        { CadServiceManager.PropNames.Linetype, NameTestLinetype },
                        { CadServiceManager.PropNames.LinetypeScale, 1.5 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.LineWeight070 }
                    }
                },
                {
                    "ATTRIBUTE_1_GREEN", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.AttributeDefinition },
                        { CadServiceManager.PropNames.Position, new Point3d(40, 0, 0) },
                        { CadServiceManager.PropNames.TextString, StrValue1 },
                        { CadServiceManager.PropNames.Tag, "ATTR1" },
                        { CadServiceManager.PropNames.Prompt, "默认值1" },
                        { CadServiceManager.PropNames.Height, 2.5 },
                        { CadServiceManager.PropNames.Layer, CadServiceManager.Layers.Default },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.Green },
                        { CadServiceManager.PropNames.Linetype, CadServiceManager.Linetypes.ByBlock },
                        { CadServiceManager.PropNames.LinetypeScale, 1.0 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.ByBlock },
                        { CadServiceManager.PropNames.TextStyleId, ObjectId.Null }
                    }
                },
                {
                    "ATTRIBUTE_2_BYBLOCK", new Dictionary<string, object>
                    {
                        { CadServiceManager.PropNames.TypeName, CadServiceManager.EntityTypes.AttributeDefinition },
                        { CadServiceManager.PropNames.Position, new Point3d(40, 10, 0) },
                        { CadServiceManager.PropNames.TextString, StrValue2 },
                        { CadServiceManager.PropNames.Tag, "ATTR2" },
                        { CadServiceManager.PropNames.Prompt, "默认值2" },
                        { CadServiceManager.PropNames.Height, 5.0 },
                        { CadServiceManager.PropNames.Layer, NameTestLayer },
                        { CadServiceManager.PropNames.ColorIndex, CadServiceManager.Colors.ByBlock },
                        { CadServiceManager.PropNames.Linetype, NameTestLinetype },
                        { CadServiceManager.PropNames.LinetypeScale, 1.0 },
                        { CadServiceManager.PropNames.LineWeight, LineWeight.ByBlock },
                        { CadServiceManager.PropNames.TextStyleId, ObjectId.Null }
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
                var blkDefId = serviceTrans.Block.CreateBlockDef(entities, TestBlockName);
                var blkRefId = serviceTrans.Block.CreateBlockRefInCurrentSpace(blkDefId, Point3d.Origin,
                    NameTestLayer,
                    CadServiceManager.Colors.Magenta,
                    NameTestLinetype);
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
