# DDNCadAddins 分层架构重构计划

## 背景与动机

### 问题
- 所有测试必须在 AutoCAD 进程内运行，容易死锁（卡死）
- 业务逻辑与 CAD API 调用混合，无法独立测试
- 测试速度慢（分钟级），且存在图纸污染风险

### 目标
- 将业务逻辑下沉到不依赖 CAD 的 Core 层
- 99% 的测试脱离 CAD 环境，秒级完成
- CAD 进程内只保留少量集成测试

---

## 目标架构

```
DDNCadAddins/
├── DDNCadAddins.Core/              ← 纯 .NET，零 CAD 依赖
│   ├── Interfaces/                 ← 抽象仓储和服务接口
│   ├── Models/                     ← POCO 领域模型
│   └── Services/                   ← 业务逻辑服务
│
├── DDNCadAddins.Core.Tests/        ← 纯单元测试（手写 Fake 隔离）
│   └── ...                         ← 无需 CAD 环境，秒级完成
│
├── ServiceACAD/                    ← CAD 基础设施层（保持现状）
│   └── Adapters/                   ← 实现 Core 接口的 CAD 适配器（新增）
│
└── AddinsACAD/                     ← CAD 命令层
    └── Commands/                   ← 组合 Core 服务 + CAD 适配器
```

### 依赖方向

```
AddinsACAD → Core ← ServiceACAD/Adapters
               ↑
        Core.Tests (Fake)
```

- `Core` 不依赖任何人
- `ServiceACAD` 实现 `Core` 的接口（依赖 Core）
- `AddinsACAD` 依赖 `Core` 和 `ServiceACAD`
- `Core.Tests` 只依赖 `Core`，用手写 Fake 替代 CAD 实现

---

## 渐进式迁移路线图

### ✅ 阶段1：HelloWorld PoC（已完成）

**目标**：验证分层架构可行性，搭建基础框架。

**完成内容**：
- 创建 `DDNCadAddins.Core` 项目（无 CAD 引用）
- 创建 `DDNCadAddins.Core.Tests` 项目（NUnit，无 CAD）
- 实现 `ICalculatorService` / `CalculatorService` HelloWorld 示例
- 编写 12 个纯单元测试，全部通过
- 创建 `HelloWorldCommand`（CAD 命令 `HELLO` 调用 Core 层）
- 更新 `.sln` 文件

**验证结果**：
- ✅ Core 项目编译成功（0 AutoCAD 引用）
- ✅ 12 个测试全部通过（秒级，无需 CAD）
- ✅ HELLO 命令在 CAD 中调用成功

---

### ✅ 阶段2：迁移 OpResult 到 Core（已完成）

**目标**：将共享基础类型移入 Core，使所有层统一使用。

**完成内容**：
1. 确认 `Core/Models/OpResult.cs` 的 API 完全兼容 `ServiceACAD/OpResult.cs`
2. 在 `ServiceACAD.csproj` 中添加对 `DDNCadAddins.Core` 的项目引用
3. 将 `ServiceACAD/OpResult.cs` 改为继承 Core 版本的桥接类，保持向后兼容
4. 全解决方案重新编译成功（5 个项目，0 错误）

**验证结果**：
- ✅ ServiceACAD 正确引用 DDNCadAddins.Core
- ✅ OpResult 桥接类保持向后兼容，现有代码无需修改
- ✅ 所有 5 个项目编译成功（0 错误）

---

### ✅ 阶段3：迁移图层服务（第一个真实业务模块）（已完成）

**目标**：将图层管理业务逻辑从 CAD 进程解放出来。

**完成内容**：
1. Core 层新增 `LayerInfo`、`LayerStateSnapshot` POCO 模型
2. 新增 `ILayerRepository` 仓储接口和 `LayerManagementService` 业务服务
3. ServiceACAD 新增 `AutoCadLayerRepository` 适配器和 `LayerStateSnapshotConverter`
4. `TransactionServiceForStyle` 图层状态方法委托至 Core 层（保持向后兼容）
5. `BlockCleanupCommand` 改为直接使用 Core 层服务
6. Core.Tests 新增 `FakeLayerRepository` + 12 个 `LayerManagementServiceTests`

