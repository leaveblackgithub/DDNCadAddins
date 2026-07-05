# Hatch 裁剪 — 统一环有效性设计

> 版本：2.0.0 | 日期：2026-07-05 | 状态：设计中

---

## 一、核心概念

### 1.1 环的深度（depth）

Hatch 的每个环在包含关系树中有确定的深度：

```
depth 0: 最外环（不被任何其他环包含）
depth 1: 最外环内的孔洞
depth 2: 孔洞内的岛
depth 3: 岛内的孔洞
...
```

### 1.2 环的有效性分类

根据 HatchStyle 和环的深度，每个环分为三类：

| HatchStyle | depth 0 | depth 1 | depth 2+ | 说明 |
|-----------|---------|---------|----------|------|
| **Outer** | 外（有效） | 内（有效） | 无效 | 只填充外环与内环之间 |
| **Ignore** | 外（有效） | 无效 | 无效 | 只填充外环内部 |
| **Normal** | 外（有效，填充） | 内（有效，不填充） | 有效（交替填充） | 所有环参与交替填充 |

### 1.3 裁剪边界的深度（clipDepth）

裁剪边界对应原始 Hatch 的某个环。通过比较裁剪边界面积与各原始环面积确定：

```
clipDepth = 与裁剪边界面积最匹配的原始环的 depth
```

### 1.4 新深度（newDepth）

裁剪后，每个环的新深度 = 原始深度 - 裁剪边界深度：

```
newDepth = originalDepth - clipDepth
```

- `newDepth < 0`：**容器**（在裁剪边界之上，忽略）
- `newDepth >= 0`：**内容**（在裁剪边界处或之内，参与重建）

---

## 二、统一处理逻辑

### 2.1 核心流程

```
1. GenerateHatchBoundary → 生成所有环的边界实体，记录每个环的 loopIndex
2. 计算每个原始环的 depth（包含关系层次）
3. 确定 clipDepth（裁剪边界匹配的原始环 depth）
4. 逐个环裁剪（保留 loopIndex 关联）
5. 对每个裁剪结果计算 newDepth = originalDepth - clipDepth
6. 按 HatchStyle 和 newDepth 过滤
7. 排序后重建 Hatch
```

### 2.2 HatchStyle 分策略

| HatchStyle | 过滤规则 | 无有效外环时 |
|-----------|---------|------------|
| **Outer** | 保留 newDepth == 0（外环）和 newDepth == 1（内环） | **删除 Hatch** |
| **Normal** | 保留所有 newDepth >= 0（交替填充） | **删除 Hatch** |
| **Ignore** | 保留 newDepth == 0（外环） | 取面积最大的曲线作为外环 |

### 2.3 各场景分析

#### 场景：3 层嵌套 Hatch (A→B→C)，HatchStyle.Outer

```
环 A (depth 0, 外): 大矩形
环 B (depth 1, 内): 中圆
环 C (depth 2, 无效): 小圆
```

| 裁剪边界 | clipDepth | 裁剪后 newDepth | 过滤结果 | 正确行为 |
|---------|-----------|----------------|---------|---------|
| A（外环） | 0 | A':0, B':1, C':2 | A'(外)+B'(内), C'过滤 | 填充 A'\B' |
| B（内环） | 1 | A':-1(容器), B':0, C':1 | B'(外)+C'(内) | **但 Outer 只保留 depth≤1，B'是外环，C'是内环 → 填充 B'\C'** → 不对！ |

等等，这里有问题。让我重新分析。

**Outer 语义**：填充最外环与第一个内环之间。如果裁剪边界是内环 B（depth 1），则：
- 裁剪后 A' = A ∩ B = B 的形状（因为 B 在 A 内部）
- 裁剪后 B' = B ∩ B = B 的形状
- 裁剪后 C' = C ∩ B = C 的形状（如果 C 在 B 内部）

newDepth: A'=-1(容器), B'=0, C'=1

Outer 过滤：保留 newDepth 0 和 1 → B'(外) + C'(内)

但 B 原来是内环（孔洞），C 原来是无效环（岛）。裁剪后 B' 变成外环，C' 变成内环。

填充 B'\C' = B 的形状减去 C 的形状。

但用户说"OUTER的内环或者无效环剪切，HATCH完全删除"。

这意味着：当裁剪边界是 Outer 样式的内环或无效环时，应该删除 Hatch。

为什么？因为 Outer 样式只填充外环与内环之间。如果裁剪边界是内环 B：
- 裁剪后的区域 = B 的形状
- B 原来是孔洞（不填充区域）
- 在 B 的内部，Outer 样式不填充任何东西（因为 Outer 只填充 A\B，不填充 B 内部）
- 所以裁剪后应该没有填充区域 → 删除 Hatch

