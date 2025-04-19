# DDNCadAddins 分析器管理工具

> 版本: 1.0.0  
> 最后更新: 2024-06-24  
> 作者: DDNCadAddins团队

## 工具概述

DDNCadAddins 分析器管理工具集提供了三个批处理脚本，用于管理项目中使用的代码分析器：

1. **cleanup-analyzers.bat** - 清理不需要的临时分析器并创建备份
2. **restore-analyzers.bat** - 从备份中恢复核心分析器
3. **verify-cleanup.bat** - 验证分析器清理效果，检查分析器存在状态

这些工具的设计目的是解决分析器冲突问题，提高编译速度，同时确保关键的代码质量分析保持正常工作。

## 关键概念

- **核心分析器**: 这些是项目开发和代码质量保证所必需的分析器，不应被删除。
- **临时分析器**: 这些是不需要的分析器，可能导致编译错误、警告冲突或编译速度下降。
- **备份目录**: 位于 `%USERPROFILE%\.nuget\packages_backup`，用于存储所有分析器的备份。
- **日志文件**: 每个脚本在执行过程中会生成详细的日志文件，记录所有操作和结果。

## 使用指南

### 1. 清理不需要的分析器

当你遇到分析器冲突导致的编译错误或警告时，可以运行清理脚本：

```
cleanup-analyzers.bat
```

此脚本将：
- 备份所有核心分析器到备份目录
- 备份并删除临时分析器
- 生成详细的操作日志
- 显示清理结果摘要

**注意**: 如果标准删除失败，脚本会尝试使用管理员权限进行安全模式删除。

### 2. 验证清理效果

清理完成后，建议运行验证脚本检查结果：

```
verify-cleanup.bat
```

此脚本将：
- 检查所有核心分析器是否存在
- 检查所有临时分析器是否已清理
- 显示验证结果摘要
- 提供下一步操作建议

### 3. 恢复核心分析器

如果清理过程意外删除了核心分析器，或者您需要重新安装它们，可以运行恢复脚本：

```
restore-analyzers.bat
```

此脚本提供三种恢复模式：
1. 仅恢复缺失的核心分析器（推荐）
2. 恢复所有核心分析器（覆盖现有）
3. 恢复所有备份的分析器（包括临时分析器）

## 核心分析器列表

当前版本的工具管理以下核心分析器：

1. StyleCop.Analyzers (1.2.0-beta.507)
2. SonarAnalyzer.CSharp (9.17.0.82934)
3. CSharpGuidelinesAnalyzer (3.8.5)
4. Blowin.CleanCode (2.7.0)
5. Roslynator.Analyzers (4.13.0)

## 临时分析器列表

以下分析器被识别为不必要的临时分析器，将会被清理：

1. Microsoft.CodeAnalysis.VersionCheckAnalyzer (3.3.2)
2. Microsoft.CodeAnalysis.FxCopAnalyzers (3.3.2)
3. Microsoft.CodeQuality.Analyzers (3.3.2)
4. Microsoft.NetCore.Analyzers (3.3.2)
5. Microsoft.NetFramework.Analyzers (3.3.2)
6. Roslynator.CodeFixes (4.13.0)

## 故障排除

### 清理失败

如果清理过程失败：
1. 尝试以管理员权限重新运行 `cleanup-analyzers.bat`
2. 检查日志文件了解详细错误信息
3. 手动删除临时分析器目录

### 恢复失败

如果恢复过程失败：
1. 确保备份目录存在并包含所需的分析器
2. 检查日志文件了解详细错误信息
3. 如果备份不存在，可能需要通过NuGet重新安装核心分析器

### 项目编译问题

如果清理后项目无法编译：
1. 运行 `verify-cleanup.bat` 检查核心分析器是否存在
2. 如有必要，运行 `restore-analyzers.bat` 恢复核心分析器
3. 查看编译错误信息，确定是否与分析器相关

## 日志文件位置

- 清理日志: `%USERPROFILE%\.nuget\packages\cleanup.log`
- 恢复日志: `%USERPROFILE%\.nuget\packages\restore.log`
- 验证日志: `%USERPROFILE%\.nuget\packages\verify_cleanup.log`

## 自定义配置

如需添加或移除特定分析器，请编辑相应脚本文件中的 `CORE_ANALYZERS` 和 `TEMP_ANALYZERS` 数组。

```batch
:: 定义核心分析器（需要保留的）
set CORE_ANALYZERS=(
    stylecop.analyzers.1.2.0-beta.507
    sonaranalyzer.csharp.9.17.0.82934
    ...
)

:: 定义临时分析器（需要清理的）
set TEMP_ANALYZERS=(
    microsoft.codeanalysis.versioncheckanalyzer.3.3.2
    ...
)
```

## 注意事项

- 在运行清理脚本前，建议关闭所有正在运行的Visual Studio实例
- 建议在运行清理脚本前备份重要项目
- 这些脚本会修改NuGet包缓存目录，可能影响依赖这些分析器的其他项目

## 版本历史

### 1.0.0 (2024-06-24)
- 初始版本
- 包含三个核心脚本：清理、恢复和验证
- 支持核心分析器的自动备份和选择性恢复
- 支持临时分析器的安全删除 