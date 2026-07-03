# CROPHATCH 命令设计方案

> 版本：1.0 | 日期：2026-07-03 | 模式：🏗️ Architect

---

## 一、需求概述

新增 CAD 命令 `CROPHATCH`，实现"填充裁剪"功能：用户选择一个 Hatch 和一个裁剪边界（闭合曲线），将 Hatch 的填充区域用裁剪边界进行布尔差集运算，生成裁剪后的新 Hatch 并删除原图。

**核心约束：严格调用已有的 `GENERATEHATCHBOUNDARY`、`SUBTRACTCLOSEDCURVE`、`CLONEHATCH` 三个功能完成，禁止另外造轮子。**

同时在 `MANUALCMDTESTS` 的 `CROPTESTS` 子项中新增一个测试入口。

---

## 二、三个已有功能分析

### 2.1 GENERATEHATCHBOUNDARY — Hatch 边界提取

[`GenerateHatchBoundaryCommand`](src/AddinsACAD/Commands/GenerateHatchBoundaryCommand.cs:17) 的核心逻辑：

1. 用户选择一个 Hatch
2. 在事务中打开 Hatch，根据 `HatchStyle` 确定要处理的环范围
3. 使用 [`CurveToPolygonConverter.CreateEntityFromLoop()`](src/ServiceACAD/CurveToPolygonConverter.cs:126) 从每个 `HatchLoop` 生成 CAD 实体（Polyline / Circle / Ellipse）
4. 通过 TestRecorder 记录

**可复用点：**
- [`CurveToPolygonConverter.CreateEntityFromLoop()`](src/ServiceACAD/CurveToPolygonConverter.cs:126) — 从 HatchLoop 生成 CAD 曲线实体（ObjectId）
- [`HatchBoundaryExtractor.ExtractBoundaries()`](src/ServiceACAD/HatchBoundaryExtractor.cs:41) — 提取 Hatch 所有边界环为多边形列表

### 2.2 SUBTRACTCLOSEDCURVE — 封闭曲线布尔差集

[`SubtractClosedCurveCommand`](src/AddinsACAD/Commands/SubtractClosedCurveCommand.cs:35) 的核心逻辑：

1. 选择曲线 A 和曲线 B
2. 用 [`CurveToExactSegmentConverter.ConvertToExactSegments()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:29) 将曲线转换为精确段
3. 用 [`CurveToExactSegmentConverter.ConvertToCropBoundary()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:59) 转换为裁剪边界
4. 调用 [`CurveSubtractService.Subtract()`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs:34) 计算差集 A \ B
5. 用 [`CurveToExactSegmentConverter.DrawExactSegments()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:274) 绘制结果

**可复用点：**
- [`CurveToExactSegmentConverter.ConvertToExactSegments()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:29) — 曲线→精确段
- [`CurveToExactSegmentConverter.ConvertToCropBoundary()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:59) — 曲线→裁剪边界
- [`CurveSubtractService.Subtract()`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs:34) — 纯逻辑差集计算
- [`CurveToExactSegmentConverter.DrawExactSegments()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:274) — 精确段→Polyline 实体

### 2.3 CLONEHATCH — Hatch 参数克隆填充

[`CloneHatchCommand`](src/AddinsACAD/Commands/CloneHatchCommand.cs:39) 的核心逻辑：

1. 选择源 Hatch，提取填充参数（`HatchParams` 结构体：PatternType / PatternName / Scale / Angle / Origin / Style / Normal / Elevation）
2. 选择新边界对象（ObjectId[]）
3. 调用 [`CloneHatchWithNewBoundaries()`](src/AddinsACAD/Commands/CloneHatchCommand.cs:209) 创建新 Hatch：
   - 用源参数初始化新 Hatch
   - 用 `AppendLoop(HatchLoopTypes.Outermost, ObjectIdCollection)` 追加边界
   - 设置 `Associative = true`
   - `EvaluateHatch(true)` 评估填充

