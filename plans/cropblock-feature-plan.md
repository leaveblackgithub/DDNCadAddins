# CROPBLOCK 功能计划

> 版本：1.1.0 | 日期：2026-07-05 | 状态：规划中
> 前提：暂时不考虑 XCLIP 处理，需要考虑嵌套块

---

## 一、现状分析

### 1.1 当前 CropBlockService 占位实现

[`CropBlockService.cs`](src/ServiceACAD/CropBlockService.cs) 当前是**占位实现**，仅做边界框分类：

```
BlockReference 的 GeometricExtents 包围盒
    ↓ ClassifyBoundingBox(minPt, maxPt)
    ├── Inside  → 保留
    ├── Outside → 删除
    └── Intersects → 保留（不处理）
```

**缺陷：**
- 不对块内容做任何裁剪，相交时直接保留整个块
- 不处理嵌套块
- 不处理块内实体级别的裁剪
- 没有独立命令，仅作为 CropService 调度器中的 NonCurve 处理器

### 1.2 现有 CROP 系列命令模式

| 命令 | 文件 | 模式 |
|------|------|------|
| `CROPARC` / `CROPALLARCS` | [`CropArcCommand.cs`](src/AddinsACAD/Commands/CropArcCommand.cs) | 单选/全选 + 方向选择 |
| `CROPCIRCLE` / `CROPALLCIRCLES` | [`CropCircleCommand.cs`](src/AddinsACAD/Commands/CropCircleCommand.cs) | 单选/全选 + 方向选择 |
| `CROPLINE` / `CROPALLLINES` | [`CropLineCommand.cs`](src/AddinsACAD/Commands/CropLineCommand.cs) | 单选/全选 + 方向选择 |
| `CROPPOLYLINE` / `CROPALLPOLYLINES` | [`CropPolylineCommand.cs`](src/AddinsACAD/Commands/CropPolylineCommand.cs) | 单选/全选 + 方向选择 |

**通用命令结构（三部分）：**
1. **输入获取**（命令层）：选择边界曲线 → 选择目标实体（手动/全部）→ 询问裁剪方向
2. **主体逻辑**（服务层）：执行裁剪操作
3. **输出显示**（命令层）：显示统计结果 + TestRecorder 记录

### 1.3 BlockExploder 现有能力

[`BlockExploder.cs`](src/ServiceACAD/BlockExploder.cs) 已具备：
- 爆炸块参照为基本实体（含嵌套块递归展开）
- 处理属性引用 → 文本转换
- 处理嵌套块的变换矩阵累加（位置、缩放、旋转）
- 复制 XCLIP 状态
- 图层/颜色/线型属性继承

---

## 二、目标架构

### 2.1 CROPBLOCK 核心策略：爆炸-裁剪模式

由于不考虑 XCLIP，CROPBLOCK 采用**爆炸→裁剪**策略：

```
BlockReference 选中
    ↓ 包围盒分类
    ├── 完全在保留侧 → 保留原 BlockReference
    ├── 完全在删除侧 → 删除原 BlockReference
    └── 与边界相交 → 爆炸为子实体
                          ↓
                    对每个子实体执行 ICropService.Crop()
                          ↓
                    保留侧的子实体添加到模型空间
                    删除侧的子实体丢弃
```

### 2.2 命令设计

遵循现有 CROP 系列命名规范：

| 命令名 | 功能 | 对应模式 |
|--------|------|---------|
| `CROPBLOCK` | 手动选择块参照后裁剪 | 单选模式 |
| `CROPALLBLOCKS` | 自动选择所有块参照后裁剪 | 全选模式 |

两个命令共享同一个执行入口，通过 `selectAllBlocks: bool` 参数区分。

### 2.3 分层设计

