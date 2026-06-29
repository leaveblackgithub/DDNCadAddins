# DDNCadAddins 程序架构

> 版本：1.0.0 | 最后更新：2026-06-29

## 一、解决方案结构总览

```
DDNCadAddins.sln
├── DDNCadAddins.Core          # 纯逻辑核心层（无 AutoCAD 依赖）
├── AddinsACAD                  # AutoCAD 命令层（入口）
└── ServiceACAD                 # AutoCAD 服务层（桥接 Core 与 CAD）
```

### 项目层级关系

```
┌──────────────────────────────────────────────────────────┐
│                    AddinsACAD（命令层）                    │
│  命令注册 / 用户交互 / 测试入口                            │
│  引用 → ServiceACAD                                       │
└──────────────────────────────────────────────────────────┘
                        │ 依赖
                        ▼
┌──────────────────────────────────────────────────────────┐
│              ServiceACAD（AutoCAD 服务层）                 │
│  封装 AutoCAD API / 事务管理 / 块操作 / 裁剪服务          │
│  引用 → DDNCadAddins.Core                                 │
└──────────────────────────────────────────────────────────┘
                        │ 依赖
                        ▼
┌──────────────────────────────────────────────────────────┐
│             DDNCadAddins.Core（纯逻辑核心层）              │
│  纯 C# 逻辑 / 几何算法 / 数据模型 / 接口定义              │
│  无 AutoCAD 引用，可独立 NUnit 测试                       │
└──────────────────────────────────────────────────────────┘
```

### 各层职责

| 层 | 项目 | 包含内容 | AutoCAD 依赖 |
|----|------|---------|-------------|
| **命令层** | `AddinsACAD` | AutoCAD 命令注册、输入获取、输出显示、测试入口、服务测试 | ✅ 是 |
| **AutoCAD 服务层** | `ServiceACAD` | 事务封装、块操作、裁剪服务、CAD API 桥接 | ✅ 是 |
| **纯逻辑核心层** | `DDNCadAddins.Core` | 几何算法、数据模型、操作结果(OpResult)、接口定义 | ❌ 否 |

---

## 二、核心设计模式

### 1. OpResult 返回值模式

所有方法**禁止抛出异常**，统一使用 [`OpResult`](src/DDNCadAddins.Core/Models/OpResult.cs) / [`OpResult<T>`](src/DDNCadAddins.Core/Models/OpResult.cs:6) 返回。

```csharp
// 成功返回数据
return OpResult<ObjectId>.Success(blockId, "块已创建");
// 失败返回消息
return OpResult.Fail("块定义不存在");
// 检查结果
var result = service.DoSomething();
if (!result.IsSuccess)
    return OpResult.Fail(result.Message);
```

### 2. 事务封装模式

所有 AutoCAD 数据库对象的读写**必须**通过 [`TransactionService`](src/ServiceACAD/TransactionService.cs) 的子组件操作，禁止在事务外打开 `DBObject`。

```
TransactionService
├── Entity  → ITransactionServiceForEntity  (实体增删改查)
├── Block   → ITransactionServiceForBlock   (块定义/块参照操作)
├── Style   → ITransactionServiceForStyle   (文字样式/标注样式)
└── (直接方法) → GetObject<T>() / AppendEntityToModelSpace()
```

### 3. 仓储模式（Adapter 模式）

通过 `IBlockRepository` / `ILayerRepository` 接口解耦 AutoCAD 数据库访问：

```
IBlockRepository  ←→  AutoCadBlockRepository
ILayerRepository  ←→  AutoCadLayerRepository
```

### 4. 服务分层（裁剪功能示例）

```
命令层（CropPolylineCommand）
    ↓  获取输入、调用服务、显示结果
AutoCAD 服务层（CropPolylineService : ICropService）
    ↓  调用 AutoCAD API、事务内操作实体
核心层（CropGeometryService）
    ↓  纯几何计算（裁剪线、交点计算）
```

---

## 三、命令注册体系

### AutoCAD 命令注册

所有 AutoCAD 命令通过 `[CommandMethod("命令名")]` 属性注册，命令名**大小写不敏感**。

#### 生产命令（用户可用）