**可复用点：**
- `HatchParams` 提取逻辑（Step 2 的只读事务部分）
- [`CloneHatchWithNewBoundaries()`](src/AddinsACAD/Commands/CloneHatchCommand.cs:209) — 用源参数 + 新边界创建 Hatch

---

## 三、CROPHATCH 命令设计

### 3.1 用户交互流程

```
1. 选择源 Hatch（PromptEntityOptions，限制 Hatch 类型）
2. 选择裁剪边界曲线（PromptEntityOptions，限制闭合 Curve）
3. [自动执行] 提取 Hatch 边界 → 差集运算 → 克隆填充
4. 输出结果 + TestRecorder 记录
```

### 3.2 命令三段式结构（遵守命令结构规则）

| 阶段 | 所在层 | 职责 |
|------|--------|------|
| **输入获取** | [`CropHatchCommand`](src/AddinsACAD/Commands/CropHatchCommand.cs) | 选择 Hatch、选择边界曲线 |
| **主体逻辑** | [`CropHatchService`](src/ServiceACAD/CropHatchService.cs) | 提取边界 → 差集 → 克隆填充 |
| **输出显示** | [`CropHatchCommand`](src/AddinsACAD/Commands/CropHatchCommand.cs) | 命令行提示 + TestRecorder |

### 3.3 主体逻辑流程（严格复用已有功能）

```
步骤 1: 提取 Hatch 边界（复用 GENERATEHATCHBOUNDARY 的核心方法）
  ├─ 打开 Hatch，确定环范围（HatchStyle）
  ├─ 对每个 HatchLoop:
  │   └─ CurveToPolygonConverter.CreateEntityFromLoop() → 生成 CAD 曲线实体 (ObjectId)
  └─ 收集所有生成的边界曲线 ObjectId 列表 = hatchBoundaryIds

步骤 2: 对每条 Hatch 边界曲线执行差集（复用 SUBTRACTCLOSEDCURVE 的核心方法）
  ├─ 将裁剪边界曲线转换为:
  │   ├─ CurveToExactSegmentConverter.ConvertToExactSegments() → 精确段
  │   └─ CurveToExactSegmentConverter.ConvertToCropBoundary() → 裁剪边界
  ├─ 对每条 hatchBoundaryId:
  │   ├─ 打开曲线，ConvertToExactSegments() → 精确段A
  │   ├─ ConvertToCropBoundary() → 边界A
  │   ├─ CurveSubtractService.Subtract(A段, A边界, B段, B边界) → 差集结果
  │   └─ CurveToExactSegmentConverter.DrawExactSegments() → 绘制差集结果曲线
  └─ 收集所有差集结果曲线 ObjectId 列表 = croppedBoundaryIds

步骤 3: 用差集结果曲线克隆 Hatch（复用 CLONEHATCH 的核心方法）
  ├─ 提取源 Hatch 的填充参数 (HatchParams)
  ├─ 调用 CloneHatchWithNewBoundaries(ts, sourceId, params, croppedBoundaryIds, ed, out newHatchId)
  └─ 删除原 Hatch (可选，询问用户或默认删除)

步骤 4: 清理临时实体
  └─ 删除步骤1生成的临时边界曲线（hatchBoundaryIds）
```

### 3.4 复用映射表