```
AddinsACAD（命令层）
├── CropBlockCommand.cs          # 新增：CROPBLOCK / CROPALLBLOCKS 命令
│   ├── SelectBoundaryCurve()    # 复用 CropInsideCommand 的模式
│   ├── SelectBlocksToCrop()     # 手动选择块参照（INSERT 过滤器）
│   ├── AskCropDirection()       # 复用 CropArcCommand 的模式
│   └── ExecuteCropBlock()       # 输出显示

ServiceACAD（服务层）
├── CropBlockService.cs          # 改造：从占位升级为完整实现
│   ├── CropBlocksInside()       # 保留内部
│   ├── CropBlocksOutside()      # 保留外部
│   └── CropBlockInternal()      # 核心：爆炸→裁剪→重组
└── CropService.cs               # 不变：BlockReference 仍在 nonCurveHandlers

DDNCadAddins.Core（核心层）
└── （不变，使用现有 ICropBoundary / ICropGeometryService）
```

---

## 三、边界可能性全景分析

### 3.1 块参照状态边界

| # | 场景 | 分类 | 预期行为 | 风险等级 |
|---|------|------|---------|---------|
| 1 | 正常块参照（无嵌套、无属性） | 正常路径 | 包围盒分类→保留/删除/爆炸裁剪 | 低 |
| 2 | 块参照含属性引用（AttributeReference） | 属性处理 | 爆炸时属性→文本，文本参与裁剪 | 中 |
| 3 | 块参照在锁定图层上 | 访问限制 | 跳过，计入 SkippedCount，输出警告 | 低 |
| 4 | 块参照在冻结/关闭图层上 | 可见性 | 跳过（不可见不应裁剪），计入 SkippedCount | 低 |
| 5 | 已擦除/无效 ObjectId | 脏数据 | 跳过，计入 SkippedCount | 低 |
| 6 | 块参照使用 ByLayer 属性 | 属性继承 | 爆炸后子实体继承正确图层属性 | 中 |
| 7 | 块参照非均匀缩放（X≠Y≠Z） | 几何变换 | 爆炸时正确应用 ScaleFactors | 中 |
| 8 | 块参照有旋转角度 | 几何变换 | 爆炸时正确应用 Rotation | 低 |
| 9 | 块参照缩放因子为零或负值 | 无效变换 | 跳过，计入 SkippedCount，输出警告 | 中 |
| 10 | 块定义为空（无子实体） | 空块 | 按包围盒分类（空块无几何，包围盒可能为零） | 低 |
| 11 | 块定义无效/已删除 | 脏数据 | 跳过，计入 SkippedCount | 低 |
| 12 | 匿名块（*U, *D, *E, *T, *X 前缀） | 特殊块 | 正常处理（与普通块一致） | 低 |
| 13 | 动态块参照 | 动态属性 | 爆炸后丢失动态特性（AutoCAD 标准行为） | 中 |
| 14 | 带可见性状态的动态块 | 动态属性 | 按当前可见状态爆炸，不可见实体不出现 | 中 |

### 3.2 嵌套块边界

| # | 场景 | 分类 | 预期行为 | 风险等级 |
|---|------|------|---------|---------|
| 15 | 单层嵌套（块A含块B） | 核心功能 | 块A爆炸后，块B成为独立BlockReference，对块B再次包围盒分类→递归处理 | 高 |
| 16 | 多层嵌套（3层以上） | 核心功能 | 递归爆炸直到所有嵌套块被处理 | 高 |
| 17 | 循环引用（A→B→A） | 异常路径 | 检测循环引用，设置最大递归深度（建议≤10），超出则保留原块 | 高 |
| 18 | 嵌套块缩放/旋转与父块累加 | 几何变换 | BlockExploder 已正确处理（Scale累乘 + Rotation累加） | 中 |
| 19 | 嵌套块含属性 | 属性处理 | 每层爆炸时属性→文本，文本参与裁剪 | 中 |
| 20 | 嵌套块在子块定义中位于不同图层 | 图层处理 | 爆炸后继承块定义中的图层设置 | 中 |
| 21 | 同一块定义在多个嵌套层级出现 | 递归处理 | 每个实例独立处理（不共享状态） | 中 |
| 22 | 嵌套块有 XCLIP（即使暂不处理） | 降级处理 | 检测到 XCLIP 时跳过裁剪，保留原块参照，输出提示 | 中 |
| 23 | 嵌套块爆炸后与边界的关系判断 | 核心逻辑 | 每个爆炸后的子实体独立判断与边界的关系 | 高 |

