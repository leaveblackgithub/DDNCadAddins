# CropBlock 增强架构设计

> 版本: 1.0 | 日期: 2026-07-06 | 状态: 设计阶段

---

## 1. 需求概述

增强块参照（BlockReference）裁剪功能，提供**两种裁剪策略**：

### 策略 A：爆炸裁剪（现有方式，改进版）
块与边界相交时爆炸 → 裁剪子实体 → 保留块结构部分

### 策略 B：XClip 裁剪（新增）
块与边界相交时，**不爆炸**，而是对块参照施加 XClip 边界，保留块结构完整性

### 用户可配置选项
- 策略选择（Explode / XClip / Auto）
- 嵌套块处理方式（跳过 / 递归处理 / 保留原样）
- 精确几何分类（替代包围盒分类）

---

## 2. 架构变更总览

```
┌─────────────────────────────────────────────────────────────────────┐
│                        命令层 (AddinsACAD)                           │
│  CropBlockCommand (新增)                                            │
│  ├─ 用户选择策略 (Explode/XClip/Auto)                               │
│  ├─ 用户选择块参照                                                  │
│  └─ 调用 CropBlockService.CropBlocks()                              │
├─────────────────────────────────────────────────────────────────────┤
│                    AutoCAD 服务层 (ServiceACAD)                      │
│  CropBlockService (增强)                                            │
│  ├─ 精确几何分类 (替代包围盒分类)                                    │
│  ├─ ExplodeAndCropChildren() (改进)                                 │
│  │   └─ 递归处理嵌套块                                              │
│  ├─ XClipBlock() (新增)                                             │
│  │   └─ 对块参照施加 XClip 边界                                     │
│  └─ BlockXClipService (新增)                                        │
│       └─ 封装 XClip 创建/修改/删除逻辑                               │
├─────────────────────────────────────────────────────────────────────┤
│                   纯逻辑核心层 (DDNCadAddins.Core)                   │
│  ICropBoundary (不变)                                               │
│  ICropGeometryService (不变)                                        │
│  CropBlockOptions (新增模型)                                        │
│  └─ 裁剪策略枚举 + 嵌套块处理枚举                                   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. 详细设计

### 3.1 核心层 — 新增模型

#### [`CropBlockStrategy`](src/DDNCadAddins.Core/Models/CropBlockOptions.cs:1) 枚举

```csharp
/// <summary>
///     块参照裁剪策略.
/// </summary>
public enum CropBlockStrategy
{
    /// <summary>自动选择：优先 XClip（块定义可写时），否则爆炸.</summary>
    Auto = 0,

    /// <summary>爆炸裁剪：爆炸块参照后裁剪子实体.</summary>
    Explode,

    /// <summary>XClip 裁剪：对块参照施加 XClip 边界，保留块结构.</summary>
    XClip,
}

/// <summary>
///     嵌套块参照处理方式.
/// </summary>
public enum NestedBlockHandling
{
    /// <summary>跳过嵌套块（保留原样）.</summary>
    Skip = 0,

    /// <summary>保留嵌套块原样（不处理）.</summary>
    Keep,

    /// <summary>递归处理嵌套块（应用相同策略）.</summary>
    Recursive,
}
```

#### [`CropBlockOptions`](src/DDNCadAddins.Core/Models/CropBlockOptions.cs:28) 模型

```csharp
/// <summary>
///     块参照裁剪选项.
/// </summary>
public class CropBlockOptions
{
    /// <summary>裁剪策略.</summary>
    public CropBlockStrategy Strategy { get; set; } = CropBlockStrategy.Auto;

    /// <summary>嵌套块处理方式.</summary>
    public NestedBlockHandling NestedHandling { get; set; } = NestedBlockHandling.Skip;