| 命令名 | 文件 | 功能 | 类别 |
|--------|------|------|------|
| [`BlockCleanup`](src/AddinsACAD/Commands/BlockCleanupCommand.cs:22) | [`BlockCleanupCommand.cs`](src/AddinsACAD/Commands/BlockCleanupCommand.cs) | 块清理 | 块操作 |
| [`CROPINSIDE`](src/AddinsACAD/Commands/CropInsideCommand.cs:24) | [`CropInsideCommand.cs`](src/AddinsACAD/Commands/CropInsideCommand.cs) | 边界内裁剪 | 裁剪 |
| [`CROPOUTSIDE`](src/AddinsACAD/Commands/CropInsideCommand.cs:33) | [`CropInsideCommand.cs`](src/AddinsACAD/Commands/CropInsideCommand.cs) | 边界外裁剪 | 裁剪 |
| [`CROPARC`](src/AddinsACAD/Commands/CropArcCommand.cs:23) | [`CropArcCommand.cs`](src/AddinsACAD/Commands/CropArcCommand.cs) | 裁剪弧 | 裁剪 |
| [`CROPALLARCS`](src/AddinsACAD/Commands/CropArcCommand.cs:29) | [`CropArcCommand.cs`](src/AddinsACAD/Commands/CropArcCommand.cs) | 裁剪全部弧 | 裁剪 |
| [`CROPCIRCLE`](src/AddinsACAD/Commands/CropCircleCommand.cs:22) | [`CropCircleCommand.cs`](src/AddinsACAD/Commands/CropCircleCommand.cs) | 裁剪圆 | 裁剪 |
| [`CROPALLCIRCLES`](src/AddinsACAD/Commands/CropCircleCommand.cs:28) | [`CropCircleCommand.cs`](src/AddinsACAD/Commands/CropCircleCommand.cs) | 裁剪全部圆 | 裁剪 |
| [`CROPLINE`](src/AddinsACAD/Commands/CropLineCommand.cs:26) | [`CropLineCommand.cs`](src/AddinsACAD/Commands/CropLineCommand.cs) | 裁剪线 | 裁剪 |
| [`CROPALLLINES`](src/AddinsACAD/Commands/CropLineCommand.cs:35) | [`CropLineCommand.cs`](src/AddinsACAD/Commands/CropLineCommand.cs) | 裁剪全部线 | 裁剪 |
| [`CROPPOLYLINE`](src/AddinsACAD/Commands/CropPolylineCommand.cs:26) | [`CropPolylineCommand.cs`](src/AddinsACAD/Commands/CropPolylineCommand.cs) | 裁剪多段线 | 裁剪 |
| [`CROPALLPOLYLINES`](src/AddinsACAD/Commands/CropPolylineCommand.cs:35) | [`CropPolylineCommand.cs`](src/AddinsACAD/Commands/CropPolylineCommand.cs) | 裁剪全部多段线 | 裁剪 |
| [`ExplodeAsShown`](src/AddinsACAD/Commands/ExplodeAsShownCommand.cs:18) | [`ExplodeAsShownCommand.cs`](src/AddinsACAD/Commands/ExplodeAsShownCommand.cs) | 按显示状态爆炸 | 块操作 |
| [`GenerateXclipBoundary`](src/AddinsACAD/Commands/GenerateXclipBoundaryCommand.cs:20) | [`GenerateXclipBoundaryCommand.cs`](src/AddinsACAD/Commands/GenerateXclipBoundaryCommand.cs) | 生成 XClip 边界 | 块操作 |
| [`HELLO`](src/AddinsACAD/Commands/HelloWorldCommand.cs:19) | [`HelloWorldCommand.cs`](src/AddinsACAD/Commands/HelloWorldCommand.cs) | 测试命令 | 工具 |

#### 测试命令

| 命令名 | 文件 | 功能 |
|--------|------|------|
| [`MANUALCMDTESTS`](src/AddinsACAD/Commands/CommandTestsCommand.cs:19) | [`CommandTestsCommand.cs`](src/AddinsACAD/Commands/CommandTestsCommand.cs) | 手工测试命令集合 |
| [`AUTOCMDTESTS`](src/AddinsACAD/TestCommands/AutoCADTestsCommand.cs) | [`AutoCADTestsCommand.cs`](src/AddinsACAD/TestCommands/AutoCADTestsCommand.cs) | CAD 环境自动测试 |

#### MANUALCMDTESTS 子命令注册表

`MANUALCMDTESTS` 通过 [`CommandTestsCommand.cs`](src/AddinsACAD/Commands/CommandTestsCommand.cs) 的 `[CommandMethod("MANUALCMDTESTS")]` 注册，内部通过 [`AskSubCommand`](src/AddinsACAD/Commands/CommandTestsCommand.cs:56) 选择子命令，通过 [`MapToCommand`](src/AddinsACAD/Commands/CommandTestsCommand.cs:79) 映射到具体命令。

**当前有效子命令：**

