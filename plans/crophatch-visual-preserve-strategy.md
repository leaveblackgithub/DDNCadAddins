# CROPHATCH 视觉保留修改策略

> 版本：1.0.0 | 日期：2026-07-07 | 状态：设计中

---

## 一、问题诊断

### 1.1 现有流程

```
CropHatchService.ProcessHatches:
  1. GenerateHatchBoundary → 所有环边界实体合并
  2. CropClosedCurveMulti(allGeneratedIds, boundaryId, keepInside) → 批量裁剪
  3. SortByContainmentHierarchy(clippedCurveIds, style) → 排序+过滤
  4. CloneHatchWithNewBoundaries → 重建 Hatch
```

### 1.2 根本缺陷

| # | 缺陷 | 影响 |
|---|------|------|
| 1 | 所有环合并裁剪，丢失原始环关联 | 无法区分裁剪后曲线来自哪个原始环 |
| 2 | depth 从裁剪后重新计算，非 originalDepth - clipDepth | 过滤基于错误 depth |
| 3 | 过滤逻辑未区分 keepInside/keepOutside | 保留外部行为不正确 |
| 4 | IGNORE→OUTER 转换未实现 | 保留外部时 IGNORE 视觉被破坏 |
| 5 | HatchBoundaryService 返回扁平列表 | 无法逐环裁剪 |

---

## 二、Hatch 环的填充语义

### 2.1 环的深度

```
depth 0: 最外环 → 所有 Style 的"外环"
depth 1: 孔洞 → OUTER 的"内环"
depth 2: 岛 → NORMAL 填充
depth 3: 岛内孔洞 → NORMAL 不填充
```

### 2.2 HatchStyle 有效性矩阵

| HatchStyle | depth 0 | depth 1 | depth 2 | depth 3+ |
|-----------|---------|---------|---------|----------|
| NORMAL | ✅ 填充 | ❌ 不填充 | ✅ 填充 | 交替 |
| OUTER | ✅ 填充 | ❌ 不填充 | 🗑 忽略 | 🗑 忽略 |
| IGNORE | ✅ 填充 | 🗑 忽略 | 🗑 忽略 | 🗑 忽略 |

### 2.3 newDepth 公式

```
newDepth = originalDepth - clipDepth
newDepth < 0 → 容器环，不参与重建
newDepth >= 0 → 内容环，参与重建
```

---

## 三、保留内部策略

### 核心原则

> 裁剪后子区域内，原来有填充的继续填充，原来没有填充的继续不填充。

### 各 Style 规则

**NORMAL**：保留所有 `newDepth >= 0`。AutoCAD NORMAL 交替填充自动匹配原始视觉。

**OUTER**：保留 `newDepth == 0` 和 `newDepth == 1`。`clipDepth >= 1` → 删除 Hatch。

**IGNORE**：只保留 `newDepth == 0`。

### 场景汇总

| HatchStyle | clipDepth | 保留的 newDepth | 结果 Style |
|-----------|-----------|----------------|-----------|
| NORMAL | 0 | 0,1,2,3,... | NORMAL |
| OUTER | 0 | 0,1 | OUTER |
| IGNORE | 0 | 0 | IGNORE |
| NORMAL | 1 | 0,1,2,... | NORMAL |
| OUTER | 1 | 🗑 删除 | — |
| IGNORE | 1 | 0 | IGNORE |
| NORMAL | 2+ | 0,1,2,... | NORMAL |
| OUTER | 2+ | 🗑 删除 | — |
| IGNORE | 2+ | 0 | IGNORE |

---

## 四、保留外部策略

### 核心原则

> 裁剪边界 B 从 Hatch 中挖洞。保留 B 外区域，原来有填充的继续填充，没有填充的继续不填充。

### 关键行为

**IGNORE → OUTER**：IGNORE 只有外环 A，保留外部时 A\B = 环形 = 外环+内环 = OUTER。

**OUTER 双环相交 → 单环**：B 同时与 A 和 C 相交，A\B 和 C\B 可能合并为单环 → IGNORE。

### 场景汇总

| 场景 | HatchStyle | clipDepth | 行为 | 结果 Style |
|------|-----------|-----------|------|-----------|
| B在外环内 | NORMAL | 0 | A\B + B孔洞 + 其他\B | NORMAL |
| B在外环内 | OUTER | 0 | A\B + B孔洞 + C\B | OUTER |
| B在外环内 | IGNORE | 0 | A\B外环 + B内环 | **→ OUTER** |
| B在内环内 | NORMAL | 1 | A完整 + C\B + B新环 | NORMAL |
| B在内环内 | OUTER | 1 | A完整 + C\B | OUTER |
| B在内环内 | IGNORE | 1 | A完整（无影响） | IGNORE |
| B与OUTER双环相交 | OUTER | 0 | 合并为单环 | IGNORE |

---

## 五、实现方案

### 5.1 新增数据结构