    /// <summary>使用精确几何分类（替代包围盒分类）.</summary>
    public bool UsePreciseClassification { get; set; } = false;
}
```

#### [`CropBlockDetailResult`](src/DDNCadAddins.Core/Models/CropBlockOptions.cs:44) 详细结果

```csharp
/// <summary>
///     单个块参照的裁剪结果详情.
/// </summary>
public class CropBlockDetailResult
{
    public ObjectId BlockRefId { get; set; }
    public string BlockName { get; set; }
    public ContainmentResult Containment { get; set; }
    public string Action { get; set; } // "Kept" / "Deleted" / "Exploded" / "XClipped" / "Skipped"
    public string Message { get; set; }
}
```

---

### 3.2 服务层 — CropBlockService 增强

#### 3.2.1 精确几何分类

新增 [`ClassifyBlockGeometry()`](src/ServiceACAD/CropBlockService.cs:224) 方法：

```
对块参照的实际几何进行精确分类（替代包围盒分类）：
1. 获取块定义中的实体列表（通过 BlockTableRecord）
2. 对每个实体的包围盒进行精确分类
3. 如果所有实体都在边界内 → Inside
4. 如果所有实体都在边界外 → Outside
5. 否则 → Intersects
```

#### 3.2.2 XClip 裁剪

新增 [`XClipBlock()`](src/ServiceACAD/CropBlockService.cs:154) 方法：

```
XClipBlock(blockRef, boundary, ts):
1. 获取 ICropBoundary 的近似多边形顶点
2. 将顶点转换为 WCS 坐标（考虑块参照的变换矩阵）
3. 创建 XClip 边界（使用 BlockXClipService）
4. 如果块参照已有 XClip，合并边界（求交）
```

#### 3.2.3 递归嵌套块处理

增强 [`ExplodeAndCropChildren()`](src/ServiceACAD/CropBlockService.cs:154) 方法：

```
ExplodeAndCropChildren(blockRef, boundary, keepInside, ts, options):
1. 爆炸块参照
2. 对子实体分类：
   a. 非 BlockReference → 调用 ICropService 裁剪
   b. BlockReference → 根据 options.NestedHandling：
      - Skip → 保留原样
      - Keep → 保留原样
      - Recursive → 递归调用 CropBlocks()
```

---

### 3.3 新增 BlockXClipService

```
BlockXClipService (ServiceACAD):
├─ CreateXClip(blockRef, polygonPoints) → OpResult
│   └─ 使用 AutoCAD XClip 命令创建多边形裁剪边界
├─ GetExistingXClip(blockRef) → OpResult<PolygonPoints>
│   └─ 获取块参照已有的 XClip 边界
├─ MergeXClip(blockRef, newBoundary) → OpResult
│   └─ 合并已有 XClip 与新边界（求交）
└─ RemoveXClip(blockRef) → OpResult
    └─ 移除块参照的 XClip 边界
```

---

## 4. 策略选择逻辑

### Auto 策略决策树

```
用户选择 Auto 策略
    ↓
检查块定义是否可写（非外部参照、非匿名块）
    ├─ 是 → 检查块参照是否已有 XClip
    │   ├─ 是 → 合并 XClip 边界
    │   └─ 否 → 创建新 XClip
    └─ 否 → 回退到 Explode 策略
