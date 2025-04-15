# DDNCadAddins项目规则索引

本文档提供了DDNCadAddins项目中所有规则文档的索引和简要说明，方便团队成员查找和遵循相关规则。

## 核心规则文档

| 文档 | 位置 | 说明 |
|------|------|------|
| **项目基本规则** | `CursorRules.txt` | 定义基本项目规则、编码标准和工作流程 |
| **SOLID原则规则** | `.cursorrules` | Cursor AI专用的SOLID原则定义，自动指导代码生成和审查 |
| **SOLID应用指南** | `docs/SOLID_Guidelines.md` | SOLID原则在项目中的详细应用指南和代码示例 |
| **SOLID检查工具** | `SOLIDChecker.bat` | 自动检查代码是否遵循SOLID原则并生成报告 |

## 如何使用这些规则

1. **新加入项目的开发人员**：
   - 首先阅读`CursorRules.txt`了解基本规则
   - 然后阅读`docs/SOLID_Guidelines.md`熟悉SOLID原则的具体应用
   - 在本地环境中运行`SOLIDChecker.bat`确保理解如何检查代码质量

2. **日常开发流程**：
   - 编写代码时参考SOLID应用指南
   - 提交前使用SOLIDChecker.bat检查代码质量
   - 代码审查时使用规则文档作为参考标准

3. **使用Cursor AI进行开发**：
   - 确保项目根目录包含`.cursorrules`文件
   - 使用Cursor的AI辅助功能时，AI会自动应用这些规则
   - 利用Ctrl+Shift+L快捷键打开AI面板进行SOLID原则检查

## 规则更新流程

1. 规则变更需经团队讨论并取得共识
2. 更新相应规则文档并更新版本号/日期
3. 通知所有团队成员规则变更
4. 在下一次团队会议上回顾规则变更及其影响

## 规则遵循情况的监控

1. 每次代码审查必须检查SOLID原则遵循情况
2. 每周项目例会回顾SOLID检查报告
3. 定期更新规则以反映项目的发展和团队的反馈 