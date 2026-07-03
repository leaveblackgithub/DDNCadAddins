# CROP 路径精确化迁移方案 — 统一调用 SUBTRACT 工具

> 版本：1.0 | 日期：2026-07-03 | 模式：🏗️ Architect
> 关联文档：[`PLAN_CROPHATCH.md`](docs/PLAN_CROPHATCH.md)

---

## 一、问题诊断

### 1.1 现状：两条路径的精度差异

| 路径 | 边界类型 | 求交方式 | 弧段精度 |
|------|---------|---------|---------|
| **SUBTRACT** | `ICropBoundary`（精确圆/椭圆/多边形） | [`CurveSubtractService.SplitEdgeByBoundary()`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs:126) — 弧段用 Atan2 精确角度参数 | ✅ 精确 |
| **CROP** | `IReadOnlyList<Point2D>`（折线化多边形） | [`CropGeometryService.FindLineSegmentIntersections()`](src/DDNCadAddins.Core/Services/CropGeometryService.cs) — 多边形边线段求交 | ❌ 折线化 |

### 1.2 根因分析

[`CropService`](src/ServiceACAD/CropService.cs:15) 调度器已支持 `ICropBoundary`（[`GetEffectiveBoundary()`](src/ServiceACAD/ICropService.cs:40)），但**所有 Handle* 方法都调用 `bp.GetApproximatePolygon()` 将精确边界折线化后传给子服务**：

```csharp
// CropService.cs:235 — 当前代码（问题所在）
var result = ki
    ? this._polylineService.CropPolylinesInside(bp.GetApproximatePolygon(), ids, ts)
    : this._polylineService.CropPolylinesOutside(bp.GetApproximatePolygon(), ids, ts);
```

各子服务（如 [`CropPolylineService`](src/ServiceACAD/CropPolylineService.cs:33)、[`CropArcService`](src/ServiceACAD/CropArcService.cs:24)、[`CropCircleService`](src/ServiceACAD/CropCircleService.cs:24)）接收的是 `IReadOnlyList<CorePoint2D>` 多边形顶点，内部用：
- 直线段求交走 `CropGeometryService.FindLineSegmentIntersections()` → 多边形边线段求交（折线化）
- 弧段求交各自实现了 `LineCircleIntersection()` → 精确但边界仍是折线化的多边形边

**结果**：即使边界是 Circle/Ellipse（精确解析），在 CROP 路径中也会被 `GetApproximatePolygon()` 折线化。

### 1.3 各子服务当前精度状态

| 子服务 | 被裁剪实体 | 求交精度 | 输出实体类型 | 迁移目标 |
|--------|-----------|---------|-------------|---------|
| [`CropLineService`](src/ServiceACAD/CropLineService.cs:43) | Line | ❌ 折线化 | Line | ✅ 迁移 |
| [`CropArcService`](src/ServiceACAD/CropArcService.cs:24) | Arc | ❌ 折线化边界 | Arc | ✅ 迁移 |
| [`CropCircleService`](src/ServiceACAD/CropCircleService.cs:24) | Circle | ❌ 折线化边界 | Arc | ✅ 迁移 |
| [`CropPolylineService`](src/ServiceACAD/CropPolylineService.cs:33) | Polyline | ❌ 折线化边界 | Polyline | ✅ 迁移 |
| [`CropEllipseService`](src/ServiceACAD/CropEllipseService.cs:25) | Ellipse | ⚠️ 参数搜索+GetSplitCurves | Ellipse | ❌ 保持现状 |
| [`CropSplineService`](src/ServiceACAD/CropSplineService.cs:25) | Spline | ⚠️ 参数搜索+GetSplitCurves | Spline | ❌ 保持现状 |

---

## 二、迁移目标

### 2.1 核心原则

> **将 CROP 路径中 Line / Arc / Circle / Polyline 四个子服务的求交逻辑，替换为 SUBTRACT 路径的工具链。Ellipse 和 Spline 保持现状（参数搜索 + GetSplitCurves）。**

### 2.2 SUBTRACT 工具链可复用方法

| 方法 | 来源 | 作用 |
|------|------|------|
| [`ICropBoundary.FindLineIntersections()`](src/DDNCadAddins.Core/Interfaces/ICropBoundary.cs:32) | Core | 线段与精确边界求交（圆/椭圆解析解，多边形线段求交） |
| [`ICropBoundary.IsPointInside()`](src/DDNCadAddins.Core/Interfaces/ICropBoundary.cs:24) | Core | 点是否在精确边界内 |
| [`ICropBoundary.ClassifyBoundingBox()`](src/DDNCadAddins.Core/Interfaces/ICropBoundary.cs:40) | Core | 包围盒快速分类 |

