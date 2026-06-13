# DDNCadAddins 分层架构重构计划



## 背景与动机



### 问题

- 所有测试必须在 AutoCAD 进程内运行，容易死锁（卡死）

- 业务逻辑与 CAD API 调用混合，无法独立测试

- 测试速度慢（分钟级），且存在图纸污染风险



### 目标

- 将业务逻辑下沉到不依赖 CAD 的 Core 层

- 99% 的测试脱离 CAD 环境，秒级完成

- CAD 进程内只保留少量集成测试



---



## 目标架构



```

DDNCadAddins/

├── DDNCadAddins.Core/              ← 纯 .NET，零 CAD 依赖

│   ├── Interfaces/                 ← 抽象仓储和服务接口

│   ├── Models/                     ← POCO 领域模型

│   └── Services/                   ← 业务逻辑服务

│

├── DDNCadAddins.Core.Tests/        ← 纯单元测试（手写 Fake 隔离）

│   └── ...                         ← 无需 CAD 环境，秒级完成

│

├── ServiceACAD/                    ← CAD 基础设施层（保持现状）

│   └── Adapters/                   ← 实现 Core 接口的 CAD 适配器（新增）

│

└── AddinsACAD/                     ← CAD 命令层

    └── Commands/                   ← 组合 Core 服务 + CAD 适配器

```



### 依赖方向



```

AddinsACAD → Core ← ServiceACAD/Adapters

               ↑

        Core.Tests (Fake)

```



- `Core` 不依赖任何人

- `ServiceACAD` 实现 `Core` 的接口（依赖 Core）

- `AddinsACAD` 依赖 `Core` 和 `ServiceACAD`

- `Core.Tests` 只依赖 `Core`，用手写 Fake 替代 CAD 实现



---



## 渐进式迁移路线图



### ✅ 阶段1：HelloWorld PoC（已完成）



**目标**：验证分层架构可行性，搭建基础框架。



**完成内容**：

- 创建 `DDNCadAddins.Core` 项目（无 CAD 引用）

- 创建 `DDNCadAddins.Core.Tests` 项目（NUnit，无 CAD）

- 实现 `ICalculatorService` / `CalculatorService` HelloWorld 示例

- 编写 12 个纯单元测试，全部通过

- 创建 `HelloWorldCommand`（CAD 命令 `HELLO` 调用 Core 层）

- 更新 `.sln` 文件



**验证结果**：

- ✅ Core 项目编译成功（0 AutoCAD 引用）

- ✅ 12 个测试全部通过（秒级，无需 CAD）

- ✅ HELLO 命令在 CAD 中调用成功



---



### ✅ 阶段2：迁移 OpResult 到 Core（已完成）



**目标**：将共享基础类型移入 Core，使所有层统一使用。



**完成内容**：

1. 确认 `Core/Models/OpResult.cs` 的 API 完全兼容 `ServiceACAD/OpResult.cs`

2. 在 `ServiceACAD.csproj` 中添加对 `DDNCadAddins.Core` 的项目引用

3. 将 `ServiceACAD/OpResult.cs` 改为继承 Core 版本的桥接类，保持向后兼容

4. 全解决方案重新编译成功（5 个项目，0 错误）



**验证结果**：

- ✅ ServiceACAD 正确引用 DDNCadAddins.Core

- ✅ OpResult 桥接类保持向后兼容，现有代码无需修改

- ✅ 所有 5 个项目编译成功（0 错误）



---



### ✅ 阶段3：迁移图层服务（第一个真实业务模块）（已完成）



**目标**：将图层管理业务逻辑从 CAD 进程解放出来。



**完成内容**：

1. Core 层新增 `LayerInfo`、`LayerStateSnapshot` POCO 模型

2. 新增 `ILayerRepository` 仓储接口和 `LayerManagementService` 业务服务

3. ServiceACAD 新增 `AutoCadLayerRepository` 适配器和 `LayerStateSnapshotConverter`

4. `TransactionServiceForStyle` 图层状态方法委托至 Core 层（保持向后兼容）

5. `BlockCleanupCommand` 改为直接使用 Core 层服务

6. Core.Tests 新增 `FakeLayerRepository` + 14 个 `LayerManagementServiceTests`



**验证结果**：

