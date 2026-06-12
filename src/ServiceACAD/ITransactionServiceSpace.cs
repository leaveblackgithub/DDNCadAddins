using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    /// <summary>
    /// 事务服务空间操作接口 - 块表和空间管理
    /// </summary>
    public interface ITransactionServiceSpace
    {
        /// <summary>
        /// 获取块表
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>块表</returns>
        BlockTable GetBlockTable(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        /// 获取模型空间
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>模型空间块表记录</returns>
        BlockTableRecord GetModelSpace(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        /// 获取当前空间（模型空间或纸空间）
        /// </summary>
        /// <param name="openMode">打开模式</param>
        /// <returns>当前空间块表记录</returns>
        BlockTableRecord GetCurrentSpace(OpenMode openMode = OpenMode.ForRead);

        /// <summary>
        /// 获取块表记录ID
        /// </summary>
        /// <param name="name">块名称</param>
        /// <returns>块表记录ID</returns>
        ObjectId GetBlockTableRecordId(string name);

        /// <summary>
        /// 获取块表记录
        /// </summary>
        /// <param name="name">块名称</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块表记录</returns>
        BlockTableRecord GetBlockTableRecord(string name, OpenMode openMode = OpenMode.ForRead);
    }
}