### 2.3 不迁移的部分

| 子服务 | 原因 |
|--------|------|
| [`CropEllipseService`](src/ServiceACAD/CropEllipseService.cs:25) | 使用 `GetSplitCurves()` 保留原始 Ellipse 类型，SUBTRACT 路径会将椭圆弧采样为折线 |
| [`CropSplineService`](src/ServiceACAD/CropSplineService.cs:25) | 同上，`GetSplitCurves()` 保留原始 Spline 类型 |
| 非曲线服务（Text/MText/Block/Dim/Point/Solid） | 不涉及曲线求交 |

---

## 三、迁移方案

### 3.1 方案概述：子服务接口升级

将四个子服务的方法签名从接收 `IReadOnlyList<CorePoint2D>` 改为接收 `ICropBoundary`：

```csharp
// ── 迁移前 ──
public OpResult<CropLineResult> CropLinesInside(
    IReadOnlyList<CorePoint2D> boundaryPoints, ...)

// ── 迁移后 ──
public OpResult<CropLineResult> CropLinesInside(
    ICropBoundary boundary, ...)
```

### 3.2 CropService 调度器修改

**当前代码**（`bp.GetApproximatePolygon()` 折线化）：

```csharp
private bool HandleLine(Line line, ICropBoundary bp, bool ki, ITransactionService ts, CropResult r)
{
    var ids = new List<ObjectId> { line.ObjectId };
    var result = ki
        ? this._lineService.CropLinesInside(bp.GetApproximatePolygon(), ids, ts)  // ❌ 折线化
        : this._lineService.CropLinesOutside(bp.GetApproximatePolygon(), ids, ts);
    ...
}
```

**迁移后**（直接传递 `ICropBoundary`）：

```csharp
private bool HandleLine(Line line, ICropBoundary bp, bool ki, ITransactionService ts, CropResult r)
{
    var ids = new List<ObjectId> { line.ObjectId };
    var result = ki
        ? this._lineService.CropLinesInside(bp, ids, ts)  // ✅ 精确边界
        : this._lineService.CropLinesOutside(bp, ids, ts);
    ...
}
```

**修改的 Handle* 方法**（4 个）：
- [`HandleLine()`](src/ServiceACAD/CropService.cs:275)
- [`HandleArc()`](src/ServiceACAD/CropService.cs:260)
- [`HandleCircle()`](src/ServiceACAD/CropService.cs:245)
- [`HandlePolyline()`](src/ServiceACAD/CropService.cs:230)

**不修改的 Handle* 方法**（2 个，保持 `GetApproximatePolygon()`）：
- [`HandleEllipse()`](src/ServiceACAD/CropService.cs:303) — 保持现状
- [`HandleSpline()`](src/ServiceACAD/CropService.cs:290) — 保持现状

### 3.3 各子服务内部修改

#### 3.3.1 CropLineService — 最简迁移

```csharp
// 迁移前：
var intersections = this._cropGeometry.FindLineSegmentIntersections(startPt, endPt, boundaryPoints);
var midInside = this._cropGeometry.IsPointInPolygon(midPt, boundaryPoints);

// 迁移后：
var intersections = boundary.FindLineIntersections(startPt, endPt);
var midInside = boundary.IsPointInside(midPt);
```

直线段无弧段，直接用 `ICropBoundary` 的方法替换 `ICropGeometryService` 的多边形方法。**输出仍为 Line**。

#### 3.3.2 CropArcService — 弧段精确化

```csharp
// 迁移前（SplitArcAndKeep 内部）：
// 手动遍历多边形边，调用 GeometryHelper.LineCircleIntersection()
for (int i = 0, j = bpts.Count - 1; i < bpts.Count; j = i++)
{
    var segIx = GeometryHelper.LineCircleIntersection(bpts[j].X, ...);
    ...
}
var inside = this._cropGeometry.IsPointInPolygon(new CorePoint2D(midX, midY), bpts);

// 迁移后：
// 弧段采样为弦段序列，对每条弦调用 boundary.FindLineIntersections()
var arcPoints = SampleArcToPoints(cx, cy, r, sa, ea, 64);
var angles = new List<double>();
for (int i = 0; i < arcPoints.Count - 1; i++)
{
    var ix = boundary.FindLineIntersections(arcPoints[i], arcPoints[i + 1]);
    foreach (var pt in ix)
    {
        var ang = Math.Atan2(pt.Y - cy, pt.X - cx);
        if (AngleInRange(ang, sa, ea))
            angles.Add(NormalizeAngle(ang, sa, ea));
    }
}
var inside = boundary.IsPointInside(new CorePoint2D(midX, midY));
```