### 3.3 空间关系边界

| # | 场景 | 分类 | 预期行为 | 风险等级 |
|---|------|------|---------|---------|
| 24 | 块包围盒完全在边界内部 | 保留 | 保留原 BlockReference，KeptCount++ | 低 |
| 25 | 块包围盒完全在边界外部 | 删除 | 删除原 BlockReference，DeletedCount++ | 低 |
| 26 | 块包围盒与边界相交 | 爆炸裁剪 | 爆炸→子实体逐项裁剪→保留侧添加到模型空间 | 高 |
| 27 | 块包围盒刚好接触边界线 | 边界判断 | 根据 keepInside 方向：Inside模式保留、Outside模式删除 | 中 |
| 28 | 块包围盒退化为点（零尺寸） | 退化几何 | 按单点判断：点在边界内→保留，否则→删除 | 低 |
| 29 | 块在边界顶点处 | 边界判断 | 顶点视为边界上，与 OnBoundary 处理一致 | 低 |
| 30 | 块属性文本超出包围盒 | 视觉范围 | 包围盒基于 GeometricExtents（含属性），属性文本参与判断 | 中 |

### 3.4 块内实体类型边界

| # | 场景 | 分类 | 预期行为 | 风险等级 |
|---|------|------|---------|---------|
| 31 | 块含 Curve（Line/Polyline/Arc/Circle/Spline/Ellipse） | 正常路径 | 通过 CropService 的 _curveHandlers 分发处理 | 低 |
| 32 | 块含 DBText/MTEXT | 正常路径 | 通过 CropService 的 _nonCurveHandlers 处理 | 低 |
| 33 | 块含 Dimension | 正常路径 | 通过 CropService 的 _nonCurveHandlers 处理 | 低 |
| 34 | 块含 Hatch | 正常路径 | 通过 CropService 的 _nonCurveHandlers 处理 | 低 |
| 35 | 块含 DBPoint | 正常路径 | 通过 CropService 的 _nonCurveHandlers 处理 | 低 |
| 36 | 块含 Solid | 正常路径 | 通过 CropService 的 _nonCurveHandlers 处理 | 低 |
| 37 | 块含嵌套 BlockReference | 递归处理 | 递归进入 CROPBLOCK 逻辑 | 高 |
| 38 | 块含 AttributeDefinition（非 AttributeReference） | 特殊实体 | 爆炸时被 BlockExploder 跳过（不转换） | 低 |
| 39 | 块含 Wipeout | 特殊实体 | CropService 无 Wipeout 处理器，走 SkippedCount | 低 |
| 40 | 块含 MLine/Leader/Polyline3d | 正常路径 | 通过 CropService 分发处理 | 低 |
| 41 | 块含 XLine/Ray（无限构造线） | 特殊实体 | CropService 无对应处理器，走 SkippedCount | 中 |

### 3.5 边界类型交互

| # | 场景 | 分类 | 预期行为 | 风险等级 |
|---|------|------|---------|---------|
| 42 | 多边形边界（Polyline） | 精确 | 使用 PolygonCropBoundary，无精度损失 | 低 |
| 43 | 圆形边界（Circle） | 精确 | 使用 CircleCropBoundary，解析求交 | 低 |
| 44 | 椭圆边界（Ellipse） | 精确 | 使用 EllipseCropBoundary，解析求交 | 低 |
| 45 | 样条边界（Spline） | 采样 | 使用 SplineCropBoundary，采样多边形代理 | 中 |

