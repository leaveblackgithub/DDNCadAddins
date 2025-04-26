using AddinsAcad.ServiceTests;
using AddinsACAD.TestCommands;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using ServiceACAD;

[assembly: CommandClass(typeof(CreateTestBlockForExplodeCommand))]

namespace AddinsACAD.TestCommands
{
    /// <summary>
    /// 创建用于测试爆炸命令的测试块
    /// </summary>
    public class CreateTestBlockForExplodeCommand
    {
        /// <summary>
        /// 执行命令
        /// </summary>
        [CommandMethod("CreateTestBlockForExplode")]
        public void Execute()
        {
            try
            {
                

                // 使用事务服务执行操作
                CadServiceManager._.ExecuteInTransactions("", (serviceTrans) =>
                {
                    // 调用测试工具类创建测试块
                    ObjectId blockRefId = BlockServiceTestUtils.CreateTestBlockForExplodeCommand(serviceTrans);
                    
                    if (blockRefId.IsValid)
                    {
                        CadServiceManager.ServiceEd.WriteMessage("\n测试块创建成功！");
                    }
                    else
                    {
                        CadServiceManager.ServiceEd.WriteMessage("\n测试块创建失败！");
                    }
                });
            }
            catch (System.Exception ex)
            {
                var message = $"创建测试块时发生错误: {ex.Message}";
                CadServiceManager.ServiceEd.WriteMessage(message);
                Logger._.Error(message);
            }
        }
    }
} 
