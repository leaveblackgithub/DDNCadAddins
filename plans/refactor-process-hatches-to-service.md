# ProcessHatches 下沉到 ServiceACAD 层 — 架构设计

> 版本：1.0.0 | 日期：2026-07-06 | 状态：✅ 已完成

---

## 一、现状分析

### 1.1 当前架构问题

[`ProcessHatches`](src/AddinsACAD/Commands/CropHatchCommand.cs:185) 位于 AddinsACAD 命令层，但其本质是服务逻辑（不包含任何 UI 交互），存在以下违规：

| 问题 | 说明 |
|------|------|
| **层级违规** | 服务逻辑（Hatch 裁剪+重建）放在命令层，违反三层架构 |
| **Editor 依赖** | 参数 `Editor ed` 仅用于 `ed.WriteMessage` 日志输出，阻止服务层调用 |
| **跨命令依赖** | `CropInsideCommand` 直接调用 `CropHatchCommand.ProcessHatches()`，命令类之间不应相互依赖 |
| **静态方法位置错误** | 依赖的 `GenerateHatchBoundary`、`CropClosedCurveMulti`、`ExtractHatchParams`、`CloneHatchWithNewBoundaries` 均标注"核心方法，可被其他命令或服务调用"但位于 AddinsACAD |

### 1.2 当前调用链

```
CropHatchCommand.ExecuteCropHatch()
    └── ProcessHatches(ed, hatchIds, boundaryId, keepInside)
            ├── GenerateHatchBoundaryCommand.GenerateHatchBoundary(hatchId)     ← AddinsACAD
            ├── CropClosedCurveCommand.CropClosedCurveMulti(ids, bId, dir)      ← AddinsACAD
            ├── SortByContainmentHierarchy(curveIds, style, ts, clipArea)       ← AddinsACAD
            ├── CloneHatchCommand.ExtractHatchParams(srcHatchId)                ← AddinsACAD
            └── CloneHatchCommand.CloneHatchWithNewBoundaries(ts, ...)          ← AddinsACAD

CropInsideCommand.ExecuteCrop()
    └── CropHatchCommand.ProcessHatches(ed, hatchIds, boundaryId, keepInside)  ← 跨命令依赖!
```

### 1.3 依赖的静态方法分析

这些方法虽然位于 AddinsACAD 命令类中，但**不含 UI 交互**，仅使用 `CadServiceManager` / `ITransactionService`：

| 方法 | 当前位置 | 实际层级 |
|------|---------|---------|
| `GenerateHatchBoundaryCommand.GenerateHatchBoundary` | AddinsACAD | 应为 ServiceACAD |
| `CropClosedCurveCommand.CropClosedCurveMulti` | AddinsACAD | 应为 ServiceACAD |
| `CloneHatchCommand.ExtractHatchParams` | AddinsACAD | 应为 ServiceACAD |
| `CloneHatchCommand.CloneHatchWithNewBoundaries` | AddinsACAD | 应为 ServiceACAD |

---

## 二、目标架构

### 2.1 重构后调用链

```
CropHatchCommand.ExecuteCropHatch()                    ← AddinsACAD（仅 UI 交互）
    ├── 选择边界 → 创建 ICropBoundary
    ├── 选择 Hatch → ObjectId 列表
    ├── 询问方向 → bool keepInside
    └── CropHatchService.ProcessHatches(hatchIds, boundaryId, boundary, keepInside)
            ├── HatchBoundaryService.GenerateHatchBoundary(hatchId)    ← ServiceACAD
            ├── CropClosedCurveService.CropClosedCurveMulti(...)       ← ServiceACAD
            ├── SortByContainmentHierarchy(...)                        ← ServiceACAD (private)
            ├── HatchCloneService.ExtractHatchParams(srcHatchId)       ← ServiceACAD
            └── HatchCloneService.CloneHatchWithNewBoundaries(...)     ← ServiceACAD

CropInsideCommand.ExecuteCrop()                         ← AddinsACAD
    └── CropHatchService.ProcessHatches(hatchIds, boundaryId, boundary, keepInside)
```

### 2.2 新签名