### 3.6 性能与极限边界

| # | 场景 | 分类 | 预期行为 | 风险等级 |
|---|------|------|---------|---------|
| 46 | 超大块（含数百子实体） | 性能 | 逐个处理子实体，可能较慢但不应崩溃 | 中 |
| 47 | 深嵌套 + 大量兄弟节点 | 性能 | 递归深度×兄弟节点数，需限制递归深度 | 高 |
| 48 | 全选模式无块可处理 | 空结果 | 输出"未找到块参照"提示 | 低 |
| 49 | 全选模式所有块都在锁定图层 | 全部跳过 | 输出"所有块参照所在图层均已锁定" | 低 |
| 50 | 混合场景：部分相交+部分全内+部分全外 | 综合 | 统计正确累加各项计数 | 中 |

### 3.7 命令交互边界

| # | 场景 | 分类 | 预期行为 | 风险等级 |
|---|------|------|---------|---------|
| 51 | 用户在边界选择时按 ESC | CANCEL | 静默退出，不抛异常 | 低 |
| 52 | 用户在块选择时按 ESC | CANCEL | 静默退出，不抛异常 | 低 |
| 53 | 用户在方向选择时按 ESC | CANCEL | 静默退出，不抛异常 | 低 |
| 54 | 选择的边界曲线未闭合 | 输入验证 | 提示"边界曲线未闭合"，重新选择 | 低 |
| 55 | 手动选择的实体中包含非 BlockReference | 过滤器 | SelectionFilter 限定 INSERT 类型，自动排除 | 低 |
| 56 | 手动选择的块中包含边界自身 | 自引用 | 排除边界自身的 ObjectId | 低 |
| 57 | 边界是块参照（非 Curve） | 输入验证 | SelectionFilter 限定 Curve 类型，自动排除 | 低 |

---

## 四、关键设计决策

### 4.1 爆炸后不重新打包为块

**决策**：爆炸后的子实体直接添加到模型空间，**不重新打包为块**。

**理由**：
- 重新打包会创建新的块定义，污染块表
- 如果同一块定义的多个实例被裁剪，每个都会产生不同的裁剪结果，无法合并为同一块定义
- 与 AutoCAD 的 XCLIP 行为一致（XCLIP 也不重新打包）

### 4.2 递归深度限制

**决策**：最大递归深度 = **10 层**，超出则保留原块参照并输出警告。

**理由**：
- 防止循环引用导致无限递归
- 实际图纸中嵌套深度极少超过 5 层
- 10 层提供了充足的安全边际

### 4.3 XCLIP 块降级处理

**决策**：虽然暂不实现 XCLIP 裁剪，但需**检测并跳过** XCLIP 块，保留原块参照并输出提示。

**理由**：
- 避免爆炸 XCLIP 块后丢失裁剪信息
- 为未来 CROPBLOCK + XCLIP 功能预留入口
- [`BlockService.IsXclipped()`](src/ServiceACAD/BlockService.cs:54) 已有现成检测方法

### 4.4 属性引用处理

**决策**：爆炸时属性引用转换为 DBText，参与裁剪。

**理由**：
- [`BlockExploder.Explode()`](src/ServiceACAD/BlockExploder.cs:28) 已实现此逻辑
- 属性文本是块的视觉组成部分，应参与裁剪

### 4.5 包围盒分类沿用现有逻辑

**决策**：块参照级别的包围盒分类沿用 [`CropUtils.ProcessNonCurve()`](src/ServiceACAD/CropUtils.cs:37) 逻辑。

**理由**：
- 包围盒快速分类是高效的粗筛手段
- 只有相交时才触发昂贵的爆炸+裁剪路径
- 与现有 NonCurve 处理保持一致

---

## 五、实施计划

### Phase 1：Core 层（无变化）

