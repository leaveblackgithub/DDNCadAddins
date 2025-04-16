using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Services;

namespace DDNCadAddins.Commands.XClipTest
{
    /// <summary>
    /// XClip测试验证类 - 负责执行测试验证
    /// </summary>
    public class XClipTestValidator
    {
        private readonly IAcadService _acadService;
        private readonly IXClipBlockService _xclipBlockService;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="acadService">AutoCAD服务接口</param>
        /// <param name="xclipBlockService">XClip块服务接口</param>
        public XClipTestValidator(IAcadService acadService, IXClipBlockService xclipBlockService)
        {
            _acadService = acadService ?? throw new ArgumentNullException(nameof(acadService));
            _xclipBlockService = xclipBlockService ?? throw new ArgumentNullException(nameof(xclipBlockService));
        }
        
        /// <summary>
        /// 自动执行ISOLATEXCLIPPEDBLOCKS命令
        /// </summary>
        public void ExecuteIsolateCommand(Document doc)
        {
            try
            {
                Editor ed = doc.Editor;
                
                // 清除之前的选择集，确保命令执行环境干净
                ed.SetImpliedSelection(new ObjectId[0]);
                
                // 确认当前没有活动命令
                if (ed.IsQuiescent == false)
                {
                    // 等待当前命令完成
                    System.Threading.Thread.Sleep(500);
                    // 尝试取消当前命令
                    doc.SendStringToExecute("\x1B\x1B", true, false, false); // 发送ESC
                    System.Threading.Thread.Sleep(500);
                }
                
                // 使用FindXClippedBlocks查找XClip图块，然后使用IsolateXClippedBlocks隔离它们
                // 这样可以避免使用命令行，完全通过API实现自动化
                ed.WriteMessage("\n===== 开始清理测试数据 =====");
                var result = _xclipBlockService.FindXClippedBlocks(doc.Database, ed);
                
                if (!result.Success)
                {
                    throw new System.Exception($"查找XClip图块失败: {result.ErrorMessage}");
                }
                
                if (result.Data.Count == 0)
                {
                    ed.WriteMessage("\n没有找到XClip图块，可能需要先创建测试图块");
                    return;
                }
                
                ed.WriteMessage($"\n找到 {result.Data.Count} 个XClip图块，正在隔离...");
                var isolateResult = _xclipBlockService.IsolateXClippedBlocks(doc.Database, result.Data);
                
                if (!isolateResult.Success)
                {
                    throw new System.Exception($"隔离XClip图块失败: {isolateResult.ErrorMessage}");
                }
                
                ed.WriteMessage($"\n成功隔离XClip图块: {isolateResult.Message}");
            }
            catch (System.Exception ex)
            {
                throw new System.Exception($"执行ISOLATEXCLIPPEDBLOCKS命令失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 捕获块的初始状态
        /// </summary>
        public Dictionary<ObjectId, BlockState> CaptureBlockStates(Database db, List<ObjectId> blockIds)
        {
            Dictionary<ObjectId, BlockState> states = new Dictionary<ObjectId, BlockState>();
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (var blockId in blockIds)
                {
                    if (blockId == ObjectId.Null || !blockId.IsValid)
                        continue;
                        
                    try
                    {
                        // 获取块参照
                        BlockReference blockRef = tr.GetObject(blockId, OpenMode.ForRead) as BlockReference;
                        if (blockRef == null)
                            continue;
                            
                        // 获取块定义
                        BlockTableRecord blockDef = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        if (blockDef == null)
                            continue;
                            
                        // 记录块状态
                        BlockState state = new BlockState
                        {
                            BlockId = blockId,
                            BlockName = blockDef.Name,
                            Position = blockRef.Position,
                            LayerId = blockRef.LayerId,
                            Color = blockRef.Color,
                            Linetype = ObjectId.Null, // 使用默认值或null值，在后面处理
                            LinetypeScale = blockRef.LinetypeScale,
                            IsXClipped = _acadService.IsBlockXClipped(tr, blockRef, out string ignored),
                            ParentId = FindParentBlockId(tr, blockId),
                            Rotation = GetBlockRotation(blockRef),
                            Scale = GetBlockScale(blockRef)
                        };
                        
                        // 处理线型
                        try {
                            // 尝试获取线型ID
                            LinetypeTable ltTable = tr.GetObject(blockRef.Database.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                            if (ltTable != null && ltTable.Has(blockRef.Linetype)) {
                                state.Linetype = ltTable[blockRef.Linetype];
                            }
                        } catch {
                            // 如果获取失败，使用Null
                        }
                        
                        states[blockId] = state;
                    }
                    catch
                    {
                        // 忽略单个块的错误，继续处理其他块
                    }
                }
                
                tr.Commit();
            }
            
            return states;
        }

        /// <summary>
        /// 验证测试结果
        /// </summary>
        public void VerifyResults(Database db, Dictionary<ObjectId, BlockState> initialStates)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            
            int totalTests = 0;
            int passedTests = 0;
            
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 获取模型空间
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                
                // 查找所有XClipped块
                var result = _xclipBlockService.FindXClippedBlocks(db, doc.Editor);
                
                if (!result.Success)
                {
                    ed.WriteMessage($"\n测试失败: 无法查找XClipped块: {result.ErrorMessage}");
                    return;
                }
                
                // 如果没有找到XClipped块，则测试失败
                if (result.Data.Count == 0)
                {
                    ed.WriteMessage("\n测试失败: 隔离后未找到任何XClipped块");
                    return;
                }
                
                // 验证找到的XClipped块
                foreach (var blockInfo in result.Data)
                {
                    totalTests++;
                    
                    // 找到对应的初始状态
                    BlockState initialState = null;
                    foreach (var state in initialStates.Values)
                    {
                        if (state.BlockName.Equals(blockInfo.BlockName))
                        {
                            initialState = state;
                            break;
                        }
                    }
                    
                    if (initialState == null)
                    {
                        ed.WriteMessage($"\n无法找到块 {blockInfo.BlockName} 的初始状态，跳过验证");
                        continue;
                    }
                    
                    // 获取当前块
                    BlockReference currentBlock = tr.GetObject(blockInfo.BlockReferenceId, OpenMode.ForRead) as BlockReference;
                    if (currentBlock == null)
                    {
                        ed.WriteMessage($"\n验证失败: 无法获取块 {blockInfo.BlockName} 的当前状态");
                        continue;
                    }
                    
                    // 1. 验证位置和旋转是否保持
                    bool positionCorrect = IsPositionCorrect(currentBlock, initialState);
                    
                    // 2. 验证颜色是否保持
                    bool colorCorrect = IsColorCorrect(currentBlock, initialState);
                    
                    // 3. 验证线型是否保持
                    bool linetypeCorrect = IsLinetypeCorrect(currentBlock, initialState);
                    
                    // 4. 验证所有嵌套块是否都被移到了顶层
                    bool isTopLevel = IsBlockTopLevel(tr, currentBlock);
                    
                    // 组合结果
                    bool testPassed = positionCorrect && colorCorrect && linetypeCorrect && isTopLevel;
                    
                    if (testPassed)
                        passedTests++;
                    
                    // 输出验证结果
                    string resultStr = testPassed ? "通过" : "失败";
                    ed.WriteMessage($"\n块 {blockInfo.BlockName} 测试结果: {resultStr}");
                    ed.WriteMessage($"  - 位置/旋转: {(positionCorrect ? "√" : "×")}");
                    ed.WriteMessage($"  - 颜色: {(colorCorrect ? "√" : "×")}");
                    ed.WriteMessage($"  - 线型: {(linetypeCorrect ? "√" : "×")}");
                    ed.WriteMessage($"  - 顶层: {(isTopLevel ? "√" : "×")}");
                }
                
                // 输出总结果
                double passRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;
                string finalResult = passedTests == totalTests ? "全部通过" : "部分失败";
                ed.WriteMessage($"\n\n测试结果汇总: {finalResult}");
                ed.WriteMessage($"总测试数: {totalTests}, 通过数: {passedTests}, 通过率: {passRate:F2}%");
                
                tr.Commit();
            }
        }

        /// <summary>
        /// 判断块的位置是否正确
        /// </summary>
        private bool IsPositionCorrect(BlockReference currentBlock, BlockState initialState)
        {
            try
            {
                // 允许的位置误差
                const double tolerance = 0.001;
                
                // 比较位置
                bool positionMatch = 
                    Math.Abs(currentBlock.Position.X - initialState.Position.X) < tolerance &&
                    Math.Abs(currentBlock.Position.Y - initialState.Position.Y) < tolerance &&
                    Math.Abs(currentBlock.Position.Z - initialState.Position.Z) < tolerance;
                    
                // 比较旋转
                double currentRotation = GetBlockRotation(currentBlock);
                bool rotationMatch = Math.Abs(currentRotation - initialState.Rotation) < tolerance;
                
                // 比较缩放
                double currentScale = GetBlockScale(currentBlock);
                bool scaleMatch = Math.Abs(currentScale - initialState.Scale) < tolerance;
                
                return positionMatch && rotationMatch && scaleMatch;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断块的颜色是否正确
        /// </summary>
        private bool IsColorCorrect(BlockReference currentBlock, BlockState initialState)
        {
            try
            {
                // 如果初始颜色为BYLAYER，则当前块应该保持BYLAYER
                if (initialState.Color.ColorMethod == Autodesk.AutoCAD.Colors.ColorMethod.ByLayer)
                    return currentBlock.Color.ColorMethod == Autodesk.AutoCAD.Colors.ColorMethod.ByLayer;
                    
                // 如果初始颜色为BYBLOCK，则当前块应该保持BYBLOCK
                if (initialState.Color.ColorMethod == Autodesk.AutoCAD.Colors.ColorMethod.ByBlock)
                    return currentBlock.Color.ColorMethod == Autodesk.AutoCAD.Colors.ColorMethod.ByBlock;
                    
                // 对于其他颜色方法，比较颜色索引
                return currentBlock.Color.ColorIndex == initialState.Color.ColorIndex;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断块的线型是否正确
        /// </summary>
        private bool IsLinetypeCorrect(BlockReference currentBlock, BlockState initialState)
        {
            try
            {
                // 比较线型 - 将两者都转换为字符串进行比较，避免类型不匹配问题
                string currentLinetype = currentBlock.Linetype.ToString();
                string initialLinetype = initialState.Linetype.ToString();
                bool linetypeMatch = currentLinetype == initialLinetype;
                
                // 比较线型比例
                const double tolerance = 0.001;
                bool scaleMatch = Math.Abs(currentBlock.LinetypeScale - initialState.LinetypeScale) < tolerance;
                
                return linetypeMatch && scaleMatch;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断块是否在顶层
        /// </summary>
        private bool IsBlockTopLevel(Transaction tr, BlockReference blockRef)
        {
            try
            {
                // 获取块所在的容器对象
                ObjectId ownerId = blockRef.OwnerId;
                
                // 获取模型空间ID
                BlockTable bt = tr.GetObject(blockRef.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                ObjectId msId = bt[BlockTableRecord.ModelSpace];
                
                // 如果块所在的容器是模型空间，则它是顶层块
                return ownerId == msId;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取块的父块ID
        /// </summary>
        private ObjectId FindParentBlockId(Transaction tr, ObjectId blockId)
        {
            try
            {
                // 获取块参照
                BlockReference blockRef = tr.GetObject(blockId, OpenMode.ForRead) as BlockReference;
                if (blockRef == null)
                    return ObjectId.Null;
                    
                // 获取块所在的容器对象
                ObjectId ownerId = blockRef.OwnerId;
                
                // 获取模型空间ID
                BlockTable bt = tr.GetObject(blockRef.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                ObjectId msId = bt[BlockTableRecord.ModelSpace];
                
                // 如果块所在的容器是模型空间，则它没有父块
                if (ownerId == msId)
                    return ObjectId.Null;
                    
                // 否则，容器就是父块定义
                return ownerId;
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        /// <summary>
        /// 获取块的旋转角度
        /// </summary>
        private double GetBlockRotation(BlockReference blockRef)
        {
            try
            {
                // 从块的变换矩阵中提取旋转角度
                Matrix3d blockTransform = blockRef.BlockTransform;
                Vector3d xAxis = blockTransform.CoordinateSystem3d.Xaxis;
                
                // 计算XY平面上的旋转角度
                return Math.Atan2(xAxis.Y, xAxis.X);
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// 获取块的缩放比例
        /// </summary>
        private double GetBlockScale(BlockReference blockRef)
        {
            try
            {
                // 从块的变换矩阵中提取缩放比例
                Matrix3d blockTransform = blockRef.BlockTransform;
                Vector3d xAxis = blockTransform.CoordinateSystem3d.Xaxis;
                
                // 使用X轴向量的长度作为缩放比例
                return xAxis.Length;
            }
            catch
            {
                return 1.0;
            }
        }
    }
} 