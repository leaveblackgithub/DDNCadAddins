using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DDNCadAddins.Infrastructure;
// 添加Windows表单用于文件对话框
using System.Windows.Forms;
// 使用别名解决命名冲突
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SystemException = System.Exception;

namespace DDNCadAddins.Commands
{
    /// <summary>
    /// 提取图块信息到CSV文件命令类
    /// </summary>
    public class Blk2CsvCommand
    {
        private readonly ILogger _logger;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        public Blk2CsvCommand()
        {
            _logger = new FileLogger();
        }
        
        /// <summary>
        /// 图块信息提取到CSV命令
        /// </summary>
        [CommandMethod("Blk2Csv")]
        public void Execute()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                AcadApp.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }
            
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            // 初始化日志
            _logger.Initialize("Blk2Csv");
            
            try
            {
                // 选择图块
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\n请选择要导出信息的图块: ";
                selOpts.AllowDuplicates = false;
                
                // 创建图块过滤器
                TypedValue[] filterList = new TypedValue[] {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                };
                SelectionFilter filter = new SelectionFilter(filterList);
                
                PromptSelectionResult selResult = ed.GetSelection(selOpts, filter);
                if (selResult.Status != PromptStatus.OK)
                    return;
                
                SelectionSet ss = selResult.Value;
                if (ss == null || ss.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何图块。");
                    return;
                }
                
                // 获取要保存的CSV文件路径
                string initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string defaultFileName = $"BlockData_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.csv";
                string fullPath = Path.Combine(initialDir, defaultFileName);
                
                // 使用Windows表单的SaveFileDialog替代PromptFileNameOptions
                string csvFilePath = string.Empty;
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "保存图块数据到CSV文件";
                    saveDialog.Filter = "CSV文件 (*.csv)|*.csv";
                    saveDialog.InitialDirectory = initialDir;
                    saveDialog.FileName = defaultFileName;
                    saveDialog.DefaultExt = "csv";
                    
                    // 在AutoCAD应用程序中显示对话框
                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        csvFilePath = saveDialog.FileName;
                    }
                    else
                    {
                        return; // 用户取消了保存操作
                    }
                }
                
                // 收集所有图块的属性名称，用于CSV表头
                HashSet<string> allAttribTags = new HashSet<string>();
                Dictionary<ObjectId, Dictionary<string, string>> blockData = new Dictionary<ObjectId, Dictionary<string, string>>();
                
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 首先收集所有图块的属性名称
                    foreach (SelectedObject selObj in ss)
                    {
                        BlockReference blockRef = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as BlockReference;
                        if (blockRef == null)
                            continue;
                        
                        // 获取块定义名称
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        string blockName = blockDef.Name;
                        
                        // 创建该图块的属性字典
                        Dictionary<string, string> attribValues = new Dictionary<string, string>();
                        attribValues["BlockName"] = blockName;
                        attribValues["X"] = blockRef.Position.X.ToString();
                        attribValues["Y"] = blockRef.Position.Y.ToString();
                        attribValues["Z"] = blockRef.Position.Z.ToString();
                        
                        // 添加基础属性名到集合
                        allAttribTags.Add("BlockName");
                        allAttribTags.Add("X");
                        allAttribTags.Add("Y");
                        allAttribTags.Add("Z");
                        
                        // 提取属性值
                        foreach (ObjectId attId in blockRef.AttributeCollection)
                        {
                            AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
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
                    
                    tr.Commit();
                }
                
                // 将数据写入CSV文件
                try
                {
                    // 按照收集到的所有属性名称排序创建CSV表头
                    List<string> sortedTags = allAttribTags.ToList();
                    sortedTags.Sort();
                    
                    // 将BlockName, X, Y, Z放在前面
                    if (sortedTags.Contains("Z")) sortedTags.Remove("Z");
                    if (sortedTags.Contains("Y")) sortedTags.Remove("Y");
                    if (sortedTags.Contains("X")) sortedTags.Remove("X");
                    if (sortedTags.Contains("BlockName")) sortedTags.Remove("BlockName");
                    
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
                    
                    int blockCount = blockData.Count;
                    string resultMessage = $"\n已导出 {blockCount} 个图块数据到: {csvFilePath}";
                    ed.WriteMessage(resultMessage);
                    _logger.Log(resultMessage);
                    
                    // 询问是否打开CSV文件
                    PromptKeywordOptions keyOpts = new PromptKeywordOptions("\n是否打开CSV文件? ");
                    keyOpts.Keywords.Add("是");
                    keyOpts.Keywords.Add("否");
                    keyOpts.Keywords.Default = "是";
                    keyOpts.AllowNone = true;
                    
                    PromptResult keyRes = ed.GetKeywords(keyOpts);
                    if (keyRes.Status == PromptStatus.OK && 
                        (keyRes.StringResult == "是" || string.IsNullOrEmpty(keyRes.StringResult)))
                    {
                        System.Diagnostics.Process.Start(csvFilePath);
                    }
                }
                catch (SystemException ex)
                {
                    string errorMessage = $"写入CSV文件时出错: {ex.Message}";
                    ed.WriteMessage("\n" + errorMessage);
                    _logger.LogError(errorMessage, ex);
                }
            }
            catch (SystemException ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                ed.WriteMessage("\n" + errorMessage);
                _logger.LogError(errorMessage, ex);
            }
            finally
            {
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 图块坐标和属性导出到CSV命令
        /// </summary>
        [CommandMethod("ExportBlocksWithAttributes")]
        public void ExportBlocksWithAttributes()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                AcadApp.ShowAlertDialog("当前没有打开的CAD文档");
                return;
            }
            
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            // 初始化日志
            _logger.Initialize("ExportBlocksWithAttributes");
            
            try
            {
                // 选择图块
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\n请选择要导出信息的图块: ";
                selOpts.AllowDuplicates = false;
                
                // 创建图块过滤器
                TypedValue[] filterList = new TypedValue[] {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                };
                SelectionFilter filter = new SelectionFilter(filterList);
                
                PromptSelectionResult selResult = ed.GetSelection(selOpts, filter);
                if (selResult.Status != PromptStatus.OK)
                    return;
                
                SelectionSet ss = selResult.Value;
                if (ss == null || ss.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何图块。");
                    return;
                }
                
                // 获取要保存的CSV文件路径
                string initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string defaultFileName = $"BlockData_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.csv";
                string fullPath = Path.Combine(initialDir, defaultFileName);
                
                // 使用Windows表单的SaveFileDialog替代AutoCAD的PromptFileNameOptions
                string csvFilePath = string.Empty;
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.Title = "保存图块数据到CSV文件";
                    saveDialog.Filter = "CSV文件 (*.csv)|*.csv";
                    saveDialog.InitialDirectory = initialDir;
                    saveDialog.FileName = defaultFileName;
                    saveDialog.DefaultExt = "csv";
                    
                    // 在AutoCAD应用程序中显示对话框
                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        csvFilePath = saveDialog.FileName;
                    }
                    else
                    {
                        return; // 用户取消了保存操作
                    }
                }
                
                // 收集所有图块的属性名称，用于CSV表头
                HashSet<string> allAttribTags = new HashSet<string>();
                Dictionary<ObjectId, Dictionary<string, string>> blockData = new Dictionary<ObjectId, Dictionary<string, string>>();
                
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 首先收集所有图块的属性名称
                    foreach (SelectedObject selObj in ss)
                    {
                        BlockReference blockRef = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as BlockReference;
                        if (blockRef == null)
                            continue;
                        
                        // 获取块定义名称
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        string blockName = blockDef.Name;
                        
                        // 创建该图块的属性字典
                        Dictionary<string, string> attribValues = new Dictionary<string, string>();
                        attribValues["BlockName"] = blockName;
                        attribValues["X"] = blockRef.Position.X.ToString("0.000");
                        attribValues["Y"] = blockRef.Position.Y.ToString("0.000");
                        attribValues["Z"] = blockRef.Position.Z.ToString("0.000");
                        
                        // 添加基础属性名到集合
                        allAttribTags.Add("BlockName");
                        allAttribTags.Add("X");
                        allAttribTags.Add("Y");
                        allAttribTags.Add("Z");
                        
                        // 提取属性值
                        foreach (ObjectId attId in blockRef.AttributeCollection)
                        {
                            AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
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
                    
                    tr.Commit();
                }
                
                // 将数据写入CSV文件
                try
                {
                    // 按照收集到的所有属性名称排序创建CSV表头
                    List<string> sortedTags = allAttribTags.ToList();
                    sortedTags.Sort();
                    
                    // 将BlockName, X, Y, Z放在前面
                    if (sortedTags.Contains("Z")) sortedTags.Remove("Z");
                    if (sortedTags.Contains("Y")) sortedTags.Remove("Y");
                    if (sortedTags.Contains("X")) sortedTags.Remove("X");
                    if (sortedTags.Contains("BlockName")) sortedTags.Remove("BlockName");
                    
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
                    
                    int blockCount = blockData.Count;
                    string resultMessage = $"\n已导出 {blockCount} 个图块数据到: {csvFilePath}";
                    ed.WriteMessage(resultMessage);
                    _logger.Log(resultMessage);
                    
                    // 询问是否打开CSV文件
                    PromptKeywordOptions keyOpts = new PromptKeywordOptions("\n是否打开CSV文件? ");
                    keyOpts.Keywords.Add("是");
                    keyOpts.Keywords.Add("否");
                    keyOpts.Keywords.Default = "是";
                    keyOpts.AllowNone = true;
                    
                    PromptResult keyRes = ed.GetKeywords(keyOpts);
                    if (keyRes.Status == PromptStatus.OK && 
                        (keyRes.StringResult == "是" || string.IsNullOrEmpty(keyRes.StringResult)))
                    {
                        System.Diagnostics.Process.Start(csvFilePath);
                    }
                }
                catch (SystemException ex)
                {
                    string errorMessage = $"写入CSV文件时出错: {ex.Message}";
                    ed.WriteMessage("\n" + errorMessage);
                    _logger.LogError(errorMessage, ex);
                }
            }
            catch (SystemException ex)
            {
                string errorMessage = $"执行命令时出错: {ex.Message}";
                ed.WriteMessage("\n" + errorMessage);
                _logger.LogError(errorMessage, ex);
            }
            finally
            {
                _logger.Close();
            }
        }
        
        /// <summary>
        /// 转义CSV字段，确保包含逗号、引号或换行符的字段正确格式化
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;
                
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