```csharp
// 旧签名（AddinsACAD）
public static ProcessHatchesResult ProcessHatches(
    Editor ed,                              // ← 移除：仅用于日志
    IReadOnlyList<ObjectId> hatchIds,
    ObjectId boundaryId,
    bool keepInside)

// 新签名（ServiceACAD）
public ProcessHatchesResult ProcessHatches(
    IReadOnlyList<ObjectId> hatchIds,       // 待裁剪的 Hatch ObjectId 列表
    ObjectId boundaryId,                    // 裁剪边界曲线 ObjectId（CAD 操作需要）
    ICropBoundary boundary,                 // ← 新增：抽象边界（几何计算、面积）
    bool keepInside)                        // true=保留内部，false=保留外部
```

**变更说明：**

- `Editor ed` → 内部使用 `Logger._.Info()` 替代 `ed.WriteMessage()`
- 新增 `ICropBoundary boundary`：服务层调用方可自行创建边界，无需依赖命令层的 Editor 交互
- `boundaryId` 保留：`CropClosedCurveMulti` 内部需要 ObjectId 创建 CurveSelection

---

## 三、文件变更清单

### 3.1 新建文件

| 文件 | 说明 |
|------|------|
| `src/ServiceACAD/HatchBoundaryService.cs` | 从 `GenerateHatchBoundaryCommand` 提取静态方法 |
| `src/ServiceACAD/HatchCloneService.cs` | 从 `CloneHatchCommand` 提取 `ExtractHatchParams` + `CloneHatchWithNewBoundaries` |
| `src/ServiceACAD/CropClosedCurveService.cs` | 从 `CropClosedCurveCommand` 提取 `CropClosedCurveMulti` 静态方法 |

### 3.2 修改文件

| 文件 | 变更 |
|------|------|
| `src/ServiceACAD/CropHatchService.cs` | 重写：合并 `ProcessHatches` + `SortByContainmentHierarchy` + `IsPointInsidePolygon` |
| `src/AddinsACAD/Commands/CropHatchCommand.cs` | 移除 `ProcessHatches`、`ProcessHatchesResult`、`SortByContainmentHierarchy`、`IsPointInsidePolygon`；改为调用 `CropHatchService` |
| `src/AddinsACAD/Commands/CropInsideCommand.cs` | `CropHatchCommand.ProcessHatches(...)` → `cropHatchService.ProcessHatches(...)` |
| `src/AddinsACAD/Commands/GenerateHatchBoundaryCommand.cs` | 移除 `GenerateHatchBoundary` 静态方法；保留命令 UI |
| `src/AddinsACAD/Commands/CloneHatchCommand.cs` | 移除 `ExtractHatchParams`、`CloneHatchWithNewBoundaries`；保留命令 UI |
| `src/AddinsACAD/Commands/CropClosedCurveCommand.cs` | 移除 `CropClosedCurveMulti` 静态方法；保留命令 UI |

---

## 四、ICropBoundary 面积计算

`ProcessHatches` 内部使用 `clipArea`（裁剪边界面积）进行包含关系层次排序。当前从 AutoCAD Curve 读取：

```csharp
// 旧代码：从 AutoCAD Curve 读取面积
if (clipCurve is Polyline clipPl) clipArea = clipPl.Area;
else if (clipCurve is Circle clipCirc) clipArea = clipCirc.Area;
else if (clipCurve is Ellipse clipEll) clipArea = clipEll.Area;
```

新方案：利用 `ICropBoundary` 的多态特性，通过辅助方法计算面积，**不再依赖 AutoCAD Curve**：

```csharp
// 新代码：从 ICropBoundary 计算面积（纯几何，无 AutoCAD 依赖）
private static double ComputeBoundaryArea(ICropBoundary boundary)
{
    if (boundary is CircleCropBoundary circle)
        return Math.PI * circle.Radius * circle.Radius;
    if (boundary is EllipseCropBoundary ellipse)
        return Math.PI * ellipse.SemiMajor * ellipse.SemiMinor;
    // PolygonCropBoundary / SplineCropBoundary: 使用多边形面积公式
    var polygon = boundary.GetApproximatePolygon();
    return ComputePolygonArea(polygon);
}
```

但 `CircleCropBoundary` / `EllipseCropBoundary` 的 `Radius` / `SemiMajor` / `SemiMinor` 属性可能不是公开的。需要先确认这些实现类的公开 API。

**备选方案**：统一使用 `GetApproximatePolygon()` 计算多边形面积（对所有类型通用，精度足够用于面积匹配的 1% 容差）。