- 无需修改 Core 层，现有 [`ICropBoundary`](src/DDNCadAddins.Core/Interfaces/ICropBoundary.cs) / [`ICropGeometryService`](src/DDNCadAddins.Core/Interfaces/ICropGeometryService.cs) 已满足需求

### Phase 2：ServiceACAD 层 — CropBlockService 改造

1. **改造 [`CropBlockService.cs`](src/ServiceACAD/CropBlockService.cs)**
   - 新增 `ICropBoundary` 重载（配合精确边界）
   - 新增 `CropBlockInternal()` 核心方法：爆炸→子实体裁剪→结果汇总
   - 新增递归嵌套处理逻辑
   - 新增 XCLIP 检测与降级处理
   - 新增递归深度计数器

2. **可选：扩展 [`CropBlockResult`](src/ServiceACAD/CropBlockService.cs:9)**
   - 新增 `ExplodedCount`：被爆炸的块数量
   - 新增 `NestedBlockHandledCount`：嵌套块处理数量

### Phase 3：AddinsACAD 层 — CropBlockCommand 新增

1. **新增 [`CropBlockCommand.cs`](src/AddinsACAD/Commands/CropBlockCommand.cs)**
   - `[CommandMethod("CROPBLOCK")]` — 手动选择块

---

## 六、EXPLODEASSHOWN 重构记录

### 6.1 分析结论

**EXPLODEASSHOWN 只炸开一层**，嵌套块仅做变换累加后保留为新的 `BlockReference`，不递归爆炸。详见 [`BlockExploder.ProcessExplodedEntities()`](src/ServiceACAD/BlockExploder.cs:144-159)：

```csharp
// 嵌套块 → 创建新 BlockReference（不递归爆炸）
if (obj is BlockReference nestedBlockRef)
{
    var newBlockRef = new BlockReference(transformedPosition, nestedBlockRef.BlockTableRecord)
    {
        ScaleFactors = new Scale3d(...),  // 累乘
        Rotation = nestedBlockRef.Rotation + blockRef.Rotation,  // 累加
        ...
    };
    entity = newBlockRef;
}
```

### 6.2 重构内容

**违反三层架构问题**：原 [`ExplodeAsShownCommand.ExplodeSelected()`](src/AddinsACAD/Commands/ExplodeAsShownCommand.cs:19-93) 在命令层混入了遍历、爆炸、统计的主体逻辑。

**重构方案**（已实施）：

1. **[`BlockExploder.cs`](src/ServiceACAD/BlockExploder.cs) 新增：**
   - `ExplodeMultipleResult` 类 — 批量爆炸结果（SuccessCount / TotalExploded / Details / FailedBlocks）
   - `ExplodeDetail` 类 — 单个块爆炸详情（BlockName + Stats）
   - `ExplodeMultiple()` 静态方法 — 批量爆炸多个块参照（支持取消检测）

2. **[`ExplodeAsShownCommand.cs`](src/AddinsACAD/Commands/ExplodeAsShownCommand.cs) 简化：**
   - 命令层严格分为三部分：**输入获取 → 主体逻辑（一行调用）→ 输出显示**
   - 主体逻辑委托给 `BlockExploder.ExplodeMultiple()`
   - 移除 `using System.Collections.Generic`（不再需要 `List<string>`）

### 6.3 对 CROPBLOCK 的复用价值

`BlockExploder.ExplodeMultiple()` 可直接被 [`CropBlockService`](src/ServiceACAD/CropBlockService.cs) 复用：

- CROPBLOCK 需要在爆炸后对子实体执行裁剪
- `ExplodeMultipleResult.Details` 中包含每个块爆炸后的 `Stats.EntityIds`
- 这些 `EntityIds` 可作为裁剪的输入，传递给 `CropService.CropInside()/CropOutside()`
- 取消检测（`ICommandCancellation`）同样适用于 CROPBLOCK 批量处理场景
