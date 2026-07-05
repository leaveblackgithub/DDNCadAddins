# Hatch 裁剪 — 包含关系层次排序设计

> 版本：1.1.0 | 日期：2026-07-05 | 状态：设计中

---

## 一、HatchStyle 语义

### 1.1 AutoCAD HatchStyle 定义

| HatchStyle | 填充行为 | 环嵌套层级 |
|------------|---------|-----------|
| `Normal` | 交替填充：depth-0 填充，depth-1 不填充，depth-2 填充… | 所有深度参与 |
| `Outer` | **只填充 depth-0 与 depth-1 之间的区域**，depth≥2 忽略 | 仅 depth-0 和 depth-1 |
| `Ignore` | 填充 depth-0 内部全部区域，忽略所有内环 | 仅 depth-0 |

### 1.2 包含关系层次定义

```
depth 0: 最外环（不被任何其他环包含）
depth 1: 最外环内的孔洞（被 depth-0 包含，但不被其他 depth-1 包含）
depth 2: 孔洞内的岛（被 depth-0 和 depth-1 包含）
depth 3: 岛内的孔洞（被 depth-0, depth-1, depth-2 包含）
...
```

**depth = 包含此环的上级环数量**

### 1.3 用户场景

原始 Hatch 有 3 个环，包含关系为 A→B→C：
- **环 A**（depth 0，最外环）：大矩形
- **环 B**（depth 1，孔洞）：中圆
- **环 C**（depth 2，岛）：小圆

`HatchStyle.Outer` 填充区域 = A \ B（大矩形减去中圆），环 C 被忽略。

### 1.4 裁剪后的正确行为

裁剪后应填充在**裁剪后的 A 与裁剪后的 B 之间**。环 C 裁剪后的结果仍然忽略。

---

## 二、当前流程与问题分析

### 2.1 当前 ProcessHatches 流程

```
1. GenerateHatchBoundary → 生成边界实体（所有环）
2. CropClosedCurveMulti → 对每个边界实体独立裁剪
3. 按面积降序排序（有缺陷！）× 按 HatchStyle 取相应数量
4. CloneHatchWithNewBoundaries → 用排序后的曲线重建 Hatch
```

### 2.2 面积排序的缺陷

| 场景 | 面积排序行为 | 错误原因 |
|------|-------------|---------|
| 单一外环+单一孔洞 | 外环面积>孔洞面积 → 正确 | — |
| 外环+多个孔洞（同层） | 按面积大小排出顺序，无法区分哪个是外环 | 面积排序改变了内外层次 |
| 裁剪后外环面积<裁剪后孔洞面积 | 裁剪边界大幅削切外环，但孔洞几乎未变 | 面积排序把孔洞排到外环前面 |
| 外环分裂成多个碎片 | 面积最大的碎片 ≠ 外环的完整裁剪结果 | 多个碎片可能都是外环的一部分 |

**面积排序的致命缺陷：无法反映包含关系（谁包含谁）。**

---

---

## 四、边界条件分析（更新版：以包含关系为基准）

### 4.1 裁剪方向

| 命令 | keepInside | 语义 | 对 Hatch 环的影响 |
|------|-----------|------|------------------|
| CROPOUTSIDE | true | 保留裁剪边界内部 | 每个环取与裁剪边界的交集 |
| CROPINSIDE | false | 保留裁剪边界外部 | 每个环取与裁剪边界的差集 |

### 4.2 测试用例：3层嵌套 Hatch (A→B→C)

```
原始 Hatch (HatchStyle.Outer):
  环 A (depth 0): 大矩形 100×100
  环 B (depth 1): 中圆 半径 30（在 A 内部）
  环 C (depth 2): 小圆 半径 10（在 B 内部）
  
Outer 填充: A \ B（环 C 被忽略）
```

### 4.3 CROPOUTSIDE（保留内部 = 交集 A ∩ Clip）边界条件

| # | 场景 | 裁剪后环 | 包含关系 depth | 正确行为 |
|---|------|---------|---------------|---------|
| 1 | Clip 完全包含 A | A', B', C' | A':0, B':1, C':2 | Outermost: A', Default: B' (忽略 C') |
| 2 | Clip 在 A 内、B 外 | A' (Clip形状) | A':0 | Outermost: A' (无孔洞) |
| 3 | Clip 在 A 内、B 内、C 外 | A'(=B'), 均=Clip | A':0, B':0(同形) | 填充 A'\B' = 空 → 删除 Hatch |
| 4 | Clip 在 A 内、B 内、C 内 | A'=B'=C'=Clip | 全为 0(同形) | 填充 A'\B' = 空 → 删除 Hatch |
| 5 | Clip 跨越 A+B | A'_part, B'_part | A':0, B':1 | Outermost: A', Default: B' |
| 6 | Clip 完全在 A 外 | 无 | — | 删除 Hatch |
| 7 | Clip 只与 A 相交 | A'_part | A':0 | Outermost: A' (无孔洞) |
| 8 | Clip 只与 B 相交 | B'_part (无外环) | — | 删除 Hatch |