**验证结果**：
- ✅ Core 项目编译成功，无 AutoCAD 引用
- ✅ 全解决方案 5 个项目编译成功（0 错误）
- ✅ 24 个 Core.Tests 用例（Calculator 12 + LayerManagement 12），可在 VS Test Explorer 秒级运行
- ⏳ BlockCleanup 命令需在 CAD 中手动验证

---

### 阶段4：迁移块清理服务

**目标**：将 `BlockCleanupService` 的爆炸和删除逻辑移入 Core。

**新增接口** `DDNCadAddins.Core/Interfaces/IBlockRepository.cs`：

```csharp
public interface IBlockRepository
{
    OpResult<IReadOnlyList<BlockInfo>> GetAllBlocksInCurrentSpace();
    OpResult<bool> ExplodeBlock(string blockId);
    OpResult<bool> EraseEmptyBlock(string blockId);
}
```

**新增模型**：`BlockInfo`（POCO，无 CAD 类型）

**业务服务**：`BlockCleanupService`
- `CleanupEmptyBlocks()` → 删除空定义图块
- `ExplodeAllXClippedBlocks()` → 爆炸被裁剪的图块
- 多轮迭代逻辑（纯逻辑，不涉及 CAD API）

---

### 阶段5：迁移其他模块（按需）

候选模块（按复杂度排序）：
1. **颜色/线型验证服务** — 纯逻辑，迁移简单
2. **属性处理服务** — `PropertyUtils` 部分逻辑
3. **XClip 边界生成服务** — 几何计算部分可独立

---

## 技术规范

### Core 层约束（强制）

- 禁止引用任何 `Autodesk.*` 命名空间
- 禁止引用 `ServiceACAD`
- 只允许引用 `System.*` 基础库
- 所有公共方法返回 `DDNCadAddins.Core.Models.OpResult` 或 `OpResult<T>`
- 接口方法必须有 XML 文档注释

### 适配器层约束（ServiceACAD/Adapters）

- 仅负责 CAD 类型 ↔ Core POCO 的转换
- 仅调用 `TransactionService` 系列方法，不直接访问 AutoCAD API
- 禁止包含业务逻辑
- 每个方法必须有完整 try-catch，返回 `OpResult`

### Core.Tests 约束

- 不得引用任何 `Autodesk.*`
- 使用手写 Fake 对象隔离依赖（零额外依赖，.NET Framework 4.7 原生兼容）
- 遵循 Arrange / Act / Assert 模式
- 覆盖：正常路径、边界条件（null/空）、失败路径

---

## 手写 Fake 模式

不引入额外 Mock 框架，所有测试隔离通过手写 Fake 实现：

```csharp
public class FakeLayerRepository : ILayerRepository
{
    public List<LayerInfo> Layers { get; set; } = new List<LayerInfo>();
    public List<LayerInfo> UpdatedLayers { get; } = new List<LayerInfo>();
    public bool ShouldFailGetAll { get; set; }

    public OpResult<IReadOnlyList<LayerInfo>> GetAllLayers()
    {
        if (ShouldFailGetAll)
            return OpResult<IReadOnlyList<LayerInfo>>.Fail("模拟获取失败");
        return OpResult<IReadOnlyList<LayerInfo>>.Success(Layers.AsReadOnly());
    }

    public OpResult UpdateLayer(LayerInfo layer)
    {
        UpdatedLayers.Add(layer);
        return OpResult.Success();
    }
    // ...
}
```

---

## 迁移验证检查清单

每个阶段完成后必须验证：

- [ ] Core 项目编译成功，无 AutoCAD 引用
- [ ] Core.Tests 全部通过，无需 CAD 环境
- [ ] 现有 CAD 内 RunTests 测试全部仍通过（无回归）
- [ ] 命令在 AutoCAD 中手动验证功能正确
- [ ] git commit 说明中注明"已通过 [命令名] 手动验证"

---

## 文件变更记录

| 提交 | 内容 |
|------|------|
| `326ccff` | 创建 Core/Core.Tests 项目，HelloWorld PoC |
| `634e330` | 对齐 Directory.props 统一配置 |
| `b97cd13` | 修复 CalculationResult.cs 编译问题 |
| `38dcc77` | 添加架构计划文档 |
| 阶段2 | OpResult 迁移到 Core，ServiceACAD 桥接，5个项目编译成功 |
| 阶段3 | 图层服务迁移到 Core，AutoCadLayerRepository 适配器，BlockCleanup 集成 |

---

*最后更新：2026-06-13*
