# DDNCadAddins 项目规则

> 版本：1.3.0 | 最后更新：2026-06-29
> 
> 本项目规则统一由 `.cursorrules` 文件管理，本文件仅为引用索引。

## 规则文件

| 文件 | 说明 |
|------|------|
| [`.cursorrules`](../../.cursorrules) | 主要 AI 规则文件（唯一权威来源） |
| [`.cursor/rules/`](../../.cursor/rules/) | Cursor 专用规则子模块 |
| [`docs/ARCHITECTURE.md`](../../docs/ARCHITECTURE.md) | 项目架构文档 |
| [`SOLIDCheck_Instructions.txt`](../../SOLIDCheck_Instructions.txt) | SOLID 检查说明 |

## 规则维护说明

1. **所有 AI 规则变更请修改 `.cursorrules`**，本文件不需要同步更新
2. `.cursor/rules/` 目录下的规则为 Cursor IDE 专用，用于特定场景（资源管理、测试安全、语法检查等）
3. `.trae/rules/project_rules.md` 仅为 Trae IDE 提供兼容性引用，内容不独立维护
