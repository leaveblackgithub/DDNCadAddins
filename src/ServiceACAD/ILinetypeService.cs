using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    ///     线型管理接口 - 线型的增删改查操作
    /// </summary>
    public interface ILinetypeService
    {
        /// <summary>
        ///     获取线型表
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>线型表</returns>
        LinetypeTable GetLineTypeTable(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>线型对象，如果不存在则返回null</returns>
        LinetypeTableRecord GetLineType(string lineTypeName, OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        ///     获取或创建线型，已存在则返回现有线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>线型对象，如果操作失败则返回null</returns>
        LinetypeTableRecord GetOrCreateLineType(string lineTypeName);

        /// <summary>
        ///     创建新线型
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>创建的线型对象，如果创建失败则返回null</returns>
        LinetypeTableRecord CreateLineType(string lineTypeName);

        /// <summary>
        ///     获取有效的线型ID
        /// </summary>
        /// <param name="lineTypeName">线型名称</param>
        /// <returns>有效的线型ObjectId，不存在则返回ObjectId.Null</returns>
        ObjectId GetValidLineTypeId(string lineTypeName);

        /// <summary>
        ///     获取有效的线型名称
        /// </summary>
        /// <param name="linetypeName">原始线型名称</param>
        /// <returns>有效的线型名称</returns>
        string GetValidLineTypeName(string linetypeName);
    }
}