> **同形检测**：当两个裁剪后的 Polyline 面积近似相等（差 < 1e-8），且一个包含另一个时 → 视为同形，不建立包含关系，处于同 depth。

### 4.4 CROPINSIDE（保留外部 = 差集 A \ Clip）边界条件

| # | 场景 | 裁剪后环 | 包含关系 depth | 正确行为 |
|---|------|---------|---------------|---------|
| 1 | Clip 完全在 A 外 | A, B, C 不变 | A:0, B:1, C:2 | Outermost: A, Default: B (忽略 C) |
| 2 | Clip 在 A 内、B 外 | A'_带洞, B, C | A':0, B:1, C:2 | Outermost: A', Default: B (忽略 C) |
| 3 | Clip 在 A 内、B 内、C 外 | A'_带洞, B'_带洞, C | A':0, B':1, C:2 | Outermost: A', Default: B' (忽略 C) |
| 4 | Clip 在 A 内、B 内、C 内 | A'_带洞, B'_带洞, C'_带洞 | A':0, B':1, C':2 | Outermost: A', Default: B' (忽略 C') |
| 5 | Clip 跨越 A+B | A'_part, B'_part | A':0, B':1 | Outermost: A', Default: B' |
| 6 | Clip 完全包含 A | 空 | — | 删除 Hatch |
| 7 | Clip 只与 A 相交 | A'_part, B, C | A':0, B:1, C:2 | Outermost: A', Default: B |
| 8 | Clip 只与 B 相交、不与 A 相交 | A, B'_part, C | A:0, B':1, C:2 | Outermost: A, Default: B' |

### 4.5 特殊情况

| # | 场景 | 包含关系排序的处理 | 正确行为 |
|---|------|------------------|---------|
| 1 | 裁剪后只有1个环 | depth = 0（最外环） | Outermost: 该环（无孔洞） |
| 2 | 裁剪后无环 | — | 删除 Hatch |
| 3 | 裁剪后外环分裂成多个碎片 | 多个 depth-0 环 | 取第1个为 Outermost，其余忽略 |
| 4 | 同 depth 有多个环（多个孔洞同层） | depth-1 的环全部传入 | 全部为 Default，AutoCAD 自动填充两者之间的区域 |
| 5 | 裁剪后外环与孔洞同形同面积 | 同形检测 → 视为同 depth | 填充空 → 删除 Hatch |
| 6 | HatchStyle.Ignore | 只保留 depth = 0 | 第1个 = Outermost，忽略其余 |
| 7 | HatchStyle.Normal | 保留所有 depth | 全部传入，AutoCAD 交替填充 |

---

## 五、实现方案

### 5.1 ✅ 已完成 - 修改 GenerateHatchBoundary

**移除 HatchStyle 限制，生成所有环。**

### 5.2 ✅ 已完成 - 修改 CloneHatchWithNewBoundaries

**接受排序后的边界，第1个 Outermost，其余 Default：**

```csharp
var loopType = (i == 0) ? HatchLoopTypes.Outermost : HatchLoopTypes.Default;
```

### 5.3 🔄 本次实施 — 修改 ProcessHatches：用包含关系排序替换面积排序

#### 5.3.1 射线法判断点在多边形内

```csharp
/// <summary>
/// 使用射线法判断点是否在多边形内部（WCS 2D 投影）.
/// </summary>
private static bool IsPointInsidePolygon(Point3d point, Polyline polyline)
{
    if (!polyline.Closed) return false;
    
    int n = polyline.NumberOfVertices;
    bool inside = false;
    double px = point.X, py = point.Y;
    
    for (int i = 0, j = n - 1; i < n; j = i++)
    {
        var p1 = polyline.GetPoint3dAt(i);
        var p2 = polyline.GetPoint3dAt(j);
        
        // 水平向右射线法
        if ((p1.Y > py) != (p2.Y > py) &&
            px < (p2.X - p1.X) * (py - p1.Y) / (p2.Y - p1.Y) + p1.X)
        {
            inside = !inside;
        }
    }
    return inside;
}
```

#### 5.3.2 包含关系排序方法

```csharp
/// <summary>
/// 使用包含关系层次排序裁剪后的曲线列表.
/// 构建包含树，按 depth 升序 + 面积降序排列.
/// </summary>
private static List<ObjectId> SortByContainmentHierarchy(
    List<ObjectId> curveIds, HatchStyle style, ITransactionService ts)
{
    if (curveIds.Count <= 1) return curveIds;
    
    int n = curveIds.Count;
    var areas = new double[n];
    var plineCache = new Polyline[n];  // 只读缓存
    var depth = new int[n];
    
    // Step 1: 读取所有 Polyline，计算面积
    for (int i = 0; i < n; i++)
    {
        var pline = ts.GetObject<Polyline>(curveIds[i], OpenMode.ForRead);
        if (pline == null) continue;
        plineCache[i] = pline;
        areas[i] = pline.Area;
    }
    
    // Step 2: 构建包含矩阵，计算 depth = 被包含次数
    const double areaTol = 1e-8;
    for (int i = 0; i < n; i++)
    {
        if (plineCache[i] == null) continue;
        var testPt = plineCache[i].GetPoint3dAt(0);
        
        for (int j = 0; j < n; j++)
        {
            if (i == j || plineCache[j] == null) continue;
            
            // 同形检测：面积近似相等则视为 siblings
            if (Math.Abs(areas[i] - areas[j]) < areaTol) continue;
            
            if (IsPointInsidePolygon(testPt, plineCache[j]))
                depth[i]++;
        }
    }
    
    // Step 3: 按 HatchStyle 过滤
    var filtered = new List<(int Index, int Depth, double Area)>();
    for (int i = 0; i < n; i++)
    {
        if (plineCache[i] == null) continue;
        if (style == HatchStyle.Ignore && depth[i] > 0) continue;
        if (style == HatchStyle.Outer && depth[i] > 1) continue;
        filtered.Add((i, depth[i], areas[i]));
    }
    
    if (filtered.Count == 0) return new List<ObjectId>();
    
    // Step 4: 排序 — depth 升序，同 depth 面积降序
    filtered.Sort((a, b) =>
    {
        int cmp = a.Depth.CompareTo(b.Depth);
        if (cmp == 0) cmp = b.Area.CompareTo(a.Area);
        return cmp;
    });
    
    // Step 5: 构建结果
    var result = new List<ObjectId>();
    foreach (var item in filtered) result.Add(curveIds[item.Index]);
    return result;
}
```

#### 5.3.3 ProcessHatches 中替换面积排序

将原来的「按面积排序」代码块替换为调用 `SortByContainmentHierarchy`：

```csharp
// ★ 第四步：用包含关系层次排序替代面积排序
List<ObjectId> sortedCurveIds = new List<ObjectId>();
if (clippedCurveIds.Count > 0)
{
    CadServiceManager._.ExecuteInTransactions(null, ts =>
    {
        // 获取源 Hatch 的 HatchStyle
        HatchStyle srcStyle = HatchStyle.Normal;
        if (hatchIds.Count > 0 && hatchIds[0].IsValid && !hatchIds[0].IsErased)
        {
            var srcHatch = ts.GetObject<Hatch>(hatchIds[0], OpenMode.ForRead);
            if (srcHatch != null) srcStyle = srcHatch.HatchStyle;
        }

        // 使用包含关系层次排序（替代面积排序）
        sortedCurveIds = SortByContainmentHierarchy(
            clippedCurveIds, srcStyle, ts);
    });
}
```

### 5.4 HatchStyle 对应的环选择策略

| HatchStyle | depth 过滤 | Outermost | Default |
|-----------|-----------|-----------|---------|
| Ignore | depth == 0 | 第1个 depth-0 | 无 |
| Outer | depth <= 1 | 第1个 depth-0 | 所有 depth-1 |
| Normal | 所有 depth | 第1个 depth-0 | 其余所有 |

### 5.5 清理策略

- 裁剪中间产物（`allGeneratedIds` + 未被选入 `sortedCurveIds` 的 `clippedCurveIds`）全部删除
- 原始 Hatch 全部删除
- `sortedCurveIds` 中的曲线作为新 Hatch 的关联边界保留

---

## 六、实施步骤

1. ✅ **`GenerateHatchBoundaryCommand.cs`** — 移除 HatchStyle 环数限制，生成所有环（已完成）
2. ✅ **`CloneHatchCommand.cs` CloneHatchWithNewBoundaries** — 第1个 Outermost，其余 Default（已完成）
3. 🔄 **`CropHatchCommand.cs` ProcessHatches** — 用包含关系层次排序替换面积排序（本次实施）
4. 编译验证
5. Git 提交推送