| CROPHATCH 步骤 | 复用的已有功能 | 来源文件 | 复用方式 |
|----------------|---------------|----------|---------|
| 提取 Hatch 边界为曲线 | [`CreateEntityFromLoop()`](src/ServiceACAD/CurveToPolygonConverter.cs:126) | [`CurveToPolygonConverter.cs`](src/ServiceACAD/CurveToPolygonConverter.cs) | 直接调用 |
| HatchStyle 环范围确定 | [`GenerateHatchBoundaryCommand`](src/AddinsACAD/Commands/GenerateHatchBoundaryCommand.cs:44) 中的逻辑 | [`GenerateHatchBoundaryCommand.cs`](src/AddinsACAD/Commands/GenerateHatchBoundaryCommand.cs) | 提取为 Service 方法 |
| 曲线→精确段 | [`ConvertToExactSegments()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:29) | [`CurveToExactSegmentConverter.cs`](src/ServiceACAD/CurveToExactSegmentConverter.cs) | 直接调用 |
| 曲线→裁剪边界 | [`ConvertToCropBoundary()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:59) | [`CurveToExactSegmentConverter.cs`](src/ServiceACAD/CurveToExactSegmentConverter.cs) | 直接调用 |
| 差集计算 | [`Subtract()`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs:34) | [`CurveSubtractService.cs`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs) | 直接调用 |
| 绘制差集结果 | [`DrawExactSegments()`](src/ServiceACAD/CurveToExactSegmentConverter.cs:274) | [`CurveToExactSegmentConverter.cs`](src/ServiceACAD/CurveToExactSegmentConverter.cs) | 直接调用 |
| 提取 Hatch 参数 | [`CloneHatchCommand`](src/AddinsACAD/Commands/CloneHatchCommand.cs:79) 的 HatchParams 提取 | [`CloneHatchCommand.cs`](src/AddinsACAD/Commands/CloneHatchCommand.cs) | 提取为 Service 方法 |
| 克隆填充 | [`CloneHatchWithNewBoundaries()`](src/AddinsACAD/Commands/CloneHatchCommand.cs:209) | [`CloneHatchCommand.cs`](src/AddinsACAD/Commands/CloneHatchCommand.cs) | 提取为 Service 方法 |

---

## 四、分层设计

### 4.1 文件清单

| 层 | 文件 | 类型 | 说明 |
|----|------|------|------|
| 命令层 | [`CropHatchCommand.cs`](src/AddinsACAD/Commands/CropHatchCommand.cs) | 新增 | CROPHATCH 命令：输入获取 + 输出显示 |
| 服务层 | [`CropHatchService.cs`](src/ServiceACAD/CropHatchService.cs) | 修改 | 新增 `CropHatchWithBoundary()` 方法，编排三个已有功能 |
| 服务层 | [`HatchParamExtractor.cs`](src/ServiceACAD/HatchParamExtractor.cs) | 新增 | 从 CLONEHATCH 提取的 Hatch 参数提取工具 |
| 命令层 | [`CommandTestsCommand.cs`](src/AddinsACAD/Commands/CommandTestsCommand.cs) | 修改 | 新增 `X=CROPHATCH` 子命令快捷键 |

### 4.2 服务层设计 — [`CropHatchService`](src/ServiceACAD/CropHatchService.cs)

新增方法签名（遵守 OpResult 约束）：

```csharp
/// <summary>
///     用裁剪边界对 Hatch 进行差集裁剪，生成新 Hatch.
///     严格复用 GENERATEHATCHBOUNDARY + SUBTRACTCLOSEDCURVE + CLONEHATCH 的核心方法.
/// </summary>
/// <param name="hatchId">源 Hatch 的 ObjectId.</param>
/// <param name="boundaryCurveId">裁剪边界曲线的 ObjectId.</param>
/// <param name="ts">事务服务.</param>
/// <param name="ed">编辑器（用于输出提示）.</param>
/// <param name="newHatchId">[out] 新创建的 Hatch 的 ObjectId.</param>
/// <param name="deleteOriginal">是否删除原 Hatch.</param>
/// <returns>操作结果.</returns>
public OpResult<CropHatchWithBoundaryResult> CropHatchWithBoundary(
    ObjectId hatchId,
    ObjectId boundaryCurveId,
    ITransactionService ts,
    Editor ed,
    out ObjectId newHatchId,
    bool deleteOriginal = true)
```

**返回类型：**

