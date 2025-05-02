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
        private const string EntityIdKey = "TestEntityId";

        /// <summary>
        /// 测试实体信息字典，键为实体标识符，值为实体属性信息
        /// </summary>
        public static readonly Dictionary<string, Dictionary<string, object>> TestEntityInfoDict = new Dictionary<string, Dictionary<string, object>>
        {
            {
                "LINE_1_BYBLOCK", new Dictionary<string, object>
                {
                    {"Type", "Line"},
                    {"StartPoint", new Point3d(0, 0, 0)},
                    {"EndPoint", new Point3d(10, 0, 0)},
                    {"Layer", CadServiceManager.Layer0},
                    {"ColorIndex", CadServiceManager.ColorIndexByBlock},
                    {"Linetype", CadServiceManager.StrByBlock},
                    {"LinetypeScale", 1.0},
                    {"LineWeight", LineWeight.ByBlock}
                }
            },
            {
                "LINE_2_RED", new Dictionary<string, object>
                {
                    {"Type", "Line"},
                    {"StartPoint", new Point3d(0, 10, 0)},
                    {"EndPoint", new Point3d(10, 10, 0)},
                    {"Layer", NameTestLayer},
                    {"ColorIndex", CadServiceManager.ColorIndexRed},
                    {"Linetype", NameTestLinetype},
                    {"LinetypeScale", 2.0},
                    {"LineWeight", LineWeight.LineWeight050}
                }
            },
            {
                "CIRCLE_1_BYLAYER", new Dictionary<string, object>
                {
                    {"Type", "Circle"},
                    {"Center", new Point3d(20, 0, 0)},
                    {"Radius", 5.0},
                    {"Layer", CadServiceManager.Layer0},
                    {"ColorIndex", CadServiceManager.ColorIndexByLayer},
                    {"Linetype", CadServiceManager.StrByBlock},
                    {"LinetypeScale", 1.0},
                    {"LineWeight", LineWeight.ByBlock}
                }
            },
            {
                "CIRCLE_2_BYBLOCK", new Dictionary<string, object>
                {
                    {"Type", "Circle"},
                    {"Center", new Point3d(20, 10, 0)},
                    {"Radius", 5.0},
                    {"Layer", NameTestLayer},
                    {"ColorIndex", CadServiceManager.ColorIndexByBlock},
                    {"Linetype", NameTestLinetype},
                    {"LinetypeScale", 0.5},
                    {"LineWeight", LineWeight.LineWeight030}
                }
            },
            {
                "TEXT_1_GREEN", new Dictionary<string, object>
                {
                    {"Type", "DBText"},
                    {"Position", new Point3d(30, 0, 0)},
                    {"TextString", "Text1"},
                    {"Height", 2.5},
                    {"Layer", CadServiceManager.Layer0},
                    {"ColorIndex", CadServiceManager.ColorIndexGreen},
                    {"Linetype", CadServiceManager.StrByBlock},
                    {"LinetypeScale", 1.0},
                    {"LineWeight", LineWeight.ByBlock}
                }
            },
            {
                "TEXT_2_BYBLOCK", new Dictionary<string, object>
                {
                    {"Type", "DBText"},
                    {"Position", new Point3d(30, 10, 0)},
                    {"TextString", "Text2"},
                    {"Height", 2.5},
                    {"Layer", NameTestLayer},
                    {"ColorIndex", CadServiceManager.ColorIndexByBlock},
                    {"Linetype", NameTestLinetype},
                    {"LinetypeScale", 1.5},
                    {"LineWeight", LineWeight.LineWeight070}
                }
            },
            {
                "ATTRIBUTE_1_GREEN", new Dictionary<string, object>
                {
                    {"Type", "AttributeDefinition"},
                    {"Position", new Point3d(40, 0, 0)},
                    {"TextString", "属性值1"},
                    {"Tag", "ATTR1"},
                    {"Prompt", "默认值1"},
                    {"Height", 2.5},
                    {"Layer", CadServiceManager.Layer0},
                    {"ColorIndex", CadServiceManager.ColorIndexGreen},
                    {"Linetype", CadServiceManager.StrByBlock},
                    {"LinetypeScale", 1.0},
                    {"LineWeight", LineWeight.ByBlock}
                }
            },
            {
                "ATTRIBUTE_2_BYBLOCK", new Dictionary<string, object>
                {
                    {"Type", "AttributeDefinition"},
                    {"Position", new Point3d(40, 10, 0)},
                    {"TextString", "属性值2"},
                    {"Tag", "ATTR2"},
                    {"Prompt", "默认值2"},
                    {"Height", 5.0},
                    {"Layer", NameTestLayer},
                    {"ColorIndex", CadServiceManager.ColorIndexByBlock},
                    {"Linetype", NameTestLinetype},
                    {"LinetypeScale", 1.0},
                    {"LineWeight", LineWeight.ByBlock}
                }
            }
        };

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
            
            // 使用信息字典创建所有测试实体
            foreach (var entityEntry in TestEntityInfoDict)
            {
                string entityId = entityEntry.Key;
                var properties = entityEntry.Value;
                
                // 获取实体类型
                if (!properties.TryGetValue("Type", out object typeObj))
                    continue;
                    
                string typeName = typeObj.ToString();
                
                // 创建实体
                var entity = transactionService.CreateEntityByTypeAndProperties(typeName, properties);
                if (entity != null)
                {
                    entities.Add(entity);
                    // 添加自定义标识
                    transactionService.AddCustomIdentity(entity, EntityIdKey, entityId);
                }
            }
            
            return entities;
        }

    
}
} 
