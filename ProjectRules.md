# DDNCadAddins 项目规则索引

> 版本：1.3.0 | 最后更新：2026-06-29

本文档提供了 DDNCadAddins 项目中所有规则文档的索引和简要说明。

## 核心规则文档

| 文档 | 位置 | 说明 |
|------|------|------|
| **AI 项目规则（主）** | [`.cursorrules`](.cursorrules) | AI 开发规则，含 ARCHITECTURE.md 维护要求和圆弧/椭圆精确计算规则 |
| **架构文档** | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | 项目架构设计文档，命令注册表，关键约定 |
| **SOLID 检查说明** | [`SOLIDCheck_Instructions.txt`](SOLIDCheck_Instructions.txt) | SOLID 原则检查说明 |
| **SOLID 应用指南** | [`docs/SOLID_Guidelines.md`](docs/SOLID_Guidelines.md) | SOLID 原则详细应用指南和代码示例 |
| **代码分析配置说明** | [`README-CodeAnalysis.md`](README-CodeAnalysis.md) | 代码分析器配置和 SOLID 检查说明 |
| **NuGet 分析器管理** | [`nuget-analyzers.md`](nuget-analyzers.md) | 分析器清单和清理/恢复指南 |
| **分析器工具说明** | [`analyzer-tools-readme.md`](analyzer-tools-readme.md) | 分析器管理批处理工具使用说明 |

### Cursor 专用规则

| 规则 | 位置 | 说明 |
|------|------|------|
| AutoCAD 资源管理 | [`.cursor/rules/autocad_resource_management.mdc`](.cursor/rules/autocad_resource_management.mdc) | AutoCAD 对象资源释放规范 |
| AutoCAD 测试安全 | [`.cursor/rules/autocad_test_safety.mdc`](.cursor/rules/autocad_test_safety.mdc) | 测试死锁预防和图纸污染防护 |
| 语法错误预防 | [`.cursor/rules/cursor_syntax_check.mdc`](.cursor/rules/cursor_syntax_check.mdc) | Cursor 生成代码的语法错误预防 |
| OpResult/Logger 规范 | [`.cursor/rules/opresult_logger_rules.mdc`](.cursor/rules/opresult_logger_rules.mdc) | OpResult 返回类型和 Logger 使用规范 |
| 未使用代码检测 | [`.cursor/rules/unused_code_detector.mdc`](.cursor/rules/unused_code_detector.mdc) | 检测未使用的代码元素 |

### 其他 IDE 兼容规则

| 规则 | 位置 | 说明 |
|------|------|------|
| Trae 项目规则 | [`.trae/rules/project_rules.md`](.trae/rules/project_rules.md) | Trae IDE 兼容引用（指向 .cursorrules） |

## 已归档/删除的过时文件

| 文件 | 状态 | 替代 |
|------|------|------|
| `.prompt` | ❌ 已删除 | 内容已合并到 `.cursorrules` |

## 如何使用这些规则

1. **新加入项目的开发人员**：
   - 首先阅读 `docs/ARCHITECTURE.md` 了解项目架构
   - 然后阅读 `.cursorrules` 了解编码规范
   - 阅读 `docs/SOLID_Guidelines.md` 熟悉 SOLID 原则的具体应用

2. **日常开发流程**：
   - 修改架构前必须查阅 `docs/ARCHITECTURE.md`
   - 修改架构后必须更新 `docs/ARCHITECTURE.md`
   - 圆弧/椭圆几何计算必须使用参数化公式，禁止分段采样
   - 提交前使用 SOLIDCheck_Instructions.txt 检查代码质量

3. **规则更新流程**：
   - AI 规则修改 `.cursorrules`（唯一权威来源）
   - `.trae/rules/project_rules.md` 不独立维护
   - `.cursor/rules/` 下规则为 Cursor IDE 专用