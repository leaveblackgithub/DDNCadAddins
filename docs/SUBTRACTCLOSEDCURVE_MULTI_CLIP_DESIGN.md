# SUBTRACTCLOSEDCURVE 多曲线差集架构分析

> 版本: 1.0 | 日期: 2026-07-04 | 状态: 设计阶段

---

## 1. 需求概述

当前 [`SubtractClosedCurveCommand`](src/AddinsACAD/Commands/SubtractClosedCurveCommand.cs:154) 仅支持 **1 个 Subject（A）减去 1 个 Clip（B）**。需要扩展为：

- **Subject（被减对象）**：仍为 1 个闭合曲线
- **Clip（减去对象）**：可以是多个闭合曲线（多选）
- 最终输出：A \ (B1 ∪ B2 ∪ ... ∪ Bn) 的闭合曲线结果

---

## 2. 现有架构分析

### 2.1 调用链路

```
SubtractClosedCurveCommand.Execute()
  ├─ CreateCurveSelection(idA) → CurveSelection (ExactSegments + ICropBoundary + Polygon)
  ├─ CreateCurveSelection(idB) → CurveSelection
  └─ SubtractClosedCurve(curveA, curveB)
       └─ CurveSubtractService.Subtract(subjectEdges, subjectBoundary, clipEdges, clipBoundary)
            ├─ 1. 将 A 每条边按 B 边界交点切分，保留不在 B 内部的子段
            ├─ 2. 将 B 每条边按 A 边界交点切分，保留在 A 内部的子段（反向标记 Clip）
            ├─ 3. ChainSegmentsIntoLoops() 头尾相连成闭合环
            └─ 返回 ExactSubtractResult.Loops（0+ 个闭合环）
```

### 2.2 关键类型

| 类型 | 位置 | 职责 |
|------|------|------|
| [`CurveSelection`](src/AddinsACAD/Commands/SubtractClosedCurveCommand.cs:36) | 命令层 | 单条曲线的封装：类型、采样多边形、精确段、裁剪边界 |
| [`CurveSubtractService`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs:21) | 核心层 | 精确差集 A\B 算法，纯数学无 CAD 依赖 |
| [`ExactSubtractResult`](src/DDNCadAddins.Core/Models/ExactSegment.cs:201) | 核心层 | 结果：闭合环列表（每个环是 ExactSegment 序列） |
| [`ExactSegment`](src/DDNCadAddins.Core/Models/ExactSegment.cs:1) | 核心层 | 精确段：Line/Arc/Ellipse，含 Source 标记（Subject/Clip） |

### 2.3 现有测试覆盖

- [`CurveSubtractServiceTests`](src/DDNCadAddins.Core.Tests/CurveSubtractServiceTests.cs:19) — 18 个测试
- [`SubtractIntersectionTests`](src/DDNCadAddins.Core.Tests/SubtractIntersectionTests.cs:13) — 10 个测试

---

## 3. 多 Clip 场景分类分析

设 Subject = A（单个闭合曲线），Clips = B₁, B₂, ..., Bₙ（n ≥ 1 个闭合曲线）。

### 3.1 场景一：所有 Clip 与 A 不相交

```
A 与每个 Bᵢ 都无交集（分离或仅边界接触）。
```

- **结果**：返回 A 原样（无减法发生）
- **算法**：遍历所有 Clip，检测到全部不相交则直接返回 A

### 3.2 场景二：Clip 之间不相交，各自独立与 A 相交

```
B₁ 在 A 内部 → A 减去一个洞
B₂ 与 A 边界相交 → A 被切掉一部分
B₁ 和 B₂ 互不重叠
```

- **结果**：A 减去多个独立区域的并集
- **算法**：顺序减法可行（Result = A \ B₁ \ B₂），每次传入上一次的结果

### 3.3 场景三：Clip 之间相互重叠

```
B₁ 和 B₂ 部分重叠，且都在 A 内部
```