---

## 五、类设计（SRP 合规）

### 5.1 CropHatchService（重写后）

```
CropHatchService
├── 字段: _geometry (ICropGeometryService)
├── CropHatchesInside(bp, ids, ts) → OpResult<CropHatchResult>  [保留]
├── CropHatchesOutside(bp, ids, ts) → OpResult<CropHatchResult>  [保留]
├── ProcessHatches(hatchIds, boundaryId, boundary, keepInside) → ProcessHatchesResult  [新增]
│   ├── 调用 HatchBoundaryService.GenerateHatchBoundary
│   ├── 调用 CropClosedCurveService.CropClosedCurveMulti
│   ├── 调用 SortByContainmentHierarchy (private)
│   ├── 调用 HatchCloneService.ExtractHatchParams
│   └── 调用 HatchCloneService.CloneHatchWithNewBoundaries
├── SortByContainmentHierarchy(...)  [private, 从 CropHatchCommand 移入]
├── IsPointInsidePolygon(...)        [private, 从 CropHatchCommand 移入]
└── ComputeBoundaryArea(...)         [private, 新增]
```

**SRP 检查**：
- 字段: 1 个 (_geometry) ✓
- 方法: 约 8 个 → 考虑拆分，但 SortByContainmentHierarchy 是 ProcessHatches 的内部细节，保持在一起合理
- 类行数: 需控制 < 200 行。如果 ProcessHatches + SortByContainmentHierarchy 超过限制，将排序逻辑提取到独立的 `HatchContainmentSorter` 辅助类

### 5.2 HatchBoundaryService

```
HatchBoundaryService
└── GenerateHatchBoundary(ObjectId hatchId) → GenerateHatchBoundaryResult
```

从 [`GenerateHatchBoundaryCommand.GenerateHatchBoundary`](src/AddinsACAD/Commands/GenerateHatchBoundaryCommand.cs:70) 直接迁移。

### 5.3 HatchCloneService

```
HatchCloneService
├── ExtractHatchParams(ObjectId hatchId) → OpResult<HatchParams>
└── CloneHatchWithNewBoundaries(ts, params, boundaryIds, out newHatchId) → bool
```

从 [`CloneHatchCommand`](src/AddinsACAD/Commands/CloneHatchCommand.cs:46) 提取静态方法。

### 5.4 CropClosedCurveService

```
CropClosedCurveService
├── CreateCurveSelection(ObjectId curveId) → CurveSelection
├── CropClosedCurveMulti(subjectIds, clipId, keepInside) → CropResult
└── CropClosedCurveMulti(subjectCurves, clipCurve, keepInside) → CropResult
```

从 [`CropClosedCurveCommand`](src/AddinsACAD/Commands/CropClosedCurveCommand.cs:72) 提取静态方法和类型。

---

## 六、命令层适配

### 6.1 CropHatchCommand

```csharp
// 旧代码
var result = ProcessHatches(ed, hatchIds, boundaryId, capturedKeepInside);

// 新代码
var boundary = CropBoundaryFactory.CreateFromCurve(curve); // 已有 ICropBoundary
var cropHatchService = new CropHatchService(new CropGeometryService());
var result = cropHatchService.ProcessHatches(hatchIds, boundaryId, boundary, capturedKeepInside);
```

### 6.2 CropInsideCommand

```csharp
// 旧代码（第129行）
var hatchResult = CropHatchCommand.ProcessHatches(ed, hatchIds, boundaryId, keepInside);

// 新代码
var cropHatchService = new CropHatchService(geoService);
var hatchResult = cropHatchService.ProcessHatches(hatchIds, boundaryId, boundary, keepInside);
```

注意：`CropInsideCommand` 已经有 `boundary`（ICropBoundary）变量（第51行），可以直接传入。

---

## 七、测试影响

### 7.1 纯逻辑测试（优先）

- `SortByContainmentHierarchy` 依赖 `ITransactionService`（AutoCAD），无法纯逻辑测试
- `IsPointInsidePolygon` 依赖 `Polyline`（AutoCAD），无法纯逻辑测试
- `ComputeBoundaryArea` 纯几何计算 → **可纯逻辑测试** ✓

### 7.2 CAD 自动测试