**关键改进**：当边界是 Circle/Ellipse 时，`FindLineIntersections()` 使用解析解而非折线化边求交。**输出仍为 Arc**。

#### 3.3.3 CropCircleService — 同 CropArcService

与 [`CropArcService`](src/ServiceACAD/CropArcService.cs:24) 类似，圆的 360° 拆分为 Arc。**输出仍为 Arc**。

#### 3.3.4 CropPolylineService — 弧段精确化

```csharp
// 迁移前（ProcessArcSegmentExact 内部）：
// 手动遍历多边形边，调用 GeometryHelper.LineCircleIntersection()
for (int i = 0, j = bpts.Count - 1; i < bpts.Count; j = i++)
{
    var segIx = GeometryHelper.LineCircleIntersection(bpts[j].X, ...);
    ...
}

// 迁移后：
// 弧段采样为弦段序列，对每条弦调用 boundary.FindLineIntersections()
var arcPoints = SampleArcToPoints(cx, cy, r, sa, ea, 64);
var angles = new List<double>();
for (int i = 0; i < arcPoints.Count - 1; i++)
{
    var ix = boundary.FindLineIntersections(arcPoints[i], arcPoints[i + 1]);
    foreach (var pt in ix)
    {
        var ang = Math.Atan2(pt.Y - cy, pt.X - cx);
        if (AngleInRange(ang, sa, ea))
            angles.Add(NormalizeAngle(ang, sa, ea));
    }
}

// 直线段也改用 boundary.FindLineIntersections()
var ix = boundary.FindLineIntersections(startP, endP);
```

**关键改进**：直线段和弧段都通过 `ICropBoundary` 求交。**输出仍为 Polyline**（弧段用凸度还原）。

### 3.4 迁移前后精度对比

| 场景 | 迁移前 | 迁移后 |
|------|--------|--------|
| 边界=Circle，裁剪 Line | ❌ 圆被折线化，交点近似 | ✅ `CircleCropBoundary.FindLineIntersections()` 解析解 |
| 边界=Ellipse，裁剪 Arc | ❌ 椭圆被折线化，交点近似 | ✅ `EllipseCropBoundary.FindLineIntersections()` 解析解 |
| 边界=Polyline，裁剪 Line | ✅ 多边形线段求交（精确） | ✅ 同（`PolygonCropBoundary` 内部逻辑相同） |
| 边界=Circle，裁剪 Polyline 弧段 | ❌ 圆被折线化 | ✅ 解析解 |
| 边界=Polyline，裁剪 Spline | ⚠️ 参数搜索 | ⚠️ 保持现状（不迁移） |

---

## 四、修改文件清单

| 文件 | 修改类型 | 修改内容 |
|------|---------|---------|
| [`CropService.cs`](src/ServiceACAD/CropService.cs) | 修改 | 4 个 Handle* 方法：`bp.GetApproximatePolygon()` → `bp` |
| [`CropLineService.cs`](src/ServiceACAD/CropLineService.cs) | 修改 | 签名改为 `ICropBoundary`，内部求交改用 `boundary.FindLineIntersections()` |
| [`CropArcService.cs`](src/ServiceACAD/CropArcService.cs) | 修改 | 签名改为 `ICropBoundary`，弧段求交改用 `boundary.FindLineIntersections()` |
| [`CropCircleService.cs`](src/ServiceACAD/CropCircleService.cs) | 修改 | 签名改为 `ICropBoundary`，圆求交改用 `boundary.FindLineIntersections()` |
| [`CropPolylineService.cs`](src/ServiceACAD/CropPolylineService.cs) | 修改 | 签名改为 `ICropBoundary`，直线段和弧段求交改用 `boundary.FindLineIntersections()` |

**不修改的文件**：
- [`CropEllipseService.cs`](src/ServiceACAD/CropEllipseService.cs) — 保持 `GetApproximatePolygon()`
- [`CropSplineService.cs`](src/ServiceACAD/CropSplineService.cs) — 保持 `GetApproximatePolygon()`
- 所有非曲线服务 — 不涉及

---

## 五、向后兼容性

### 5.1 公共 API 兼容

各子服务原有的 `CropXxxInside(IReadOnlyList<CorePoint2D>, ...)` 方法被外部直接调用的情况：

| 调用方 | 当前用法 | 迁移策略 |
|--------|---------|---------|
| [`CropService`](src/ServiceACAD/CropService.cs) Handle* | `bp.GetApproximatePolygon()` | 改为传 `bp` |
| [`CropTestsCommand`](src/AddinsACAD/Commands/CropTestsCommand.cs) | 通过 `CropService` 间接调用 | 无需修改 |
| [`CropInsideCommand`](src/AddinsACAD/Commands/CropInsideCommand.cs) | 通过 `CropService` 间接调用 | 无需修改 |
| 子服务的 `CropAllXxxInside` 方法 | 内部调用 `CropXxx` | 同步修改签名 |