- **结果**：重叠区域只应被减去一次（不能重复减）
- **算法**：需要先求 Clip 的并集 Union(B₁, B₂, ..., Bₙ)，再执行 A \ Union
- **顺序减法的陷阱**：A \ B₁ \ B₂ 在重叠区域可能产生错误，因为第二次减法时 B₂ 的部分边界已被第一次减法移除

### 3.4 场景四：★ 内外环同时与 Clip 相交（核心场景）

```
A 是一个多环闭合曲线（如 Hatch 边界），包含：
  - 外环 Outer（CCW）
  - 内环 Inner₁（CW，即孔洞）
  
Clip B 同时与外环和某个内环相交：
  - B 的一部分在外环外部（需保留）
  - B 的一部分在外环内部、内环外部（需减去）
  - B 的一部分在内环内部（内环的孔洞区域，实际是"空"的）
```

这是用户明确提到的场景：**"内外两个环都与 SUBTRACT 对象相交，最后生成一个封闭曲线"**。

**分析**：
- 内环代表"孔洞"，在差集语义中是 A 的"非实体"区域
- 当 B 跨越外环和内环时：
  - 外环外的 B 部分：不在 A 内 → 不参与减法
  - 外环内、内环外的 B 部分：在 A 实体区域内 → 应被减去
  - 内环内的 B 部分：在孔洞内 → 不在 A 实体区域内 → 不应被减去（孔洞本身已不是 A 的一部分）
- **结果**：一个闭合曲线，它的边界由 A 的外环片段 + A 的内环片段 + B 的边界片段共同组成

**这个场景需要 `CurveSubtractService` 原生支持多环 Subject**。

### 3.5 场景五：混合场景

部分 Clip 重叠，部分 Clip 与 A 边界相交，部分 Clip 跨越内外环。

---

## 4. 设计方案

### 4.1 推荐方案：分层处理

```
┌─────────────────────────────────────────────────────┐
│                  命令层 (AddinsACAD)                  │
│  SubtractClosedCurveCommand                          │
│  ├─ Step 1: 单选 Subject 曲线 A                      │
│  ├─ Step 2: 多选 Clip 曲线 B₁...Bₙ                   │
│  ├─ Step 3: 转换为 CurveSelection 列表                │
│  └─ Step 4: 调用 SubtractClosedCurve(A, [B₁...Bₙ])   │
├─────────────────────────────────────────────────────┤
│                  核心层 (DDNCadAddins.Core)           │
│  CurveSubtractService                               │
│  ├─ Subtract(subject, List<Clip>)  ← 新增多Clip重载   │
│  │   ├─ 如果 Clips 数量 = 1 → 调用现有单Clip算法      │
│  │   ├─ 如果 Clips 相互重叠 → 计算 Clip Union 后再减   │
│  │   └─ 如果 Clips 不重叠 → 顺序减法                  │
│  └─ Subtract(subject, clip)  ← 现有单Clip算法（不变）  │
└─────────────────────────────────────────────────────┘
```

### 4.2 实现步骤

#### Phase 1：命令层多选支持（最小改动）

1. [`SubtractClosedCurveCommand.Execute()`](src/AddinsACAD/Commands/SubtractClosedCurveCommand.cs:154) 中 Step 2 改为多选：
   - 使用 `PromptSelectionOptions` + `SelectionFilter`（允许 Curve 类型）
   - 允许多选，支持 CANCEL

2. 核心层新增重载：
```csharp
// CurveSubtractService.cs 新增方法
public OpResult<ExactSubtractResult> SubtractMultiple(
    IReadOnlyList<ExactSegment> subjectEdges,
    ICropBoundary subjectBoundary,
    IReadOnlyList<(IReadOnlyList<ExactSegment> edges, ICropBoundary boundary)> clips)
```

#### Phase 2：Clip Union（处理重叠 Clip）

当多个 Clip 相互重叠时，需要先计算并集。可利用现有 [`PolygonClipperService`](src/DDNCadAddins.Core/Services/PolygonClipperService.cs) 的交集能力，通过 De Morgan 律推导并集。

