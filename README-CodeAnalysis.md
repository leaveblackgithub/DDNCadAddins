# 代码分析和SOLID原则检查配置

本项目使用了多种代码分析器来确保代码质量和遵循SOLID原则。所有配置通过以下文件统一管理：

1. `.editorconfig` - 定义代码样式和分析规则
2. `stylecop.json` - StyleCop特定配置
3. `Directory.Build.props` - 全局项目属性和分析器包引用

## 启用的分析器

项目引用了以下分析器：

1. **Roslynator** - 提供全面的C#代码分析和重构功能
2. **StyleCop** - 强制执行代码样式和一致性规则
3. **SonarAnalyzer** - 专注于安全性和性能问题检测
4. **CSharpGuidelinesAnalyzer** - 实施C#编码指南，包括SOLID原则检查
5. **BlowinCleanCode** - 提供清洁代码分析，特别关注单一职责原则

## SOLID原则检查

这些分析器共同强制执行SOLID原则：

### 1. 单一职责原则 (SRP)
- BCC1002：避免方法包含"And"的名称
- BCC1004：避免过长方法
- BCC1005：避免方法认知复杂度过高
- BCC1008：避免类过大
- SA1402：一个文件只能有一个类
- AV1500：方法不应超过7个语句

### 2. 开闭原则 (OCP)
- CA1051：不要声明可见的实例字段
- CA1724：类型名称不应与命名空间冲突
- RCS1018：添加默认访问修饰符

### 3. 里氏替换原则 (LSP)
- CA2012：正确使用ValueTask
- CA1063：正确实现IDisposable
- AV1130：返回最具体的类型

### 4. 接口隔离原则 (ISP)
- CA1040：避免空接口
- CA1062：验证公共方法的参数
- AV1135：声明参数为最抽象的类型

### 5. 依赖倒置原则 (DIP)
- CA2000：在适当的范围内释放对象
- BCC1044：避免公共静态字段

## 使用方法

1. **构建项目**：构建项目时会自动运行分析器并报告违规
   ```
   dotnet build
   ```

2. **修复自动可修复的问题**：部分问题可使用Roslynator自动修复
   ```
   dotnet roslynator fix <项目或解决方案文件> --verbosity d
   ```

3. **查看所有分析器规则**：
   ```
   dotnet roslynator list-analyzers
   ```

4. **禁用特定规则**：如需临时禁用代码中的规则，可使用以下注释：
   ```csharp
   #pragma warning disable SA1402 // 禁用特定规则
   // 受影响的代码
   #pragma warning restore SA1402 // 重新启用规则
   ```

## 自定义配置

如需调整规则严重级别，可修改以下文件：

- `.editorconfig`：调整各规则的严重级别（none, suggestion, warning, error）
- `Directory.Build.props`：修改分析器包引用或全局编译属性
- `stylecop.json`：自定义StyleCop特定行为

## 常见问题

1. **分析器报告太多警告**：初次引入分析器时，可能会看到大量警告。建议逐步解决，先专注高优先级规则。

2. **配置冲突**：当不同分析器规则产生冲突时，可以在`.editorconfig`中将冲突规则的一个设置为`none`。

3. **性能问题**：如果分析器导致IDE响应缓慢，可以考虑仅在构建时启用某些规则。

4. **规则太严格**：如果某些规则对项目不适用，可以在`.editorconfig`中调整其严重级别或完全禁用。 