```csharp
// HatchBoundaryService 新增
public class LoopBoundaryInfo {
    public int LoopIndex;
    public List<ObjectId> GeneratedEntityIds;
    public double Area;
    public int OriginalDepth;
}

// CropHatchService 新增
public class CroppedLoopInfo {
    public ObjectId CurveId;
    public int OriginalLoopIndex;
    public int OriginalDepth;
    public int NewDepth;
    public double Area;
}
```

### 5.2 重写 ProcessHatches 流程

```
for each hatchId:
  1. 提取 HatchStyle
  2. GenerateHatchBoundaryPerLoop → List<LoopBoundaryInfo>
     (每个环独立生成边界实体 + 计算面积 + 计算 originalDepth)
  3. clipDepth = 面积匹配的原始环 depth（无匹配→0）
  4. 逐环裁剪:
     for each loop:
       clippedIds = CropClosedCurveMulti(loop.EntityIds, boundaryId, keepInside)
       → CroppedLoopInfo(CurveId, OriginalDepth, Area)
  5. newDepth = originalDepth - clipDepth
  6. FilterByStyle(loops, style, keepInside, clipDepth)
  7. DetermineTargetStyle(style, keepInside, clipDepth, count)
  8. 排序: newDepth升序 + 面积降序 + 去重
  9. CloneHatchWithNewBoundaries
```

### 5.3 过滤逻辑

```csharp
// 保留内部
static FilterKeepInside(loops, style, clipDepth):
  NORMAL → newDepth >= 0
  OUTER  → clipDepth>=1 ? 空 : newDepth in [0,1]
  IGNORE → newDepth == 0

// 保留外部
static FilterKeepOutside(loops, style, clipDepth):
  NORMAL → newDepth >= 0
  OUTER  → newDepth in [0,1]
  IGNORE → newDepth == 0  (但结果Style变为OUTER)
```

### 5.4 目标 HatchStyle 确定

```csharp
static DetermineTargetStyle(srcStyle, keepInside, clipDepth, loopCount):
  if keepInside: return srcStyle  // 保持不变
  
  // 保留外部
  if srcStyle == IGNORE && clipDepth == 0:
    return OUTER  // IGNORE→OUTER
  if srcStyle == OUTER && loopCount == 1:
    return IGNORE  // 双环合并为单环
  return srcStyle
```

---

## 六、修改文件清单

| 文件 | 修改类型 | 说明 |
|------|---------|------|
| [`HatchBoundaryService.cs`](src/ServiceACAD/HatchBoundaryService.cs) | 新增方法 | `GenerateHatchBoundaryPerLoop()` |
| [`CropHatchService.cs`](src/ServiceACAD/CropHatchService.cs) | 重写 | `ProcessHatches()` + 新增过滤方法 |
| [`HatchCloneService.cs`](src/ServiceACAD/HatchCloneService.cs) | 修改 | 支持指定 targetStyle |
| [`CropHatchCommand.cs`](src/AddinsACAD/Commands/CropHatchCommand.cs) | 适配 | 适配新 ProcessHatches 签名 |
| [`plans/hatch-crop-outer-design.md`](plans/hatch-crop-outer-design.md) | 替换 | 以此文档替换 |

### 向后兼容

- `SortByContainmentHierarchy` 保留但标记为 Obsolete
- `CropHatchService.Crop()` (旧 Crop 方法) 保持不变
- `GenerateHatchBoundary()` 保持现有签名，新增 `GenerateHatchBoundaryPerLoop()`

---

## 七、风险与待确认

1. **保留外部时 B 作为孔洞环的生成**：当前 `CurveSubtractService.Subtract` 的差集结果中，B 在 A 内部的边界段已标记为 `Clip` 源并反向加入结果环。需要验证这些 Clip 段是否能正确形成闭合的孔洞环。

2. **多 Subject 裁剪时的环归属**：`CurveSubtractService.SubtractMultiSubject` 对每个 Subject 独立执行差集，结果合并。如果两个原始环裁剪后产生重叠环，需要去重。

3. **clipDepth 匹配精度**：当裁剪边界不完全等于任何一个原始环时（如用户自选边界），clipDepth 默认为 0。这种情况下的行为需要确认是否符合预期。

4. **OUTER clipDepth>=1 删除 Hatch**：当前实现已有此逻辑（[`CropHatchService.cs:174-179`](src/ServiceACAD/CropHatchService.cs:174)），保留。

---

## 八、外环内环顺序规则

裁剪后环的排序规则（传递给 `CloneHatchWithNewBoundaries` 的顺序）：

1. **newDepth 升序**：newDepth=0（最外层）排最前
2. **面积降序**：同 newDepth 内，面积大的排前面
3. **去重**：相同面积的环只保留第一个（面积容差 1e-8）

第 1 个环 = Outermost（外环），其余 = Default（内环/孔洞）。

AutoCAD 根据 HatchStyle 和环类型（Outermost/Default）自动处理填充规则。