但考虑到 `CurveSubtractService` 使用的是 `ExactSegment`（支持弧/椭圆），而 `PolygonClipperService` 使用采样多边形，这里需要权衡：

- **方案 A**：先用采样多边形计算 Clip Union 的近似多边形，再用 `CurveSubtractService` 做精确差集
- **方案 B**：实现基于 `ExactSegment` 的精确 Union 算法（复杂度高）
- **方案 C**：顺序减法 + 重叠检测（检测到重叠时合并 Clip 采样多边形）

**推荐 Phase 2 采用方案 A**：先用多边形近似计算 Union，然后用精确曲线算法做最终差集。

#### Phase 3：多环 Subject 支持（内外环场景）

修改 `CurveSubtractService` 以支持 Subject 有多个环（外环 + 内孔环）：

- Subject 环方向约定：外环 CCW，内环 CW
- 算法调整：B 在 A 内部的判断需要考虑内环（孔洞区域不算 A 内部）
- `ChainSegmentsIntoLoops()` 需要能处理多环的连接

---

## 5. 场景矩阵与预期结果

| # | A（Subject） | B（Clips） | 关系 | 预期结果 | 优先级 |
|---|-------------|-----------|------|---------|--------|
| 1 | 单环矩形 | 1个不相交矩形 | 完全分离 | 返回 A | P0 |
| 2 | 单环矩形 | 1个内部矩形 | A 包含 B | A 带洞环 | P0（现有） |
| 3 | 单环矩形 | 1个外部矩形 | B 包含 A | 空 | P0（现有） |
| 4 | 单环矩形 | 1个相交矩形 | 部分重叠 | L 形多边形 | P0（现有） |
| 5 | 单环矩形 | 2个不相交内部矩形 | A 包含两个独立 B | A 带两个洞 | P1 |
| 6 | 单环矩形 | 2个重叠内部矩形 | A 包含重叠的 B | A 带一个合并洞 | P1 |
| 7 | 多环（外+内）| 1个与外环相交的Clip | B 仅切外环 | 单闭合曲线 | P1 |
| 8 | 多环（外+内）| 1个与内外环都相交的Clip | ★ B 跨越内外环 | ★ 单闭合曲线 | P2 |
| 9 | 单环矩形 | 2个Clip，一个内部一个相交 | 混合 | A 带洞 + 切边 | P2 |

---

## 6. 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| [`SubtractClosedCurveCommand.cs`](src/AddinsACAD/Commands/SubtractClosedCurveCommand.cs) | 修改 | Step 2 单选→多选；Step 3 遍历多个 Clip |
| [`CurveSubtractService.cs`](src/DDNCadAddins.Core/Services/CurveSubtractService.cs) | 修改 | 新增 `SubtractMultiple()` 重载 |
| [`CurveSubtractServiceTests.cs`](src/DDNCadAddins.Core.Tests/CurveSubtractServiceTests.cs) | 修改 | 新增多 Clip 测试用例 |
| [`SubtractIntersectionTests.cs`](src/DDNCadAddins.Core.Tests/SubtractIntersectionTests.cs) | 修改 | 新增多 Clip 交集测试 |

---

## 7. 建议实施顺序

1. **Phase 1 先行**（命令层多选 + 顺序减法），覆盖场景 1-5
2. **Phase 2 后续**（Clip Union），覆盖场景 6
3. **Phase 3 最后**（多环 Subject），覆盖场景 7-9

---

## 8. 待确认问题

1. **Clip 重叠时的语义**：重叠区域减一次还是减多次？→ 建议减一次（并集语义）
2. **多环 Subject 的来源**：是 Hatch 边界提取的多环结果，还是用户直接选择的多条曲线？→ 建议先支持前者（配合 CROPHATCH）
3. **Phase 1 是否先实施**：命令层多选 + 顺序减法（Clip 不重叠时正确），等确认后再做 Phase 2/3？
