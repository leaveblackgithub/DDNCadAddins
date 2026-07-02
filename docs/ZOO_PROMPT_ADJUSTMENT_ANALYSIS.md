# ZOO 各角色系统提示词调整分析

> 版本：1.0.0 | 最后更新：2026-07-02

## 一、背景

本项目（DDNCadAddins）是一个基于 .NET Framework 4.7 和 AutoCAD API 2019 的插件开发项目，拥有一套极其严格的项目规则体系（见 [`.cursorrules`](.cursorrules)）。ZOO 的默认系统提示词是通用型的，未包含本项目特有的关键约束，直接使用会导致 AI 生成不符合项目规范的代码。

本文档分析了 ZOO 五个内置角色（Architect / Code / Ask / Debug / Orchestrator）的默认提示词与项目规则之间的差距，并给出调整建议。对应的配置文件为 [`.roomodes`](.roomodes)。

---

## 二、核心冲突点汇总

以下是 ZOO 默认提示词与项目规则之间的**关键冲突**，按严重程度排序：

| # | 冲突点 | ZOO 默认行为 | 项目要求 | 影响角色 |
|---|--------|-------------|---------|---------|
| 1 | **异常处理** | 鼓励标准 try-catch-throw 模式 | ★★★ 禁止 throw，必须返回 OpResult/OpResult\<T\> | Code, Debug, Architect |
| 2 | **方法返回类型** | 允许 void 返回 | 禁止 void，必须返回 OpResult | Code |
| 3 | **命令行语法** | 使用 Unix/PowerShell 语法 | 必须用 Windows CMD 语法，`;` 分割非 `&&` | 所有角色 |
| 4 | **AutoCAD 事务** | 无事务意识 | 所有 DBObject 必须在 Transaction 内访问 | Code, Debug |
| 5 | **AutoCAD 资源** | 无资源管理意识 | 必须区分释放/不释放对象，using 语句 | Code |
| 6 | **分层架构** | 无架构感知 | 3 层（Core/ServiceACAD/AddinsACAD），依赖方向不可逆 | Architect, Code, Orchestrator |
| 7 | **TDD 流程** | 无测试优先意识 | 3 级测试优先级，修改后必须验证 | Code, Orchestrator |
| 8 | **SOLID 度量** | 通用 SOLID 描述 | 具体度量：类 < 200 行、方法 < 20 行、接口 < 7 方法 | Architect, Code |
| 9 | **dynamic 禁令** | 无限制 | 禁止 dynamic 访问 AutoCAD 对象，必须强类型 | Code |
| 10 | **架构文档维护** | 无文档维护意识 | 修改架构后必须更新 ARCHITECTURE.md | Architect, Code |
| 11 | **圆弧几何计算** | 无特殊要求 | 禁止分段采样，必须参数化公式 | Code, Architect |
| 12 | **测试安全** | 无测试安全意识 | 禁止死锁操作、图纸污染 | Code, Debug |
| 13 | **语言** | 默认英文 | 必须中文回复 | 所有角色 |

---

## 三、各角色调整详情

### 3.1 💻 Code 模式（影响最大）

**默认问题：** ZOO 的 Code 模式会生成标准的 C# 代码（throw 异常、void 方法、直接访问对象），完全不符合本项目的 OpResult 模式和异常安全要求。

**调整内容：**
- 注入 AutoCAD 异常安全最高原则（禁止 throw、必须 OpResult、必须 try-catch）
- 注入 OpResult/OpResult\<T\> 使用模板
- 注入 Logger._.Error 使用规范
- 注入 AutoCAD 事务和强类型访问规则
- 注入 3 层架构依赖方向
- 注入 SOLID 具体度量标准
- 注入 TDD 验证流程（NUnit Console Runner 命令）
- 注入 Windows CMD 语法要求
- 注入圆弧/椭圆参数化计算规则
- 注入资源管理规则（using 语句、事务内访问）

### 3.2 🏗️ Architect 模式

**默认问题：** Architect 模式设计架构时不了解本项目的 3 层分层约束和 SOLID 具体度量。

**调整内容：**
- 注入 3 层架构（Core → ServiceACAD → AddinsACAD）依赖方向
- 注入 ARCHITECTURE.md 维护规则（修改前查阅、修改后更新）
- 注入 SOLID 具体度量（类 < 200 行、方法 < 20 行、接口 < 7 方法、禁止 partial 规避 SRP）
- 注入 TDD 3 级测试优先级体系
- 注入 OpResult 模式对架构设计的影响
- 注入 AutoCAD 异常安全对架构的约束

### 3.3 🪲 Debug 模式

**默认问题：** Debug 模式诊断问题时不知道 AutoCAD 进程崩溃的根本原因（未捕获异常），也不了解 OpResult 失败链路。

**调整内容：**
- 注入 AutoCAD 崩溃根因分析（未捕获异常 = 致命 Crash）
- 注入 OpResult 失败链路追踪方法（IsSuccess 检查、Message 传递）
- 注入 Logger._.Error 日志定位方法
- 注入测试安全规则（死锁排查：全局图层状态、UpgradeOpen、db.Clayer）
- 注入事务外访问 DBObject 的诊断方法
- 注入 Windows CMD 语法（调试命令也必须用 CMD）

### 3.4 ❓ Ask 模式

**默认问题：** Ask 模式回答问题时不知道项目专有术语和架构约定。

**调整内容：**
- 注入项目架构概览（3 层、OpResult、事务封装）
- 注入规则文件索引（.cursorrules、.cursor/rules/、ARCHITECTURE.md）
- 注入命令注册体系概览
- 注入测试体系概览

### 3.5 🪃 Orchestrator 模式

**默认问题：** Orchestrator 分解任务时不考虑 3 层架构的依赖方向和 TDD 验证要求。

**调整内容：**
- 注入 3 层架构协调原则（Core 先行 → ServiceACAD 桥接 → AddinsACAD 入口）
- 注入 TDD 合规要求（每个子任务必须包含测试验证步骤）
- 注入 OpResult 异常安全合规检查点
- 注入 ARCHITECTURE.md 同步更新要求
- 注入 Windows CMD 语法要求

---

## 四、配置文件说明

调整后的配置写入 [`.roomodes`](.roomodes) 文件，ZOO 会自动读取项目根目录下的此文件并覆盖默认角色提示词。

**配置策略：**
- 使用 `customInstructions` 字段注入项目特定规则，不替换 `roleDefinition`（保留 ZOO 基础能力描述）
- 规则精简引用，指向 [`.cursorrules`](.cursorrules) 和 [`.cursor/rules/`](.cursor/rules/) 避免重复维护
- 每个角色只注入与其职责相关的规则子集，避免信息过载

---

## 五、维护说明

1. `.roomodes` 中的项目规则应与 `.cursorrules` 保持一致
2. 当 `.cursorrules` 更新核心规则时，同步检查 `.roomodes` 是否需要更新
3. `.roomodes` 版本号跟随 `.cursorrules` 版本号
4. 新增 ZOO 自定义角色时，在 `customModes` 数组中追加配置