### 5.2 重载保留策略

为避免破坏性变更，在迁移后的子服务中保留旧签名为**兼容重载**：

```csharp
// 新签名（主方法）
public OpResult<CropLineResult> CropLinesInside(
    ICropBoundary boundary, List<ObjectId> lineIds, ITransactionService ts)

// 旧签名（兼容重载，内部包装为 PolygonCropBoundary）
public OpResult<CropLineResult> CropLinesInside(
    IReadOnlyList<CorePoint2D> boundaryPoints, List<ObjectId> lineIds, ITransactionService ts)
{
    return this.CropLinesInside(new PolygonCropBoundary(boundaryPoints), lineIds, ts);
}
```

---

## 六、SOLID 检查

| 原则 | 检查 |
|------|------|
| **SRP** | 各子服务仍只负责各自曲线类型裁剪，职责不变 |
| **OCP** | 通过 `ICropBoundary` 多态扩展，不修改接口 |
| **LSP** | `CircleCropBoundary` / `EllipseCropBoundary` / `PolygonCropBoundary` 可替换 |
| **ISP** | `ICropBoundary` 方法数 5 个，不超限 |
| **DIP** | 子服务依赖 `ICropBoundary` 抽象，不依赖具体实现 |

---

## 七、测试计划

### 7.1 第一优先级（纯逻辑测试）

[`CropGeometryServiceTests.cs`](src/DDNCadAddins.Core.Tests/CropGeometryServiceTests.cs) 和 [`CropBoundaryTests.cs`](src/DDNCadAddins.Core.Tests/CropBoundaryTests.cs) 已覆盖 `ICropBoundary` 各实现的求交逻辑。无需新增纯逻辑测试。

### 7.2 第二优先级（CAD 环境测试）

验证迁移后的精度提升：

| 测试用例 | 验证点 |
|---------|--------|
| Circle 边界裁剪 Line | 交点坐标与解析解一致（精度 < 1e-10） |
| Ellipse 边界裁剪 Arc | 交点坐标与解析解一致 |
| Polyline 边界裁剪 Polyline | 行为与迁移前一致（回归测试） |
| Circle 边界裁剪 Circle | 两个圆相交，拆分结果精确 |

### 7.3 第三优先级（手工测试）

通过 `MANUALCMDTESTS → C → CROPTESTS` 执行，使用含圆/椭圆边界的测试图纸，对比迁移前后的 TestRecords JSON。

---

## 八、实施顺序

| 步骤 | 文件 | 说明 |
|------|------|------|
| 1 | [`CropLineService.cs`](src/ServiceACAD/CropLineService.cs) | 最简迁移，验证 `ICropBoundary` 替换可行性 |
| 2 | [`CropArcService.cs`](src/ServiceACAD/CropArcService.cs) | 弧段求交迁移 |
| 3 | [`CropCircleService.cs`](src/ServiceACAD/CropCircleService.cs) | 圆求交迁移（与 Arc 类似） |
| 4 | [`CropPolylineService.cs`](src/ServiceACAD/CropPolylineService.cs) | 直线段 + 弧段求交迁移 |
| 5 | [`CropService.cs`](src/ServiceACAD/CropService.cs) | 4 个 Handle* 方法改为传 `bp` |
| 6 | CAD 环境测试 | 验证精度提升 + 回归测试 |

---

## 九、风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| `ICropBoundary.FindLineIntersections()` 对弧段的弦近似仍有误差 | 弧段求交精度取决于采样密度 | 弧段采样密度 64 点（与 SUBTRACT 路径的 [`SplitArcEdgeByBoundary()`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs:219) 一致） |
| 迁移后行为变化导致回归 | 已有测试可能失败 | 保留旧签名兼容重载，渐进迁移 |
| `PolygonCropBoundary` 场景精度不变 | 多边形边界无精度提升 | 符合预期，仅圆/椭圆边界受益 |

---

## 十、与 CROPHATCH 方案的关系

本迁移方案与 [`PLAN_CROPHATCH.md`](docs/PLAN_CROPHATCH.md) 独立但互补：

- **CROPHATCH**：新建命令，编排 GENERATEHATCHBOUNDARY + SUBTRACTCLOSEDCURVE + CLONEHATCH
- **CROP 精确化迁移**：修改已有 CROP 路径，将子服务求交从折线化升级为 `ICropBoundary` 精确边界

两者可独立实施，也可先做 CROP 精确化迁移再做 CROPHATCH（此时 CROPHATCH 可直接复用精确化后的 CROP 路径）。