| 快捷键 | 命令名 | 映射到的命令方法 | 功能说明 |
|--------|--------|-----------------|---------|
| `C` | `CROPTESTS` | — | 裁剪测试集合（调用 CROPINSIDE/CROPOUTSIDE 等） |
| `K` | `CLONEHATCH` | [`CLONEHATCH`](src/AddinsACAD/Commands/CloneHatchCommand.cs:39) | 克隆填充 |
| `G` | `GENERATEXCLIPBOUNDARY` | [`GenerateXclipBoundary`](src/AddinsACAD/Commands/GenerateXclipBoundaryCommand.cs:20) | 生成 XClip 边界 |
| `E` | `EXPLODEASSHOWN` | [`ExplodeAsShown`](src/AddinsACAD/Commands/ExplodeAsShownCommand.cs:18) | 按显示状态爆炸图块 |
| `H` | `GENERATEHATCHBOUNDARY` | [`GENERATEHATCHBOUNDARY`](src/AddinsACAD/Commands/GenerateHatchBoundaryCommand.cs:18) | 提取 Hatch 边界 |
| `B` | `SUBTRACTCLOSEDCURVE` | [`SUBTRACTCLOSEDCURVE`](src/AddinsACAD/Commands/SubtractClosedCurveCommand.cs:29) | 封闭曲线布尔交集 |

**关于命令名大小写：** AutoCAD 通过 `[CommandMethod]` 注册的命令名**大小写不敏感**。因此即使映射到 `"ExplodeAsShown"`（驼峰），用户在 AutoCAD 命令行输入 `EXPLODEASSHOWN` 也能正确执行。

---

## 四、裁剪服务架构

裁剪功能支持多种实体类型，每种类型有对应的 AutoCAD 服务实现：

```
ICropService（裁剪服务接口）
├── CropArcService       - 弧裁剪
├── CropCircleService    - 圆裁剪
├── CropLineService      - 线裁剪
├── CropPolylineService  - 多段线裁剪
├── CropSplineService    - 样条曲线裁剪
├── CropEllipseService   - 椭圆裁剪
├── Crop3DPolylineService - 3D 多段线裁剪
├── CropMLineService     - 多线裁剪
├── CropLeaderService    - 引线裁剪
├── CropHatchService     - 填充裁剪
├── CropBlockService     - 块参照裁剪
├── CropTextService      - 文字裁剪
├── CropMTextService     - 多行文字裁剪
├── CropDimService       - 标注裁剪
├── CropPointService     - 点裁剪
├── CropSolidService     - 实体裁剪
└── CropService          - 主裁剪服务（调度器）
```

核心逻辑层 (`DDNCadAddins.Core`) 提供纯几何计算：
- [`CropGeometryService`](src/DDNCadAddins.Core/Services/CropGeometryService.cs) - 裁剪几何计算
- [`PolygonClipperService`](src/DDNCadAddins.Core/Services/PolygonClipperService.cs) - 多边形裁剪
- [`CurveSampler`](src/DDNCadAddins.Core/Services/CurveSampler.cs) - 曲线采样

---

## 五、测试体系

### 测试优先级

```
第一优先级（运行最快）
    ↓  NUnit Console Runner（纯逻辑测试）
    ↓  DDNCadAddins.Core.Tests
第二优先级（需 AutoCAD 进程）
    ↓  AUTOCMDTESTS 命令（内存侧数据库）
    ↓  AddinsACAD.ServiceTests 目录
第三优先级（需真实图纸，仅手工执行）
    ↓  MANUALCMDTESTS 命令
    ↓  返回 TestRecords JSON 文件用于复盘
```

### 测试项目文件

```bash
src/
├── DDNCadAddins.Core.Tests/        # 纯逻辑 NUnit 测试
│   ├── CalculatorServiceTests.cs
│   ├── CropGeometryServiceTests.cs
│   ├── PolygonClipperServiceTests.cs
│   ├── PropertyComparisonUtilsTests.cs
│   ├── ...（更多测试文件）
│   └── packages.config
└── AddinsACAD/ServiceTests/         # CAD 环境服务测试
    ├── BlockServiceExtendedTests.cs
    ├── BlockServiceTestUtils.cs
    ├── CommonTestMethods.cs
    ├── CropServiceTestBase.cs
    ├── CropArcServiceTests.cs
    ├── CropCircleServiceTests.cs
    ├── CropLineServiceTests.cs
    ├── CropPolylineServiceTests.cs
    └── TransactionServiceTest.cs
```

---

## 六、关键约定与限制

### AutoCAD 访问规则

