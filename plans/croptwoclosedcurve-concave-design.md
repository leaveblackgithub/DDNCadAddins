# CROPTWOCLOSEDCURVE + 凹字形测试图形 — 架构设计

**版本**: 1.0.0 | **日期**: 2026-07-07 | **作者**: Zoo (Architect)

---

## 1. 需求背景

### 问题
奇偶环重叠裁剪（偶数环裁剪奇数环）在 `ProcessHatches` 中不成功，怀疑是精度问题。需要独立的测试命令来调试两个环同时被一个边界裁剪的场景。

### 目标
1. **CROPTWOCLOSEDCURVE** — 手动选择两个闭合曲线（外环+内环）和一个裁剪边界，同时裁剪并绘制结果
2. **凹字形测试图形生成** — 在 `CropTestsCommand` 中增加凹字形环的生成，用于复现局部重叠场景

---

## 2. 架构分析

### 2.1 现有架构（不变）
```
AddinsACAD (命令层)
  ├─ CropClosedCurveCommand.cs        → CROPCLOSEDCURVE 命令（UI交互）
  ├─ CropTestsCommand.cs              → CROPTESTS 批量测试入口
  └─ CropHatchCommand.cs              → CROPHATCH（调用 ProcessHatches）
        ↓ 调用
ServiceACAD (服务层)
  ├─ CropClosedCurveService.cs        → CropClosedCurveMulti（核心裁剪逻辑）
  └─ CurveToExactSegmentConverter.cs  → DrawExactSegments（绘制结果环）
        ↓ 调用
DDNCadAddins.Core (核心层)
  └─ CurveSubtractService.cs          → Subtract/Intersect（几何运算）
```

### 2.2 关键发现
`CropClosedCurveService.CropClosedCurveMulti` 已经支持**多个 Subject**（任意数量），内部调用 `CurveSubtractService.SubtractMultiSubject` 或 `IntersectMultiSubject`，所有 Subject 的结果环合并输出。

**CROPTWOCLOSEDCURVE 不需要新的 Service 层或 Core 层代码** — 它是 `CropClosedCurveMulti` 的 2-Subject 特化命令，纯粹为调试而生。

---

## 3. 文件变更清单

### 3.1 新增文件

| 文件 | 层 | 说明 |
|------|-----|------|
| `src/AddinsACAD/Commands/CropTwoClosedCurveCommand.cs` | 命令层 | CROPTWOCLOSEDCURVE 命令 |

### 3.2 修改文件

| 文件 | 层 | 变更 |
|------|-----|------|
| `src/AddinsACAD/Commands/CropTestsCommand.cs` | 命令层 | 新增凹字形测试图形生成选项 |
| `src/AddinsACAD/AddinsACAD.csproj` | — | 注册 `CropTwoClosedCurveCommand.cs` |

### 3.3 不变文件
- `CropClosedCurveService.cs` — 无需变更
- `CurveSubtractService.cs` — 无需变更
- 所有 Core 层文件 — 无需变更

---

## 4. 详细设计

### 4.1 CROPTWOCLOSEDCURVE 命令

```
交互流程:
  1. 选择裁剪边界曲线 B（Clip，单选）
  2. 选择外环曲线 A₁（Subject 1，单选）
  3. 选择内环曲线 A₂（Subject 2，单选）
  4. 询问裁剪方向（保留内部/外部）
  5. 调用 CropClosedCurveService.CropClosedCurveMulti([A₁, A₂], B, keepInside)
  6. 输出结果：每个环的顶点数、面积、颜色
```

**关键设计决策：**
- 三个环用不同颜色绘制：外环=黄色(2)、内环=青色(4)、裁剪结果=绿色(3)
- 保留所有中间产物（不删除，供调试）
- 输出 TestRecord（UID 格式：CROPTWOCLOSEDCURVE）

### 4.2 凹字形测试图形

```
凹字形（U-shaped concave polygon）:
  顶点序列: (0,0) → (100,0) → (100,30) → (30,30) → (30,70) → (100,70)
           → (100,100) → (0,100) → 闭合
```

**关键特征：**
- 凹入区域 (30,30)~(100,70) 可以放置另一个环（外环或内环）
- 当内环（孔洞）在此凹入区域与外环有局部重叠时，恰好复现奇偶环重叠场景
- 此形状同时可用于 `CROPTESTS` 中作为 Hatch 边界生成测试

**生成方式：** 在 `CropTestsCommand` 中添加 `GenerateConcaveRing` 静态方法，通过 `MANUALCMDTESTS` → `CT`（新增）或直接 `CT` 命令调用。

### 4.3 SOLID 度量

| 原则 | 度量 | CropTwoClosedCurveCommand |
|------|------|--------------------------|
| SRP | <200行, <20行/方法 | 约 120 行，4 个方法（Execute, SelectClosedCurve, AskCropDirection, 输出） |
| ISP | 不新增接口 | 无接口变更 |
| DIP | 依赖 Service 层抽象 | 依赖 `CropClosedCurveService`（静态类，已有） |

---

## 5. 命令注册

在 `CropTestsCommand` 的命令映射表中新增：

```
现有: A=ARC, B=CROPCLOSEDCURVE, C=CIRCLE, D=CROPPOLYLINE, E=CROPELLIPSE, ...
新增: U=CROPTWOCLOSEDCURVE (U = 2 = Two)
新增: V=GenerateConcaveRing (V = 凹)
```

---

## 6. 验证计划

1. 编译：`MSBuild /t:Rebuild`
2. 纯逻辑测试：现有 `CurveSubtractServiceTests`（310 个，已有）
3. CAD 手工测试：
   - 在 AutoCAD 中用凹字形生成两个环
   - 执行 `CROPTWOCLOSEDCURVE` 观察裁剪结果
   - 检查重叠边界是否严格对齐

---

## 7. 风险与注意事项

- **CropTwoClosedCurveCommand 仅用于调试**，不是生产命令
- 凹字形生成应放在 `CropTestsCommand` 中（测试入口），不污染命令列表
- 保留临时边界实体（不删除），与当前 `ProcessHatches` 的调试模式一致
