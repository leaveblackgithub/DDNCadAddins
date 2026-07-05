# HatchStyle.Outer 裁剪正确逻辑设计

> 版本：1.0.0 | 日期：2026-07-05 | 状态：设计中

---

## 一、HatchStyle.Outer 语义

### 1.1 AutoCAD HatchStyle 定义

| HatchStyle | 填充行为 | 环数 |
|------------|---------|------|
| `Normal` | 交替填充：外环填充，内环不填充，内环内的环再填充… | 所有环参与 |
| `Outer` | **只填充最外环与第一个内环之间的区域**，更内层的环全部忽略 | 仅最外2个环有效 |
| `Ignore` | 填充最外环内部全部区域，忽略所有内环 | 仅最外1个环有效 |

### 1.2 用户场景

原始 Hatch 有 3 个环：
- **环 A**（最外环）：大矩形
- **环 B**（第一个内环 / 孔洞）：中圆
- **环 C**（环 B 内的岛 / 被忽略）：小圆

`HatchStyle.Outer` 填充区域 = A \ B（大矩形减去中圆），环 C 被忽略。

### 1.3 裁剪后的正确行为

裁剪后应填充在**裁剪后的 A 与裁剪后的 B 之间**。环 C 裁剪后的结果仍然忽略。

---

## 二、当前流程分析

### 2.1 当前 ProcessHatches 流程

```
1. GenerateHatchBoundary → 生成边界实体（所有环或部分环）
2. CropClosedCurveMulti → 对每个边界实体独立裁剪
3. CloneHatchWithNewBoundaries → 用裁剪结果曲线重建 Hatch
```

### 2.2 当前问题

| 问题 | 原因 |
|------|------|
| 只生成前2个环 | `GenerateHatchBoundary` 中 `HatchStyle.Outer` 限制 `loopEnd = Math.Min(2, loopCount)` |
| 环顺序不保证 | AutoCAD Hatch loop 顺序不保证外环在前 |
| 裁剪后内外环关系可能改变 | 裁剪边界可能切割外环但不切割内环，或反之 |
| 重建时内外环类型错误 | `CloneHatchWithNewBoundaries` 无法知道哪个是外环哪个是内环 |

---

## 三、正确逻辑设计

### 3.1 核心原则

> **裁剪后的 Hatch 应填充在裁剪后的最外环与裁剪后的第一个内环之间。**

这意味着：
1. 需要生成**所有环**的边界实体（包括被忽略的环 C）
2. 对每个环独立裁剪
3. 裁剪后，**按面积从大到小排序**，识别最外环和第一个内环
4. 用排序后的前2个环重建 Hatch（HatchStyle.Outer）
5. 更内层的环（如果有）仍然忽略

### 3.2 为什么需要生成所有环

如果只生成前2个环，但环顺序是 A→C→B（外环→岛→孔洞），则生成的边界实体是 A 和 C，缺少真正的孔洞 B。裁剪后无法正确重建。

生成所有环后，裁剪+排序可以正确识别最外环和第一个内环。

### 3.3 为什么按面积排序

裁剪后，最外环的面积最大，第一个内环的面积次大。按面积降序排序后：
- 第1个 = 裁剪后的最外环（Outermost）
- 第2个 = 裁剪后的第一个内环（Default / 孔洞）
- 第3个及以后 = 被忽略的环

**例外**：如果裁剪边界将外环切割成多个碎片，面积最大的碎片是最外环。

---

## 四、边界条件分析

### 4.1 裁剪方向

| 命令 | keepInside | 语义 | 对 Hatch 环的影响 |
|------|-----------|------|------------------|
| CROPOUTSIDE | true | 保留裁剪边界内部 | 每个环取与裁剪边界的交集 |
| CROPINSIDE | false | 保留裁剪边界外部 | 每个环取与裁剪边界的差集 |

### 4.2 CROPOUTSIDE（保留内部 = 交集 A ∩ Clip）边界条件

| # | 场景 | 环 A（外环） | 环 B（内环/孔洞） | 环 C（岛/忽略） | 裁剪后结果 | 正确行为 |
|---|------|------------|-----------------|---------------|-----------|---------|
| 1 | Clip 完全包含 A | A 不变 | B 不变 | C 不变 | 3个环不变 | 填充 A\B，忽略 C |
| 2 | Clip 在 A 内、B 外 | A ∩ Clip = Clip 形状 | B ∩ Clip = 空 | C ∩ Clip = 空 | 1个环（Clip 形状） | 填充 Clip 形状（无孔洞） |
| 3 | Clip 在 A 内、B 内、C 外 | A ∩ Clip = Clip 形状 | B ∩ Clip = Clip 形状 | C ∩ Clip = 空 | 2个环（相同形状） | 填充 Clip 形状减去 Clip 形状 = 空 |
| 4 | Clip 在 A 内、B 内、C 内 | A ∩ Clip = Clip | B ∩ Clip = Clip | C ∩ Clip = Clip | 3个环（相同形状） | 填充 Clip 减去 Clip = 空 |
| 5 | Clip 跨越 A 和 B | A 部分保留 | B 部分保留 | C 可能保留 | 2-3个环 | 填充裁剪后A \ 裁剪后B |
| 6 | Clip 完全在 A 外 | 空 | 空 | 空 | 0个环 | 删除 Hatch |
| 7 | Clip 只与 A 相交 | A 部分保留 | 空 | 空 | 1个环 | 填充裁剪后A（无孔洞） |
| 8 | Clip 只与 B 相交、不与 A 相交 | 空 | B 部分保留 | C 可能保留 | 1-2个环 | 无外环 → 删除 Hatch |