```csharp
public class CropHatchWithBoundaryResult
{
    public int ExtractedBoundaryCount;   // 步骤1提取的边界数量
    public int CroppedBoundaryCount;     // 步骤2生成的差集边界数量
    public bool HatchCreated;            // 步骤3是否成功创建 Hatch
    public string Message;               // 结果消息
}
```

### 4.3 服务层设计 — [`HatchParamExtractor`](src/ServiceACAD/HatchParamExtractor.cs)

从 [`CloneHatchCommand`](src/AddinsACAD/Commands/CloneHatchCommand.cs) 提取的可复用工具：

```csharp
/// <summary>
///     Hatch 填充参数提取器 — 从 CLONEHATCH 命令提取的可复用工具.
/// </summary>
public class HatchParamExtractor
{
    public struct HatchParams
    {
        public HatchPatternType PatternType;
        public string PatternName;
        public double PatternScale;
        public double PatternAngle;
        public bool PatternDouble;
        public double PatternSpace;
        public Point2d Origin;
        public HatchStyle Style;
        public Vector3d Normal;
        public double Elevation;
    }

    /// <summary>从 Hatch 提取填充参数.</summary>
    public OpResult<HatchParams> Extract(Hatch hatch);

    /// <summary>
    ///     用源参数 + 新边界创建 Hatch（从 CloneHatchCommand.CloneHatchWithNewBoundaries 提取）.
    /// </summary>
    public OpResult<ObjectId> CreateHatchWithParams(
        ITransactionService ts, HatchParams p,
        ObjectId[] boundaryIds, Editor ed);
}
```

### 4.4 命令层设计 — [`CropHatchCommand`](src/AddinsACAD/Commands/CropHatchCommand.cs)

```csharp
[CommandMethod("CROPHATCH")]
public void Execute()
{
    // ── 输入获取 ──
    // 1. 选择源 Hatch
    // 2. 选择裁剪边界曲线
    // 3. 询问是否删除原 Hatch

    // ── 主体逻辑（调用 CropHatchService）──
    // CadServiceManager._.ExecuteInCommandTransaction(ts => {
    //     cropHatchService.CropHatchWithBoundary(...)
    // });

    // ── 输出显示 ──
    // TestRecorder.Record(...)
    // ed.WriteMessage(...)
}
```

### 4.5 MANUALCMDTESTS 子命令注册

在 [`CommandTestsCommand.cs`](src/AddinsACAD/Commands/CommandTestsCommand.cs) 中新增快捷键 `X`：

| 快捷键 | 命令名 | 功能 |
|--------|--------|------|
| `X` | `CROPHATCH` | 填充裁剪 |

修改点：
- [`AskSubCommand()`](src/AddinsACAD/Commands/CommandTestsCommand.cs:56) — 添加 `X` 关键字
- [`MapToCommand()`](src/AddinsACAD/Commands/CommandTestsCommand.cs:79) — 添加 `case "X": return "CROPHATCH"`
- 提示文本更新为包含 `X=CROPHATCH`

---

## 五、类行数与 SOLID 检查

### 5.1 行数估算

| 类 | 估算行数 | 限制 | 状态 |
|----|---------|------|------|
| [`CropHatchCommand`](src/AddinsACAD/Commands/CropHatchCommand.cs) | ~100 行 | < 200 | ✅ |
| [`CropHatchService`](src/ServiceACAD/CropHatchService.cs) (新增方法) | ~80 行 | < 200 | ✅ |
| [`HatchParamExtractor`](src/ServiceACAD/HatchParamExtractor.cs) | ~100 行 | < 200 | ✅ |

### 5.2 SOLID 检查

| 原则 | 检查 |
|------|------|
| **SRP** | CropHatchCommand 只做输入/输出；CropHatchService 只做编排；HatchParamExtractor 只做参数提取 |
| **OCP** | 通过组合已有工具类扩展，不修改已有命令逻辑 |
| **LSP** | 无继承关系，不适用 |
| **ISP** | CropHatchService 方法数 ≤ 7 |
| **DIP** | CropHatchService 依赖 ITransactionService 抽象，不依赖具体实现 |

