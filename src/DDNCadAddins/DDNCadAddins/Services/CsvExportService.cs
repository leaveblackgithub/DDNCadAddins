using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using DDNCadAddins.Infrastructure;
using DDNCadAddins.Models;
// 使用别名解决命名冲突
using SystemException = System.Exception;

namespace DDNCadAddins.Services
{
    /// <summary>
    /// CSV导出服务实现
    /// </summary>
    public class CsvExportService : ICsvExportService
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public CsvExportService()
        {
        }
        
        /// <summary>
        /// 导出图块数据到CSV文件
        /// </summary>
        /// <param name="blockData">图块数据字典</param>
        /// <param name="allAttributeTags">所有属性标签集合</param>
        /// <param name="csvFilePath">CSV文件保存路径</param>
        /// <returns>操作结果，包含导出的记录数</returns>
        public OperationResult<int> ExportToCsv(
            Dictionary<ObjectId, Dictionary<string, string>> blockData,
            HashSet<string> allAttributeTags,
            string csvFilePath)
        {
            if (blockData == null) {return OperationResult<int>.ErrorResult("图块数据为空", TimeSpan.Zero);}
                
            if (allAttributeTags == null) {return OperationResult<int>.ErrorResult("属性标签集合为空", TimeSpan.Zero);}
                
            if (string.IsNullOrEmpty(csvFilePath)) {return OperationResult<int>.ErrorResult("CSV文件路径无效", TimeSpan.Zero);}
                
            DateTime startTime = DateTime.Now;
            
            try
            {
                // 按照收集到的所有属性名称排序创建CSV表头
                List<string> sortedTags = allAttributeTags.ToList();
                sortedTags.Sort();
                
                // 将BlockName, X, Y, Z放在前面
                if (sortedTags.Contains("Z")) {sortedTags.Remove("Z");}
                if (sortedTags.Contains("Y")) {sortedTags.Remove("Y");}
                if (sortedTags.Contains("X")) {sortedTags.Remove("X");}
                if (sortedTags.Contains("BlockName")) {sortedTags.Remove("BlockName");}
                
                sortedTags.Insert(0, "Z");
                sortedTags.Insert(0, "Y");
                sortedTags.Insert(0, "X");
                sortedTags.Insert(0, "BlockName");
                
                // 创建并写入CSV文件
                using (StreamWriter sw = new StreamWriter(csvFilePath, false, Encoding.UTF8))
                {
                    // 写入表头
                    string header = string.Join(",", sortedTags.Select(tag => EscapeCsvField(tag)));
                    sw.WriteLine(header);
                    
                    // 写入每个图块的数据
                    foreach (var blockEntry in blockData)
                    {
                        var values = blockEntry.Value;
                        
                        List<string> rowData = new List<string>();
                        foreach (string tag in sortedTags)
                        {
                            if (values.ContainsKey(tag))
                            {
                                rowData.Add(EscapeCsvField(values[tag]));
                            }
                            else
                            {
                                rowData.Add(string.Empty);
                            }
                        }
                        
                        string row = string.Join(",", rowData);
                        sw.WriteLine(row);
                    }
                }
                
                TimeSpan duration = DateTime.Now - startTime;
                return OperationResult<int>.SuccessResult(blockData.Count, duration);
            }
            catch (SystemException ex)
            {
                TimeSpan duration = DateTime.Now - startTime;
                return OperationResult<int>.ErrorResult($"写入CSV文件时出错: {ex.Message}", duration);
            }
        }
        
        /// <summary>
        /// 转义CSV字段，确保包含逗号、引号或换行符的字段正确格式化
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) {return string.Empty;}
                
            // 检查是否需要转义
            bool needsEscaping = field.Contains(",") || field.Contains("\"") || 
                                 field.Contains("\r") || field.Contains("\n");
                                 
            if (needsEscaping)
            {
                // 将字段中的双引号替换为两个双引号
                field = field.Replace("\"", "\"\"");
                // 在字段两端添加双引号
                field = "\"" + field + "\"";
            }
            
            return field;
        }
    }
} 