### 4.3 CROPINSIDE（保留外部 = 差集 A \ Clip）边界条件

| # | 场景 | 环 A（外环） | 环 B（内环/孔洞） | 环 C（岛/忽略） | 裁剪后结果 | 正确行为 |
|---|------|------------|-----------------|---------------|-----------|---------|
| 1 | Clip 完全在 A 外 | A 不变 | B 不变 | C 不变 | 3个环不变 | 填充 A\B，忽略 C |
| 2 | Clip 在 A 内、B 外 | A \ Clip = 带洞环 | B 不变 | C 不变 | 3个环 | 填充(A\Clip)\B |
| 3 | Clip 在 A 内、B 内、C 外 | A \ Clip = 带洞环 | B \ Clip = 带洞环 | C 不变 | 3个环 | 填充(A\Clip)\(B\Clip) |
| 4 | Clip 在 A 内、B 内、C 内 | A \ Clip = 带洞环 | B \ Clip = 带洞环 | C \ Clip = 带洞环 | 3个环 | 填充(A\Clip)\(B\Clip)，忽略 C\Clip |
| 5 | Clip 跨越 A 和 B | A 部分保留 | B 部分保留 | C 可能保留 | 2-3个环 | 填充裁剪后A \ 裁剪后B |
| 6 | Clip 完全包含 A | 空 | 空 | 空 | 0个环 | 删除 Hatch |
| 7 | Clip 只与 A 相交 | A 部分保留 | B 不变 | C 不变 | 2-3个环 | 填充裁剪后A \ B |
| 8 | Clip 只与 B 相交、不与 A 相交 | A 不变 | B 部分保留 | C 可能保留 | 2-3个环 | 填充 A \ 裁剪后B |

### 4.4 特殊情况

| # | 场景 | 分析 | 正确行为 |
|---|------|------|---------|
| 1 | 裁剪后外环面积 < 内环面积 | 不可能（外环包含内环，裁剪后仍应包含） | — |
| 2 | 裁剪后外环和内环面积相等 | Clip 恰好同时切割 A 和 B，且切割后形状相同 | 填充区域为零 → 删除 Hatch |
| 3 | 裁剪后只有1个环 | 外环保留、内环消失（或反之） | 填充该环（无孔洞），或删除 Hatch（无外环） |
| 4 | 裁剪后0个环 | 所有环都被裁剪掉 | 删除 Hatch |
| 5 | 裁剪后外环分裂成多个 | 差集运算可能将外环切成多个碎片 | 取面积最大的碎片作为外环 |
| 6 | HatchStyle.Normal | 交替填充，所有环参与 | 需要不同的重建策略（不在本次修复范围） |
| 7 | HatchStyle.Ignore | 只填充最外环 | 只需最外环裁剪结果，忽略所有内环 |

---

## 五、实现方案

### 5.1 修改 GenerateHatchBoundary

**移除 HatchStyle 限制，生成所有环：**

```csharp
// 修改前
case HatchStyle.Outer:
    loopStart = 0; loopEnd = Math.Min(2, loopCount); break;

// 修改后
loopStart = 0;
loopEnd = loopCount;  // 生成所有环
```

### 5.2 修改 ProcessHatches

**裁剪后按面积排序，取前2个环重建 Hatch：**

```
1. GenerateHatchBoundary → 生成所有环的边界实体
2. CropClosedCurveMulti → 裁剪所有边界实体
3. 对裁剪结果按面积降序排序
4. 如果结果数 >= 2：
   - 第1个 = Outermost（外环）
   - 第2个 = Default（内环/孔洞）
   - 忽略其余环
5. 如果结果数 == 1：
   - 唯一环 = Outermost（无孔洞）
6. 如果结果数 == 0：
   - 删除原 Hatch
```

### 5.3 修改 CloneHatchWithNewBoundaries

**接受排序后的边界，第1个 Outermost，第2个 Default：**

```csharp
// 第1个环 = Outermost（外环）
// 第2个环 = Default（内环/孔洞）
// 第3个及以后 = 不传入（忽略）
var loopType = (i == 0) ? HatchLoopTypes.Outermost : HatchLoopTypes.Default;
```

### 5.4 面积计算方法

在 `ProcessHatches` 中，裁剪后对每个 `CreatedEntityIds` 中的 Polyline 计算面积：

```csharp
// 使用 Polyline.GetArea() 或 Shoelace 公式
var pline = ts.GetObject<Polyline>(id);
double area = pline.Area;
```

按面积降序排序后取前2个。

### 5.5 HatchStyle.Ignore 的处理

如果原始 Hatch 是 `HatchStyle.Ignore`：
- 只需要最外环裁剪后的结果
- 按面积排序后取第1个（Outermost），忽略其余

### 5.6 HatchStyle.Normal 的处理

如果原始 Hatch 是 `HatchStyle.Normal`：
- 所有环都参与交替填充
- 按面积降序排序后全部传入
- 第1个 = Outermost，其余 = Default
- AutoCAD 的 HatchStyle.Normal 会自动交替填充

---

## 六、实施步骤

1. **`GenerateHatchBoundaryCommand.cs`** — 移除 HatchStyle 环数限制，生成所有环
2. **`CropHatchCommand.cs` ProcessHatches** — 裁剪后按面积排序，按 HatchStyle 取相应数量的环
3. **`CloneHatchCommand.cs` CloneHatchWithNewBoundaries** — 第1个 Outermost，第2个 Default，其余不传入
4. 编译验证
5. Git 提交推送