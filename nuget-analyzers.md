# DDNCadAddins项目分析器管理

## 核心分析器（需要保留）

这些分析器在项目的`Directory.Build.props`中配置，是项目质量控制的核心组件：

1. **StyleCop.Analyzers (1.2.0-beta.507)**
   - 代码风格检查和标准化
   - 路径: `packages\StyleCop.Analyzers.1.2.0-beta.507`

2. **SonarAnalyzer.CSharp (9.17.0.82934)**
   - 综合代码质量和安全性分析
   - 路径: `packages\SonarAnalyzer.CSharp.9.17.0.82934`

3. **CSharpGuidelinesAnalyzer (3.8.5)**
   - C#编码指南和最佳实践检查
   - 路径: `packages\CSharpGuidelinesAnalyzer.3.8.5`

4. **Blowin.CleanCode (2.7.0)**
   - 清洁代码原则检查，关注单一职责原则
   - 路径: `packages\Blowin.CleanCode.2.6.0` (注意版本不匹配)

5. **Roslynator.Analyzers (4.13.0)**
   - 增强的C#分析器和重构工具
   - 路径: `packages\Roslynator.Analyzers.4.13.0`

## 临时分析器（可以移除）

这些分析器是为了自动修复而临时安装的，或者是其他分析器的依赖，可以安全移除：

1. **Roslynator.CodeFixes (4.13.0)**
   - 仅用于自动修复，主要功能由Roslynator.Analyzers提供
   - 已移至: `packages_backup\Roslynator.CodeFixes.4.13.0`

2. **Microsoft.CodeAnalysis.FxCopAnalyzers (3.3.2)**
   - 旧版FxCop分析器，功能与SonarAnalyzer重叠
   - 已移至: `packages_backup\Microsoft.CodeAnalysis.FxCopAnalyzers.3.3.2`

3. **Microsoft.CodeAnalysis.VersionCheckAnalyzer (3.3.2)**
   - FxCop的依赖项
   - 路径: `packages\Microsoft.CodeAnalysis.VersionCheckAnalyzer.3.3.2`

4. **Microsoft.CodeQuality.Analyzers (3.3.2)**
   - FxCop的依赖项
   - 路径: `packages\Microsoft.CodeQuality.Analyzers.3.3.2`

5. **Microsoft.NetCore.Analyzers (3.3.2)**
   - FxCop的依赖项
   - 路径: `packages\Microsoft.NetCore.Analyzers.3.3.2`

6. **Microsoft.NetFramework.Analyzers (3.3.2)**
   - FxCop的依赖项
   - 路径: `packages\Microsoft.NetFramework.Analyzers.3.3.2`

7. **StyleCop.Analyzers.Unstable (1.2.0.507)**
   - StyleCop.Analyzers的预览版组件
   - 路径: `packages\StyleCop.Analyzers.Unstable.1.2.0.507`

8. **SonarAnalyzer.CSharp (10.8.0.113526)**
   - 重复安装的更新版本
   - 路径: `packages\SonarAnalyzer.CSharp.10.8.0.113526`

## 注意事项

1. 由于访问权限问题，某些分析器无法移动到备份目录。建议在项目稳定后，使用管理员权限手动删除这些临时分析器。

2. 移除分析器前，确保项目能够正常构建。对于每个被移除的分析器，应该进行测试构建，确认不会影响项目功能。

3. Blowin.CleanCode包版本不匹配（Directory.Build.props中为2.7.0，实际安装为2.6.0），建议统一版本以避免潜在问题。 