using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace ServiceACAD
{
    /// <summary>
    ///     事务服务接口，提供与事务相关的操作
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        ///     事务对象
        /// </summary>
        // Transaction CadTrans { get; }

        /// <summary>
        ///     块服务缓存字典
        /// </summary>
        IDictionary<ObjectId, IBlockService> BlockServiceDict { get; }
        
        
        

        /// <summary>
        ///     为实体添加自定义标识
        /// </summary>
        /// <param name="entity">要添加标识的实体</param>
        /// <param name="identityKey">标识键名</param>
        /// <param name="identityValue">标识值</param>
        /// <returns>是否添加成功</returns>
        bool AddCustomIdentity(Entity entity, string identityKey, string identityValue);

        /// <summary>
        ///     获取实体的自定义标识
        /// </summary>
        /// <param name="entity">要获取标识的实体</param>
        /// <param name="identityKey">标识键名</param>
        /// <returns>标识值，如不存在则返回null</returns>
        string GetCustomIdentity(Entity entity, string identityKey);

        /// <summary>
        ///     根据类型名称和属性字典创建实体
        /// </summary>
        /// <param name="typeName">实体类型名称</param>
        /// <param name="properties">属性字典</param>
        /// <returns>创建的实体对象，如果创建失败则返回null</returns>
        Entity CreateEntityByTypeAndProperties(string typeName, Dictionary<string, object> properties);

        /// <summary>
        ///     获取数据库对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="objectId">对象ID</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>数据库对象</returns>
        T GetObject<T>(ObjectId objectId, OpenMode openMode = OpenMode.ForRead) where T : DBObject;

        /// <summary>
        ///     获取块服务
        /// </summary>
        /// <param name="objectId">块引用ID</param>
        /// <returns>块服务实例</returns>
        IBlockService GetBlockService(ObjectId objectId);

        /// <summary>
        ///     向模型空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToModelSpace(Entity entity);

        /// <summary>
        ///     向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param
        /// <summary>
        ///     向块表记录添加实体
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToBlockTableRecord(BlockTableRecord blockTableRecord, Entity entity);

        /// <summary>
        ///     获取模型空间
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>模型空间块表记录</returns>
        BlockTableRecord GetModelSpace(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取块表记录ID
        /// </summary>
        /// <param name="name">块名称</param>
        /// <returns>块表记录ID</returns>
        ObjectId GetBlockTableRecordId(string name);

        /// <summary>
        ///     获取块表记录
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块表记录</returns>
        BlockTableRecord GetBlockTableRecord(string name, OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取块表记录的子对象
        /// </summary>
        /// <param name="blockTableRecord">块表记录</param>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        List<ObjectId> GetChildObjects<T>(BlockTableRecord blockTableRecord, Func<T, bool> filter = null)
            where T : DBObject;

        /// <summary>
        ///     从模型空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        List<ObjectId> GetChildObjectsFromModelspace<T>(Func<T, bool> filter = null) where T : DBObject;

        /// <summary>
        ///     从当前空间获取子对象
        /// </summary>
        /// <param name="filter">过滤器</param>
        /// <returns>子对象ID集合</returns>
        List<ObjectId> GetChildObjectsFromCurrentSpace<T>(Func<T, bool> filter = null) where T : DBObject;

        void IsolateObjectsOfModelSpace(ICollection<ObjectId> objectIdsToIsolate);

        /// <summary>
        ///     获取当前空间（模型空间或纸空间）
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>当前空间块表记录</returns>
        BlockTableRecord GetCurrentSpace(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     向当前空间添加实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <returns>添加的实体ID</returns>
        ObjectId AppendEntityToCurrentSpace(Entity entity);

        List<ObjectId> AppendEntitiesToCurrentSpace(List<Entity> entities);

        List<ObjectId> AppendEntitiesToBlockTableRecord(BlockTableRecord blockTableRecord,
            ICollection<Entity> entities);

        List<ObjectId> FilterObjects<T>(ICollection<ObjectId> objectIds, Func<T, bool> filter = null)
            where T : DBObject;

        /// <summary>
        ///     创建块定义
        /// </summary>
        /// <param name="entities">要包含在块中的实体集合</param>
        /// <param name="blockName">块名称</param>
        /// <returns>创建的块的ObjectId</returns>
        ObjectId CreateBlockDef(ICollection<Entity> entities, string blockName = "");

        LayerTable GetLayerTable(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     创建新线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>创建的线型对象，如果创建失败则返回null</returns>
        LinetypeTableRecord CreateNewLineType(string lineTypeName);

        /// <summary>
        ///     获取图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>图层对象，如果不存在则返回null</returns>
        LayerTableRecord GetLayer(string layerName, OpenMode openMode = OpenMode.ForRead);

        LinetypeTable GetLineTypeTable(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取块表
        /// </summary>
        /// <returns>块表</returns>
        BlockTable GetBlockTable(OpenMode openMode = OpenMode.ForRead);

        string GetValidLayerName(string layerName);
        string GetValidLineTypeName(string linetypeName);
        short GetValidColorIndex(short colorIndex, short defaultColorIndex = CadServiceManager.ColorIndexWhite);
        Color GetValidColor(short colorIndex,short defaultColorIndex=CadServiceManager.ColorIndexWhite);

        /// <summary>
        ///     获取当前图层名称
        /// </summary>
        /// <returns>当前图层名称</returns>
        string GetCurrentLayerName();

        /// <summary>
        ///     创建新图层
        /// </summary>
        /// <param name="layerName">图层名称</param>
        /// <returns>创建的图层对象，如果创建失败则返回null</returns>
        LayerTableRecord CreateNewLayer(string layerName = "",
            short colorIndex = CadServiceManager.ColorIndexWhite,
            string lineType = CadServiceManager.LineTypeContinuous);

        /// <summary>
        ///     在当前空间创建块参照
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="insertPt">插入点</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="color">颜色</param>
        /// <param name="linetype">线型</param>
        /// <returns>创建成功的块参照ObjectId，失败返回ObjectId.Null</returns>
        ObjectId CreateBlockRefInCurrentSpace(ObjectId blkDefId, Point3d insertPt = default(Point3d),
            string layerName = "", short colorIndex = 256, string linetype = "BYLAYER");

        /// <summary>
        ///     获取线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>线型对象，如果不存在则返回null</returns>
        LinetypeTableRecord GetLineType(string lineTypeName, OpenMode openMode = OpenMode.ForRead);

        void AddNewlyCreatedDBObject(DBObject obj, bool add);
        

        /// <summary>
        /// 为块参照添加多个属性并赋值
        /// </summary>
        /// <param name="transactionService">事务服务</param>
        /// <param name="blockReference">块参照对象</param>
        /// <param name="attributeValues">属性Tag和对应的值字典</param>
        /// <returns>是否成功添加属性</returns>
        bool AddAttributesToBlockReference(ObjectId blkDefId,
            ObjectId blkRefId,
            Dictionary<string, Dictionary<string, object>> attributeValues);
    }
}
