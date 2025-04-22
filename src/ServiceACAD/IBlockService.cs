using Autodesk.AutoCAD.DatabaseServices;

namespace ServiceACAD
{
    public interface IBlockService
    {
        bool IsXclipped();
        
        /// <summary>
        /// 检查块参照是否包含属性
        /// </summary>
        /// <returns>如果块参照包含属性返回true，否则返回false</returns>
        bool HasAttributes();
    }
}