如果裁剪边界是无效环 C：
- 裁剪后的区域 = C 的形状
- C 在 B 内部，B 是孔洞（不填充）
- 所以 C 的区域也不填充 → 删除 Hatch

**结论：Outer 样式，当 clipDepth >= 1 时，删除 Hatch。**

#### 修正后的 Outer 逻辑

| 裁剪边界 | clipDepth | 行为 |
|---------|-----------|------|
| 外环（depth 0） | 0 | 正常裁剪：A'(外) + B'(内) |
| 内环（depth 1） | 1 | **删除 Hatch**（裁剪区域在孔洞内，无填充） |
| 无效环（depth 2+） | 2+ | **删除 Hatch**（裁剪区域在无效环内，无填充） |
| 非 Hatch 环 | 无法匹配 | 正常裁剪（按包含关系排序） |

#### Normal 逻辑

| 裁剪边界 | clipDepth | newDepth | 行为 |
|---------|-----------|---------|------|
| 外环（depth 0, 填充） | 0 | A':0, B':1, C':2 | 全部保留，交替填充 |
| 内环（depth 1, 不填充） | 1 | A':-1(容器), B':0, C':1 | 过滤容器，B'(外)+C'(内) |
| 岛（depth 2, 填充） | 2 | A':-2, B':-1, C':0 | 过滤容器，C'(外) |
| 孔洞内孔洞（depth 3, 不填充） | 3 | A':-3, B':-2, C':-1, D':0 | 过滤容器，D'(外) |

**Normal 规则**：保留 newDepth >= 0，过滤 newDepth < 0（容器）。newDepth 0 = Outermost，其余 = Default。

#### Ignore 逻辑

| 裁剪边界 | clipDepth | 行为 |
|---------|-----------|------|
| 外环（depth 0） | 0 | A'(外)，忽略所有内环 |
| 内环（depth 1+） | 1+ | 裁剪后 A' = clip 形状 → **A' 作为外环**（Ignore 填充所有内部） |
| 非 Hatch 环 | 无法匹配 | 取面积最大的曲线作为外环 |

**Ignore 规则**：取面积最大的曲线作为外环（因为 Ignore 填充最外环内部全部区域）。

---

## 三、实现方案

### 3.1 数据结构

```csharp
/// <summary>
/// 环的有效性分类.
/// </summary>
public enum LoopValidity
{
    Outer,    // 外环（depth 0，填充）
    Inner,    // 内环（depth 1，孔洞）
    Invalid,  // 无效环（depth 2+，在 Outer/Ignore 中被忽略）
}

/// <summary>
/// 带原始环信息的裁剪结果.
/// </summary>
public class CroppedLoopInfo
{
    public ObjectId CurveId;        // 裁剪后的曲线 ObjectId
    public int OriginalDepth;       // 原始环的 depth
    public int NewDepth;            // 新 depth = originalDepth - clipDepth
    public double Area;             // 裁剪后面积
    public LoopValidity Validity;   // 有效性分类
}
```

### 3.2 ProcessHatches 修改流程

```
1. 对每个 Hatch:
   a. GenerateHatchBoundary → 生成所有环，记录 loopIndex
   b. 计算每个环的 originalDepth（包含关系层次）
   c. 计算每个环的面积

2. 确定 clipDepth:
   a. 计算裁剪边界面积
   b. 与各原始环面积比较，找最匹配的环
   c. clipDepth = 匹配环的 originalDepth
   d. 如果无匹配（裁剪边界不是 Hatch 的环），clipDepth = 0

3. 逐个环裁剪:
   a. 对每个环的边界实体，调用 CropClosedCurveMulti 单独裁剪
   b. 记录裁剪结果的 CurveId 和 originalDepth

4. 计算 newDepth 和有效性:
   a. newDepth = originalDepth - clipDepth
   b. 按 HatchStyle 分类有效性

5. 按 HatchStyle 过滤:
   a. Outer: clipDepth >= 1 → 删除 Hatch
            clipDepth == 0 → 保留 newDepth 0 和 1
   b. Normal: 保留 newDepth >= 0，过滤 newDepth < 0
   c. Ignore: 取面积最大的曲线

6. 排序: newDepth 升序 + 面积降序

7. 重建 Hatch: newDepth 0 = Outermost，其余 = Default
```

### 3.3 clipDepth 匹配算法

```csharp
/// <summary>
/// 确定裁剪边界对应的原始环 depth.
/// </summary>
private static int DetermineClipDepth(
    double clipArea, List<(int LoopIndex, double Area, int Depth)> loopInfos,
    double areaTol = 0.01)
{
    // 按面积差绝对值排序，找最接近的环
    var sorted = loopInfos
        .OrderBy(x => Math.Abs(x.Area - clipArea))
        .ToList();

    if (sorted.Count == 0) return 0;

    // 如果最接近的环面积差 < 容差，认为匹配
    if (Math.Abs(sorted[0].Area - clipArea) / clipArea < areaTol)
        return sorted[0].Depth;

    // 无匹配：裁剪边界不是 Hatch 的环，clipDepth = 0
    return 0;
}
```