- ✅ Core 项目编译成功，无 AutoCAD 引用

- ✅ 全解决方案 5 个项目编译成功（0 错误）

- ✅ BlockCleanup 命令已在 CAD 中手动验证（含 eInvalidLayer 跳过修复）



---



### ✅ 阶段4：迁移块清理服务（已完成）



**目标**：将图块清理的多轮爆炸/删除逻辑移入 Core。



**完成内容**：

1. Core 层新增 `BlockInfo`、`BlockCleanupResult`、`BlockExplodeResult` POCO 模型

2. 新增 `IBlockRepository` 仓储接口和 `BlockCleanupService` 业务服务

3. ServiceACAD 新增 `AutoCadBlockRepository` 适配器（内部调用 `ExplodeAsShown`）

4. `BlockCleanupCommand` 主体逻辑改为调用 Core 层服务（命令层仅负责 I/O）

5. Core.Tests 新增 `FakeBlockRepository` + 7 个 `BlockCleanupServiceTests`

6. `ExplodeAsShown` / `BlockCleanup` 增加逐块统计命令行输出



**验证结果**：

- ✅ Core 项目编译成功，无 AutoCAD 引用

- ✅ 全解决方案编译成功（0 错误）

- ✅ 40 个 Core.Tests 用例（Calculator 12 + LayerManagement 14 + BlockCleanup 7 + StyleValidation 7）

- ⏳ BlockCleanup / ExplodeAsShown 命令需在 CAD 中再次手动验证



**说明**：`ExplodeAsShown` 的 CAD 爆炸实现仍留在 `BlockService`（ServiceACAD），Core 层负责清理编排与统计透传。



---



### ✅ 阶段5：迁移其他模块（已完成）



#### ✅ 5a：颜色/线型验证服务（已完成）



**完成内容**：

1. Core 层新增 `CadStyleConstants` 常量

2. 新增 `IStyleValidationService` / `StyleValidationService`（ACI 颜色索引验证、线型名规范化）

3. `TransactionServiceForStyle.GetValidColorIndex` 与 `GetOrCreateLineType` 委托至 Core 层

4. Core.Tests 新增 7 个 `StyleValidationServiceTests`



#### ✅ 5b：属性处理服务（已完成）

**完成内容**：
1. Core 层新增 `PropertyConversionUtils`（类型转换兼容性判断）
2. Core 层新增 `PropertyComparisonUtils`（属性值相等比较）
3. `PropertyUtils.CanBeConvertedFrom` 与 `BlockService.MatchProp` 委托至 Core 层
4. Core.Tests 新增 9 个属性工具测试



#### ✅ 5c：XClip 边界生成服务（已完成）

**完成内容**：
1. Core 层新增 `Point2D`、`Matrix3D` POCO 模型
2. 新增 `IXClipBoundaryGeometryService` / `XClipBoundaryGeometryService`（局部点扩展、矩阵变换到 WCS）
3. `BlockService.GetXClipBoundaryPointsWcs` 委托至 Core 层
4. Core.Tests 新增 5 个 `XClipBoundaryGeometryServiceTests`



#### 仍留在 ServiceACAD、未纳入 Core 的命令/服务



| 模块 | 说明 |

|------|------|

| `ExplodeAsShown` | 爆炸 + 属性转文字，CAD 密集 |

| `GenerateXclipBoundary` | CAD 读取 SpatialFilter + 写多段线；几何计算已迁 Core |

| `PropertyUtils` | 反射读写仍留 ServiceACAD；比较/转换逻辑已迁 Core |



---



## 技术规范



### Core 层约束（强制）



- 禁止引用任何 `Autodesk.*` 命名空间

- 禁止引用 `ServiceACAD`

- 只允许引用 `System.*` 基础库

- 所有公共方法返回 `DDNCadAddins.Core.Models.OpResult` 或 `OpResult<T>`

- 接口方法必须有 XML 文档注释



### 适配器层约束（ServiceACAD/Adapters）



- 仅负责 CAD 类型 ↔ Core POCO 的转换

- 仅调用 `TransactionService` 系列方法，不直接访问 AutoCAD API

- 禁止包含业务逻辑

- 每个方法必须有完整 try-catch，返回 `OpResult`



### Core.Tests 约束