1. **所有数据库操作必须在事务内完成**，使用 `TransactionService` 的 `GetObject<T>()` 方法
2. **禁止直接使用 `Application.DocumentManager`**，必须通过 `EditorService` / `DocumentService` 间接访问
3. **禁止使用 `dynamic` 类型** 访问 AutoCAD 对象，必须使用强类型
4. **禁止在事务外** 打开或修改 `DBObject`

### 异常安全规则

1. 所有方法必须用 `try-catch(Exception)` 包裹
2. **禁止 `throw` 传播异常**，使用 `OpResult` / `OpResult<T>` 返回
3. catch 块必须调用 `Logger._.Error` 记录异常
4. `void` 方法需改为 `OpResult` 返回类型

### 命令结构规则

1. 命令必须分为三部分：**输入获取 → 主体逻辑 → 输出显示**
2. 输入获取和输出显示在命令类中，主体逻辑在服务类中
3. 所有命令必须支持 `CANCEL` 操作
4. 非命令模块不应直接进行输入/输出操作

### MANUALCMDTESTS 维护规则

1. 子命令的快捷键（C/K/G/E/H/B）**不可复用**，每个命令独占一个字母
2. 映射到 `MapToCommand()` 的命令名**无需与 CommandMethod 名称完全一致**（AutoCAD 大小写不敏感）
3. 删除子命令时，同时清理 `AskSubCommand()` 中的关键字和 `MapToCommand()` 中的映射
4. 新增子命令时，必须确保目标 `[CommandMethod]` 已注册

---

## 七、关键文件索引

### 解决方案配置

| 文件 | 说明 |
|------|------|
| [`DDNCadAddins.sln`](src/DDNCadAddins.sln) | 解决方案文件 |
| [`Directory.props`](src/Directory.props) | 共享编译属性 |
| [`CommonAssemblyInfo.cs`](src/CommonAssemblyInfo.cs) | 共享版本号信息 |

### 接口定义

| 文件 | 说明 |
|------|------|
| [`ITransactionService.cs`](src/ServiceACAD/ITransactionService.cs) | 事务服务主接口 |
| [`ITransactionServiceForEntity.cs`](src/ServiceACAD/ITransactionServiceForEntity.cs) | 实体子服务接口 |
| [`ITransactionServiceForBlock.cs`](src/ServiceACAD/ITransactionServiceForBlock.cs) | 块子服务接口 |
| [`ITransactionServiceForStyle.cs`](src/ServiceACAD/ITransactionServiceForStyle.cs) | 样式子服务接口 |
| [`ICropService.cs`](src/ServiceACAD/ICropService.cs) | 裁剪服务接口 |
| [`IBlockService.cs`](src/ServiceACAD/IBlockService.cs) | 块服务接口 |
| [`IBlockRepository.cs`](src/DDNCadAddins.Core/Interfaces/IBlockRepository.cs) | 块仓储接口（纯逻辑） |
| [`ILayerRepository.cs`](src/DDNCadAddins.Core/Interfaces/ILayerRepository.cs) | 图层仓储接口（纯逻辑） |

### AutoCAD 服务实现

| 文件 | 说明 |
|------|------|
| [`TransactionService.cs`](src/ServiceACAD/TransactionService.cs) | 事务服务实现 |
| [`CadServiceManager.cs`](src/ServiceACAD/CadServiceManager.cs) | 文档服务管理器（单例） |
| [`EditorService.cs`](src/ServiceACAD/EditorService.cs) | 编辑器服务 |
| [`DocumentService.cs`](src/ServiceACAD/DocumentService.cs) | 文档服务 |
| [`BlockService.cs`](src/ServiceACAD/BlockService.cs) | 块服务 |
| [`BlockExploder.cs`](src/ServiceACAD/BlockExploder.cs) | 块爆炸器 |
| [`HatchBoundaryExtractor.cs`](src/ServiceACAD/HatchBoundaryExtractor.cs) | Hatch 边界提取 |

### 工具类

| 文件 | 说明 |
|------|------|
| [`GeometryHelper.cs`](src/ServiceACAD/GeometryHelper.cs) | CAD 几何帮助类 |
| [`CropUtils.cs`](src/ServiceACAD/CropUtils.cs) | 裁剪工具类 |
| [`CurveConverter.cs`](src/ServiceACAD/CurveConverter.cs) | 曲线转换 |
| [`TestRecorder.cs`](src/ServiceACAD/TestRecorder.cs) | 测试记录器 |
| [`CommandCancellationScope.cs`](src/ServiceACAD/CommandCancellationScope.cs) | 命令取消范围 |