### 3.4 逐个环裁剪

```csharp
// 对每个环单独裁剪，保留 loopIndex 关联
var croppedLoops = new List<CroppedLoopInfo>();
foreach (var loopInfo in loopInfos)
{
    var cropResult = CropClosedCurveCommand.CropClosedCurveMulti(
        new List<ObjectId> { loopInfo.EntityId }, boundaryId, keepInside);

    if (cropResult.IsSuccess && cropResult.CreatedEntityIds != null)
    {
        foreach (var curveId in cropResult.CreatedEntityIds)
        {
            // 读取面积
            double area = 0;
            CadServiceManager._.ExecuteInTransactions(null, ts =>
            {
                var pline = ts.GetObject<Polyline>(curveId, OpenMode.ForRead);
                if (pline != null) area = pline.Area;
            });

            croppedLoops.Add(new CroppedLoopInfo
            {
                CurveId = curveId,
                OriginalDepth = loopInfo.Depth,
                Area = area,
            });
        }
    }
}
```

### 3.5 HatchStyle 过滤

```csharp
// 计算 newDepth
foreach (var cl in croppedLoops)
    cl.NewDepth = cl.OriginalDepth - clipDepth;

// 按 HatchStyle 过滤
List<CroppedLoopInfo> filtered;
switch (srcStyle)
{
    case HatchStyle.Outer:
        if (clipDepth >= 1)
        {
            // 裁剪边界是内环或无效环 → 删除 Hatch
            filtered = new List<CroppedLoopInfo>();
        }
        else
        {
            // 保留 newDepth 0 和 1
            filtered = croppedLoops
                .Where(x => x.NewDepth >= 0 && x.NewDepth <= 1)
                .ToList();
        }
        break;

    case HatchStyle.Normal:
        // 保留 newDepth >= 0，过滤容器
        filtered = croppedLoops
            .Where(x => x.NewDepth >= 0)
            .ToList();
        break;

    case HatchStyle.Ignore:
        // 取面积最大的曲线作为外环
        filtered = croppedLoops
            .OrderByDescending(x => x.Area)
            .Take(1)
            .ToList();
        break;

    default:
        filtered = croppedLoops.Where(x => x.NewDepth >= 0).ToList();
        break;
}

// 去重：相同面积只保留第一条
filtered = DeduplicateByArea(filtered, areaTol);

// 排序：newDepth 升序 + 面积降序
filtered = filtered
    .OrderBy(x => x.NewDepth)
    .ThenByDescending(x => x.Area)
    .ToList();
```

---

## 四、边界条件汇总

### 4.1 Outer 样式

| 裁剪边界 | clipDepth | 行为 |
|---------|-----------|------|
| 外环 | 0 | 保留 newDepth 0(外) + 1(内) |
| 内环 | 1 | **删除 Hatch** |
| 无效环 | 2+ | **删除 Hatch** |
| 非 Hatch 环 | 0 | 按包含关系排序，取 depth 0+1 |

### 4.2 Normal 样式

| 裁剪边界 | clipDepth | 行为 |
|---------|-----------|------|
| 外环(depth 0, 填充) | 0 | 全部保留，交替填充 |
| 内环(depth 1, 不填充) | 1 | 过滤容器，newDepth 0=外, 1=内 |
| 岛(depth 2, 填充) | 2 | 过滤容器，newDepth 0=外 |
| 更深层 | 3+ | 过滤容器，newDepth 0=外 |
| 非 Hatch 环 | 0 | 按包含关系排序，全部保留 |

### 4.3 Ignore 样式

| 裁剪边界 | clipDepth | 行为 |
|---------|-----------|------|
| 外环 | 0 | 取面积最大曲线作为外环 |
| 内环/无效环 | 1+ | 取面积最大曲线作为外环 |
| 非 Hatch 环 | 0 | 取面积最大曲线作为外环 |

---

## 五、实施步骤

1. **`GenerateHatchBoundaryCommand.cs`** — 返回每个环的 loopIndex 和面积
2. **`CropHatchCommand.cs` ProcessHatches** — 重写为统一逻辑：
   - 逐个环裁剪（保留 loopIndex 关联）
   - 确定 clipDepth
   - 计算 newDepth
   - 按 HatchStyle 过滤
3. **`CloneHatchCommand.cs`** — 保持 HatchStyle.Normal + 第1个 Outermost，其余 Default
4. 编译验证
5. Git 提交推送
