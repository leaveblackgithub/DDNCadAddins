using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;

namespace DDNCadAddins
{
    /// <summary>
    /// CAD插件加载类 - 实现初始化和卸载逻辑
    /// </summary>
    public class DDNCadAppLoader : IExtensionApplication
    {
        /// <summary>
        /// 在DLL加载时显示版本和命令信息
        /// </summary>
        public void Initialize()
        {
            try
            {
                // 获取版本信息
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                string versionInfo = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                
                // 输出插件信息
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    // 获取所有带CommandMethod特性的方法
                    var commands = GetAllCommands();
                    
                    // 输出版本和插件信息
                    doc.Editor.WriteMessage($"\n===========================================");
                    doc.Editor.WriteMessage($"\nDDN CAD插件集已加载 - 版本 {versionInfo}");
                    doc.Editor.WriteMessage($"\n===========================================");
                    doc.Editor.WriteMessage($"\n可用命令列表:");
                    
                    // 输出命令信息
                    foreach (var cmdInfo in commands)
                    {
                        doc.Editor.WriteMessage($"\n· {cmdInfo.CommandName} - {cmdInfo.Description}");
                    }
                    
                    doc.Editor.WriteMessage($"\n===========================================\n");
                }
            }
            catch (System.Exception ex)
            {
                // 忽略错误，确保不会影响CAD的正常运行
                System.Diagnostics.Debug.WriteLine($"插件初始化出错: {ex.Message}");
            }
        }

        /// <summary>
        /// DLL卸载时执行的操作
        /// </summary>
        public void Terminate()
        {
            // 通常不需要做特别的清理工作
        }
        
        /// <summary>
        /// 命令信息类
        /// </summary>
        private class CommandInfo
        {
            public string CommandName { get; set; }
            public string Description { get; set; }
        }
        
        /// <summary>
        /// 获取所有命令方法信息
        /// </summary>
        private List<CommandInfo> GetAllCommands()
        {
            var cmdList = new List<CommandInfo>();
            
            try
            {
                // 获取当前程序集中的所有类型
                var types = Assembly.GetExecutingAssembly().GetTypes();
                
                // 命令方法映射字典 - 使用命令名作为键，方法名作为值
                var cmdMethodMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                // 先收集所有命令和对应的方法
                foreach (var type in types)
                {
                    var methods = type.GetMethods();
                    foreach (var method in methods)
                    {
                        var cmdAttr = method.GetCustomAttributes(typeof(CommandMethodAttribute), false)
                                          .FirstOrDefault() as CommandMethodAttribute;
                        
                        if (cmdAttr != null)
                        {
                            cmdMethodMap[cmdAttr.GlobalName] = method.Name;
                        }
                    }
                }
                
                // 然后为每个命令创建信息对象
                foreach (var entry in cmdMethodMap)
                {
                    string cmdName = entry.Key;
                    string methodName = entry.Value;
                    
                    string description = GetCommandDescription(cmdName, methodName);
                    
                    cmdList.Add(new CommandInfo
                    {
                        CommandName = cmdName,
                        Description = description
                    });
                }
            }
            catch (System.Exception ex)
            {
                // 记录反射过程中的错误
                System.Diagnostics.Debug.WriteLine($"收集命令时出错: {ex.Message}");
            }
            
            return cmdList;
        }
        
        /// <summary>
        /// 获取命令的描述信息
        /// </summary>
        private string GetCommandDescription(string commandName, string methodName)
        {
            // 命令描述字典 - 使用命令名和方法名作为键
            var cmdDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 使用命令名匹配
                { "FindXClippedBlocks", "查找所有被XClip的图块" },
                { "CreateXClippedBlock", "创建XClip测试图块" },
                { "OpenXClipLog", "打开日志文件目录" },
                { "IsolateXClippedBlocks", "隔离显示被XClip的图块" },
                { "Blk2Csv", "导出图块信息到CSV文件" },
                { "ExportBlocksWithAttributes", "导出图块坐标和属性到CSV" },
                
                // 使用方法名匹配
                { "TestTopLevelXClip", "测试顶层XClip图块" },
                { "TestNestedXClip", "测试嵌套XClip图块" },
                { "TestMixedXClip", "测试混合XClip图块场景" },
                { "TestAttributeXClip", "测试带属性的XClip图块" },
                { "TestTransformXClip", "测试变换XClip图块" },
                { "TestComprehensive", "执行综合XClip测试" },
                { "CleanupTests", "清理测试对象" },
                { "RunAutomatedTest", "执行自动化测试" }
            };
            
            // 先用命令名尝试获取描述
            if (cmdDescriptions.TryGetValue(commandName, out string desc))
                return desc;
                
            // 如果命令名未匹配，尝试用方法名
            if (cmdDescriptions.TryGetValue(methodName, out desc))
                return desc;
                
            // 尝试更精确的匹配，如DDNTEST_开头的命令
            if (commandName.StartsWith("DDNTEST_", StringComparison.OrdinalIgnoreCase))
            {
                string testType = commandName.Substring(8); // 去掉DDNTEST_前缀
                switch (testType)
                {
                    case "TOPLEVEL_XCLIP":
                        return "测试顶层XClip图块";
                    case "NESTED_XCLIP":
                        return "测试嵌套XClip图块";
                    case "MIXED_XCLIP":
                        return "测试混合XClip图块场景";
                    case "ATTRIBUTE_XCLIP":
                        return "测试带属性的XClip图块";
                    case "TRANSFORM_XCLIP":
                        return "测试变换XClip图块";
                    case "COMPREHENSIVE":
                        return "执行综合XClip测试";
                    case "CLEANUP":
                        return "清理测试对象";
                    case "AUTO":
                        return "执行自动化测试";
                }
            }
            
            return "CAD命令";
        }
    }
} 