### 5.3 异常安全

所有方法返回 `OpResult` 或 `OpResult<T>`，禁止 `throw`，`catch` 块调用 `Logger._.Error`。

---

## 六、ARCHITECTURE.md 更新计划

### 6.1 命令注册体系更新

生产命令表新增：

| 命令名 | 文件 | 功能 | 类别 |
|--------|------|------|------|
| [`CROPHATCH`](src/AddinsACAD/Commands/CropHatchCommand.cs) | [`CropHatchCommand.cs`](src/AddinsACAD/Commands/CropHatchCommand.cs) | 填充裁剪 | 填充 |

MANUALCMDTESTS 子命令表新增：

| 快捷键 | 命令名 | 映射到的命令方法 | 功能说明 |
|--------|--------|-----------------|---------|
| `X` | `CROPHATCH` | [`CROPHATCH`](src/AddinsACAD/Commands/CropHatchCommand.cs) | 填充裁剪 |

### 6.2 关键文件索引更新

新增：

| 文件 | 说明 |
|------|------|
| [`HatchParamExtractor.cs`](src/ServiceACAD/HatchParamExtractor.cs) | Hatch 参数提取工具（从 CLONEHATCH 提取复用） |

### 6.3 版本号递增

版本 1.0.0 → 1.1.0，日期更新为 2026-07-03。

---

## 七、测试计划

### 7.1 第一优先级（纯逻辑测试）

`CropHatchWithBoundary` 的差集部分依赖纯逻辑层的 [`CurveSubtractService`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs)，已有 [`SubtractIntersectionTests.cs`](src/DDNCadAddins.Core.Tests/SubtractIntersectionTests.cs) 覆盖。无需新增纯逻辑测试。

### 7.2 第二优先级（CAD 环境自动测试）

在 [`AddinsACAD/ServiceTests/`](src/AddinsACAD/ServiceTests/) 目录下新增 `CropHatchServiceTests.cs`，测试内容：
- 创建临时 Hatch + 裁剪边界，调用 `CropHatchWithBoundary()`，验证返回 `IsSuccess`
- 验证 `newHatchId` 不为空且新 Hatch 存在于模型空间
- 验证原 Hatch 是否按 `deleteOriginal` 参数被删除/保留
- 验证临时边界实体已被清理

### 7.3 第三优先级（CAD 手工测试）

通过 `MANUALCMDTESTS → X → CROPHATCH` 执行：
- 使用 [`examples/hatch boundary not closed.dwg`](examples/hatch%20boundary%20not%20closed.dwg) 测试图纸
- 验证 TestRecorder 生成的 JSON 记录包含正确的 `Command = "CROPHATCH"`、`Direction`、`Entities` 快照

---

## 八、CROPTESTS 子项集成

### 8.1 当前 CROPTESTS 结构

[`CROPTESTS`](src/AddinsACAD/Commands/CropTestsCommand.cs:27) 是 [`MANUALCMDTESTS`](src/AddinsACAD/Commands/CommandTestsCommand.cs:20) 的子项之一（快捷键 `C`），本身是一个独立的 `[CommandMethod]` 命令，通过 `SendStringToExecute` 排入 AutoCAD 队列执行。

### 8.2 集成方案

CROPHATCH 作为 `MANUALCMDTESTS` 的**独立子项**（快捷键 `X`），与 CROPTESTS 并列，而非嵌入 CROPTESTS 内部。原因：

1. CROPHATCH 有独立的交互流程（选 Hatch + 选边界），不适合 CROPTESTS 的"选一个边界批量裁剪"模式
2. 符合 [`CommandTestsCommand`](src/AddinsACAD/Commands/CommandTestsCommand.cs) 的设计模式 — 每个子命令独立执行