- 不得引用任何 `Autodesk.*`

- 使用手写 Fake 对象隔离依赖（零额外依赖，.NET Framework 4.7 原生兼容）

- 遵循 Arrange / Act / Assert 模式

- 覆盖：正常路径、边界条件（null/空）、失败路径



---



## 手写 Fake 模式



不引入额外 Mock 框架，所有测试隔离通过手写 Fake 实现：



```csharp

public class FakeLayerRepository : ILayerRepository

{

    public List<LayerInfo> Layers { get; set; } = new List<LayerInfo>();

    public List<LayerInfo> UpdatedLayers { get; } = new List<LayerInfo>();

    public bool ShouldFailGetAll { get; set; }



    public OpResult<IReadOnlyList<LayerInfo>> GetAllLayers()

    {

        if (ShouldFailGetAll)

            return OpResult<IReadOnlyList<LayerInfo>>.Fail("模拟获取失败");

        return OpResult<IReadOnlyList<LayerInfo>>.Success(Layers.AsReadOnly());

    }



    public OpResult UpdateLayer(LayerInfo layer)

    {

        UpdatedLayers.Add(layer);

        return OpResult.Success();

    }

    // ...

}

```



---



## 迁移验证检查清单



每个阶段完成后必须验证：



- [x] Core 项目编译成功，无 AutoCAD 引用（阶段 1–5c）

- [x] Core.Tests 全部通过，无需 CAD 环境（阶段 1–5c，54 用例）

- [ ] 现有 CAD 内 RunTests 测试全部仍通过（无回归）

- [ ] 命令在 AutoCAD 中手动验证功能正确

- [ ] git commit 说明中注明"已通过 [命令名] 手动验证"



---



## 文件变更记录



| 提交 | 内容 |

|------|------|

| `326ccff` | 创建 Core/Core.Tests 项目，HelloWorld PoC |

| `634e330` | 对齐 Directory.props 统一配置 |

| `b97cd13` | 修复 CalculationResult.cs 编译问题 |

| `38dcc77` | 添加架构计划文档 |

| `7977f9b` | 阶段2：OpResult 迁移到 Core |

| `f198642` | 阶段3：图层服务迁移到 Core |

| `40dd0c6` | 阶段4：块清理服务迁移到 Core |

| `87b5142` | ExplodeAsShown/BlockCleanup 逐块统计输出 |

| `4472d1f` | 添加 AC-MALL 2F 示例图纸 |

| 阶段5a | StyleValidationService 迁移，TransactionServiceForStyle 委托 |
| 阶段5b | PropertyConversionUtils / PropertyComparisonUtils 迁移 |
| 阶段5c | XClipBoundaryGeometryService 迁移，BlockService 委托 |
| P0 | CommandCancellationScope、ExecuteInCommandTransaction、逐块实时输出 |



---



## Plan 完成后的产品任务清单



迁移路线图（阶段 1–5）完成后，继续推进以下 CAD 产品化任务：



| 优先级 | 任务 | 说明 | 状态 |
|--------|------|------|------|
| P0 | **命令中断回滚** | 用户 ESC / CANCEL 时中止事务并回滚，不在图面留下半成品实体 | ✅ 已实现（`CommandCancellationScope` + `ExecuteInCommandTransaction` Abort） |
| P0 | **命令行同步输出** | 长耗时命令在处理过程中实时输出进度（非仅事务提交后批量输出） | ✅ 已实现（`ExplodeAsShown` / `BlockCleanup` 逐块 `WriteMessage` + `Update`） |
| P1 | **ExplodeAsShown：XClip 转 Crop** | 对带 XClip 的块参照，爆炸后应保留裁剪效果（Crop），而非忽略 XClip 或仅生成边界 | ⏳ 待实现 |

**关联命令**：`ExplodeAsShown`、`BlockCleanup`（经 `ExplodeAsShown` 间接调用）



**验收建议**：

- 中断回滚：炸开/清理过程中按 ESC，原块仍在、无新增脏实体

- 同步输出：处理第 N 个块时命令行立即出现第 N 行统计

- XClip→Crop：使用 `examples/AC-MALL-P01~P05(平面图) _t3_2F xcliped.dwg` 对比爆炸前后裁剪区域



---



*最后更新：2026-06-13*