- `CropHatchService.ProcessHatches` 整体流程需要 CAD 自动测试
- 复用现有 `CropHatchServiceTests` 结构

### 7.3 CAD 手工测试

- `CROPHATCH` / `CROPALLHATCHES` 命令行为不变
- `CROPINSIDE` / `CROPOUTSIDE` 的 Hatch 裁剪行为不变

---

## 八、实施步骤

| 步骤 | 内容 | 影响范围 |
|------|------|---------|
| 1 | 创建 `HatchBoundaryService.cs`，迁移 `GenerateHatchBoundary` | ServiceACAD 新增 |
| 2 | 创建 `HatchCloneService.cs`，迁移 `ExtractHatchParams` + `CloneHatchWithNewBoundaries` | ServiceACAD 新增 |
| 3 | 创建 `CropClosedCurveService.cs`，迁移 `CropClosedCurveMulti` | ServiceACAD 新增 |
| 4 | 重写 `CropHatchService.cs`，合并 `ProcessHatches` + 排序逻辑 | ServiceACAD 修改 |
| 5 | 更新 `CropHatchCommand.cs`：移除方法，改为调用 Service | AddinsACAD 修改 |
| 6 | 更新 `CropInsideCommand.cs`：调用新位置 | AddinsACAD 修改 |
| 7 | 更新 `GenerateHatchBoundaryCommand.cs`：委托到 Service | AddinsACAD 修改 |
| 8 | 更新 `CloneHatchCommand.cs`：委托到 Service | AddinsACAD 修改 |
| 9 | 更新 `CropClosedCurveCommand.cs`：委托到 Service | AddinsACAD 修改 |
| 10 | 更新 `ARCHITECTURE.md` | 文档更新 |

---

## 九、风险评估

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| `SortByContainmentHierarchy` 使用 `ITransactionService` 读取 Polyline，迁移后行为不变 | 低 | 仅移动位置，不修改逻辑 |
| `CropClosedCurveMulti` 的 `CurveSelection`/`CropResult` 类型被多处引用 | 中 | 保留原类型在命令层作为兼容别名，或在 Service 中定义后命令层引用 |
| 面积计算从 AutoCAD Curve 改为 ICropBoundary 可能引入精度差异 | 低 | 1% 容差足够宽松，多边形近似面积精度足够 |
| `GenerateHatchBoundaryCommand` UI 方法依赖静态方法 | 低 | 命令层保留 UI 方法，委托到 Service |

---

## 十、已确认问题（设计决策）

### 10.1 ICropBoundary 面积计算 ✅ 已确认

`CircleCropBoundary.Radius`（第45行）和 `EllipseCropBoundary.MajorRadius`/`MinorRadius`（第77-80行）均为公开属性。

```csharp
// 面积计算实现（在 CropHatchService 中）
private static double ComputeBoundaryArea(ICropBoundary boundary)
{
    if (boundary is CircleCropBoundary circle)
        return Math.PI * circle.Radius * circle.Radius;
    if (boundary is EllipseCropBoundary ellipse)
        return Math.PI * ellipse.MajorRadius * ellipse.MinorRadius;
    // PolygonCropBoundary / 其他：使用多边形面积公式
    var polygon = boundary.GetApproximatePolygon();
    return ComputePolygonArea(polygon);
}
```

### 10.2 CurveSelection / CropResult 类型位置 ✅ 决策

**类型定义移到 ServiceACAD**，`CropClosedCurveCommand` 从 ServiceACAD 引用。理由：
- 这些类型不含 UI 逻辑，属于服务层概念
- `CurveSelection` 持有 `ICropBoundary` 和 `ExactSegment`（均为 Core 类型）
- 命令层通过 `using` 引用或完全限定名访问

### 10.3 Editor.WriteMessage 替换 ✅ 决策

使用 `Logger._.Info()` 替代所有 `ed.WriteMessage()` 调用。理由：
- 服务层不应直接输出到 AutoCAD 命令行
- 中间进度信息记录到日志文件即可
- 命令层从 `ProcessHatchesResult` 读取汇总统计后自行输出
- 符合"命令层=输出显示，服务层=主体逻辑"的架构原则

### 10.4 ProcessHatchesResult 类型位置 ✅ 决策

与 `ProcessHatches` 方法一起移到 `CropHatchService`。命令层通过 `CropHatchService.ProcessHatchesResult` 引用。