### 8.3 修改清单

[`CommandTestsCommand.cs`](src/AddinsACAD/Commands/CommandTestsCommand.cs) 修改：

```csharp
// AskSubCommand() — 提示文本和关键字
var kw = new PromptKeywordOptions(
    "\n选择测试子命令 C=CROPTESTS K=CLONEHATCH G=GENERATEXCLIPBOUNDARY " +
    "E=EXPLODEASSHOWN H=GENERATEHATCHBOUNDARY B=SUBTRACTCLOSEDCURVE " +
    "X=CROPHATCH [C/K/G/E/H/B/X]");
kw.Keywords.Add("C");
kw.Keywords.Add("K");
kw.Keywords.Add("G");
kw.Keywords.Add("E");
kw.Keywords.Add("H");
kw.Keywords.Add("B");
kw.Keywords.Add("X");  // ← 新增

// MapToCommand() — 映射
case "X": return "CROPHATCH";  // ← 新增
```

---

## 九、数据流图

```
用户选择 Hatch + 边界曲线
         │
         ▼
┌─────────────────────────────────────────────────────┐
│  步骤1: 提取 Hatch 边界（复用 GENERATEHATCHBOUNDARY）  │
│  Hatch → HatchLoop[] → CreateEntityFromLoop()         │
│  → List<ObjectId> hatchBoundaryIds（临时曲线实体）     │
└─────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────┐
│  步骤2: 差集运算（复用 SUBTRACTCLOSEDCURVE）            │
│  对每条 hatchBoundaryId:                               │
│    ConvertToExactSegments() → ExactSegment[] A        │
│    ConvertToCropBoundary()  → ICropBoundary A         │
│  边界曲线:                                              │
│    ConvertToExactSegments() → ExactSegment[] B        │
│    ConvertToCropBoundary()  → ICropBoundary B         │
│  CurveSubtractService.Subtract(A, B) → ExactSubtractResult │
│  DrawExactSegments() → List<ObjectId> croppedBoundaryIds │
└─────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────┐
│  步骤3: 克隆填充（复用 CLONEHATCH）                     │
│  HatchParamExtractor.Extract(sourceHatch) → HatchParams │
│  HatchParamExtractor.CreateHatchWithParams(           │
│    params, croppedBoundaryIds) → ObjectId newHatchId  │
└─────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────┐
│  步骤4: 清理                                           │
│  删除临时边界实体 (hatchBoundaryIds)                    │
│  删除原 Hatch (if deleteOriginal)                      │
│  TestRecorder.Record()                                │
└─────────────────────────────────────────────────────┘
```

---

## 十、实现顺序建议

| 步骤 | 文件 | 说明 |
|------|------|------|
| 1 | [`HatchParamExtractor.cs`](src/ServiceACAD/HatchParamExtractor.cs) | 新建，从 CLONEHATCH 提取 `HatchParams` 结构和 `Extract()` / `CreateHatchWithParams()` 方法 |
| 2 | [`CropHatchService.cs`](src/ServiceACAD/CropHatchService.cs) | 新增 `CropHatchWithBoundary()` 方法，编排三步流程 |
| 3 | [`CropHatchCommand.cs`](src/AddinsACAD/Commands/CropHatchCommand.cs) | 新建命令类，输入获取 + 调用 Service + 输出显示 |
| 4 | [`CommandTestsCommand.cs`](src/AddinsACAD/Commands/CommandTestsCommand.cs) | 添加 `X=CROPHATCH` 子命令快捷键 |
| 5 | [`ARCHITECTURE.md`](docs/ARCHITECTURE.md) | 更新命令注册表、子命令表、文件索引、版本号 |
| 6 | [`CropHatchServiceTests.cs`](src/AddinsACAD/ServiceTests/CropHatchServiceTests.cs) | 新建 CAD 环境自动测试 |