```

### 用户显式选择

| 策略 | 适用场景 | 优点 | 缺点 |
|------|---------|------|------|
| Explode | 需要修改块内容 | 可精确裁剪子实体 | 破坏块结构 |
| XClip | 保留块结构 | 非破坏性，可恢复 | 仅隐藏不修改 |
| Auto | 通用场景 | 自动选择最优方案 | 行为可能不符合预期 |

---

## 5. 接口变更清单

### 新增文件

| 文件 | 层 | 说明 |
|------|-----|------|
| [`CropBlockOptions.cs`](src/DDNCadAddins.Core/Models/CropBlockOptions.cs) | Core | 裁剪选项模型 |
| [`BlockXClipService.cs`](src/ServiceACAD/BlockXClipService.cs) | ServiceACAD | XClip 操作封装 |
| [`CropBlockCommand.cs`](src/AddinsACAD/Commands/CropBlockCommand.cs) | AddinsACAD | 块裁剪命令（可选） |

### 修改文件

| 文件 | 变更说明 |
|------|---------|
| [`CropBlockService.cs`](src/ServiceACAD/CropBlockService.cs) | 新增精确分类、XClip、递归嵌套处理 |
| [`CropBlockResult.cs`](src/ServiceACAD/CropBlockService.cs:14) | 新增 DetailResults 列表 |
| [`CropService.cs`](src/ServiceACAD/CropService.cs) | BlockReference 处理传递 options |
| [`ICropService.cs`](src/ServiceACAD/ICropService.cs) | 可选：新增带 options 的重载 |

---

## 6. 测试计划

### 6.1 纯逻辑测试 (DDNCadAddins.Core.Tests)

| 测试 | 说明 |
|------|------|
| `CropBlockOptionsTests` | 选项默认值、序列化 |
| `CropBlockStrategyTests` | 策略枚举值验证 |

### 6.2 CAD 自动测试 (AUTOCMDTESTS)

| 测试 | 说明 |
|------|------|
| `XClipBlock_SimpleRect` | 矩形边界 XClip |
| `XClipBlock_PolygonBoundary` | 多边形边界 XClip |
| `XClipBlock_WithExistingXClip` | 已有 XClip 时合并 |
| `ExplodeBlock_NestedBlockRecursive` | 递归处理嵌套块 |
| `PreciseClassification_RotatedBlock` | 旋转块的精确分类 |

### 6.3 CAD 手工测试 (MANUALCMDTESTS)

| 测试 | 说明 |
|------|------|
| `CropBlock_AutoStrategy` | Auto 策略在不同场景的行为 |
| `CropBlock_XClipStrategy` | XClip 策略的视觉效果 |
| `CropBlock_ExplodeStrategy` | Explode 策略的视觉效果 |

---

## 7. 实施建议

### Phase 1：精确几何分类 + XClip 基础支持（P0）

1. 新增 `CropBlockOptions` 模型（Core 层）
2. 新增 `BlockXClipService`（ServiceACAD 层）
3. 增强 `CropBlockService.ClassifyBlockBoundingBox()` → 精确几何分类
4. 增强 `CropBlockService.CropBlocks()` → 支持 XClip 策略

### Phase 2：递归嵌套块处理（P1）

1. 增强 `ExplodeAndCropChildren()` → 递归处理嵌套块
2. 增强 `XClipBlock()` → 递归处理嵌套块

### Phase 3：Auto 策略 + 详细结果（P2）

1. 实现 Auto 策略决策树
2. 增强 `CropBlockResult` → 包含 `DetailResults`
3. 新增 `CropBlockCommand`（可选）

---

## 8. 架构合规性检查

### SOLID 合规

| 原则 | 检查 |
|------|------|
| SRP | `BlockXClipService` 只负责 XClip 操作；`CropBlockService` 只负责块裁剪调度 |
| OCP | 新增策略无需修改现有代码，通过 `CropBlockOptions` 扩展 |
| LSP | 所有接口方法返回 `OpResult`/`OpResult<T>`，不抛异常 |
| ISP | 每个接口 ≤ 7 方法 |
| DIP | 高层依赖 `ICropBoundary`/`ICropGeometryService` 抽象 |

### 三层依赖方向

```
AddinsACAD → ServiceACAD → DDNCadAddins.Core
✅ 新增代码符合依赖方向
✅ Core 层无 AutoCAD 依赖
✅ 所有接口方法返回 OpResult/OpResult<T>
```

---

## 9. 待确认问题

1. **XClip 在 AutoCAD 中的实现方式**：是通过 `[CommandMethod("XCLIP")]` 调用 AutoCAD 内置命令，还是直接操作 `XClipInfo` 属性？
2. **多裁剪边界合并**：当块参照已有 XClip 时，新边界是与旧边界求交还是替换？
3. **Auto 策略的默认行为**：是否应该默认使用 Auto 策略？
4. **性能考虑**：精确几何分类需要遍历块定义中的所有实体，对复杂块定义可能有性能影响，是否需要缓存？
