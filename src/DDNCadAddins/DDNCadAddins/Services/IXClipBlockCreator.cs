using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Models;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// XClip块创建服务接口 - 负责创建和应用XClip相关操作
    /// 遵循接口隔离原则，从IXClipBlockService拆分
    /// </summary>
    public interface IXClipBlockCreator
    {
        /// <summary>
        /// 使用矩形裁剪块
        /// </summary>
        /// <param name="blockRef">块参照</param>
        /// <param name="p1">矩形第一个点</param>
        /// <param name="p2">矩形第二个点</param>
        /// <returns>裁剪操作是否成功</returns>
        bool ClipBlockWithRectangle(BlockReference blockRef, Point3d p1, Point3d p2);
        
        /// <summary>
        /// 创建测试块
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果</returns>
        OperationResult CreateTestBlock(Database database);
        
        /// <summary>
        /// 创建测试块并返回块ID
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <returns>操作结果，包含创建的块ID</returns>
        OperationResult<ObjectId> CreateTestBlockWithId(Database database);
        
        /// <summary>
        /// 自动对图块进行XClip裁剪
        /// </summary>
        /// <param name="database">当前CAD数据库</param>
        /// <param name="blockRefId">图块参照ID</param>
        /// <returns>操作结果</returns>
        OperationResult AutoXClipBlock(Database database, ObjectId blockRefId);
        
        /// <summary>
        /// 设置是否抑制日志输出
        /// </summary>
        /// <param name="suppress">是否抑制</param>
        void SetLoggingSuppression(bool suppress);
    }
} 