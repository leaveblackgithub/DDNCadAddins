using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// 图块数据服务实现
    /// </summary>
    public class BlockDataService : IBlockDataService
    {
        /// <summary>
        /// 从选定的图块中提取数据
        /// </summary>
        /// <param name="blockIds">图块对象ID集合</param>
        /// <param name="transaction">数据库事务</param>
        /// <returns>图块数据和所有属性标签的元组</returns>
        public (Dictionary<ObjectId, Dictionary<string, string>> BlockData, HashSet<string> AllAttributeTags) ExtractBlockData(
            IEnumerable<ObjectId> blockIds, 
            Transaction transaction)
        {
            HashSet<string> allAttribTags = new HashSet<string>();
            Dictionary<ObjectId, Dictionary<string, string>> blockData = new Dictionary<ObjectId, Dictionary<string, string>>();
            
            // 添加基础属性名到集合
            allAttribTags.Add("BlockName");
            allAttribTags.Add("X");
            allAttribTags.Add("Y");
            allAttribTags.Add("Z");
            
            foreach (ObjectId blockId in blockIds)
            {
                BlockReference blockRef = transaction.GetObject(blockId, OpenMode.ForRead) as BlockReference;
                if (blockRef == null)
                    continue;
                
                // 获取块定义名称
                BlockTableRecord blockDef = transaction.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                string blockName = blockDef.Name;
                
                // 创建该图块的属性字典
                Dictionary<string, string> attribValues = new Dictionary<string, string>();
                attribValues["BlockName"] = blockName;
                attribValues["X"] = blockRef.Position.X.ToString("0.000");
                attribValues["Y"] = blockRef.Position.Y.ToString("0.000");
                attribValues["Z"] = blockRef.Position.Z.ToString("0.000");
                
                // 提取属性值
                foreach (ObjectId attId in blockRef.AttributeCollection)
                {
                    AttributeReference attRef = transaction.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (attRef != null)
                    {
                        string tag = attRef.Tag;
                        string value = attRef.TextString;
                        
                        // 添加到字典和集合
                        attribValues[tag] = value;
                        allAttribTags.Add(tag);
                    }
                }
                
                // 保存该图块的数据
                blockData[blockRef.ObjectId] = attribValues;
            }
            
            return (blockData, allAttribTags);
        }
    }
} 