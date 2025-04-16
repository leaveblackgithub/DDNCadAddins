using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// AutoCAD API服务接口 - 所有与CAD API的交互都通过此接口
    /// </summary>
    public interface IAcadService
    {
        /// <summary>
        /// 获取当前活动文档
        /// </summary>
        /// <returns>当前文档是否可用</returns>
        bool GetActiveDocument(out Database database, out Editor editor);
        
        /// <summary>
        /// 获取当前活动文档对象
        /// </summary>
        /// <returns>当前活动文档对象，如果没有打开的文档则返回null</returns>
        Document GetMdiActiveDocument();
        
        /// <summary>
        /// 显示警告对话框
        /// </summary>
        /// <param name="message">显示的消息</param>
        void ShowAlertDialog(string message);
        
        /// <summary>
        /// 执行事务操作
        /// </summary>
        /// <typeparam name="T">返回数据类型</typeparam>
        /// <param name="database">数据库</param>
        /// <param name="action">要在事务中执行的操作</param>
        /// <param name="errorMessagePrefix">错误消息前缀</param>
        /// <returns>操作结果</returns>
        OperationResult<T> ExecuteInTransaction<T>(Database database, Func<Transaction, T> action, string errorMessagePrefix);
        
        /// <summary>
        /// 执行事务操作（无返回值）
        /// </summary>
        /// <param name="database">数据库</param>
        /// <param name="action">要在事务中执行的操作</param>
        /// <param name="errorMessagePrefix">错误消息前缀</param>
        /// <returns>操作结果</returns>
        OperationResult ExecuteInTransaction(Database database, Action<Transaction> action, string errorMessagePrefix);
        
        /// <summary>
        /// 获取块参照对象
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRefId">块参照ID</param>
        /// <param name="openMode">打开模式</param>
        /// <returns>块参照对象，如果获取失败则返回null</returns>
        BlockReference GetBlockReference(Transaction tr, ObjectId blockRefId, OpenMode openMode = OpenMode.ForRead);
        
        /// <summary>
        /// 获取块的几何边界
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <returns>几何边界，如果获取失败则返回null</returns>
        Extents3d? GetBlockGeometricExtents(BlockReference blockRef);
        
        /// <summary>
        /// 获取块的属性信息
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRef">块参照</param>
        /// <returns>块名称和定义的元组，如果获取失败则返回null</returns>
        (string BlockName, BlockTableRecord BlockDef)? GetBlockInfo(Transaction tr, BlockReference blockRef);
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockName">块名称</param>
        /// <param name="insertionPoint">插入点</param>
        /// <returns>创建的块参照ID</returns>
        ObjectId CreateTestBlock(Transaction tr, string blockName, Point3d insertionPoint);
        
        /// <summary>
        /// 查找所有块参照
        /// </summary>
        /// <param name="editor">编辑器</param>
        /// <returns>块参照ID数组</returns>
        ObjectId[] FindAllBlockReferences(Editor editor);
        
        /// <summary>
        /// 检查块是否被XClip
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="blockRef">块参照</param>
        /// <param name="detectionMethod">检测方法</param>
        /// <returns>是否被XClip</returns>
        bool IsBlockXClipped(Transaction tr, BlockReference blockRef, out string detectionMethod);
        
        /// <summary>
        /// 执行XClip命令
        /// </summary>
        /// <param name="blockRefId">块参照ID</param>
        /// <param name="minPoint">裁剪边界最小点</param>
        /// <param name="maxPoint">裁剪边界最大点</param>
        /// <returns>操作结果</returns>
        OperationResult ExecuteXClipCommand(ObjectId blockRefId, Point3d minPoint, Point3d maxPoint);
        
        /// <summary>
        /// 写入消息到命令行
        /// </summary>
        /// <param name="message">消息内容</param>
        void WriteMessage(string message);
        Editor GetEditor();
    }
} 