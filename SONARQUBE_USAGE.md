# SonarQube未使用代码清理指南

本文档介绍如何使用SonarQube在DDNCadAddins项目中检测并清理未使用的类和成员。

## 前提条件

1. 已安装SonarQube服务器（本地或远程），默认地址为 http://localhost:9000
2. 已安装.NET Core SDK
3. 已安装SonarScanner .NET工具 (`dotnet tool install --global dotnet-sonarscanner`)

## 配置步骤

### 1. 设置SonarQube令牌

1. 登录SonarQube服务器
2. 进入"我的账户">"安全"
3. 生成用户令牌并复制
4. 编辑 `run-sonar-analysis.bat` 文件，将令牌填入 `SONAR_LOGIN` 变量

### 2. 运行分析

1. 打开命令提示符（不要使用PowerShell）
2. 进入项目根目录
3. 执行 `run-sonar-analysis.bat`
4. 等待分析完成

### 3. 生成未使用代码报告

1. 确保分析完成并在SonarQube中可见
2. 执行 `generate-unused-code-report.bat`
3. 查看生成的 `unused_code_report.txt` 文件

### 4. 清理未使用的代码

#### 交互式清理（推荐）

1. 打开PowerShell
2. 执行 `.\clean-unused-code.ps1`
3. 对于每个未使用的代码项，确认是否要删除：
   - 输入 `Y` 处理当前项
   - 输入 `N` 跳过当前项
   - 输入 `All` 处理所有剩余项
   - 输入 `Quit` 退出清理过程
4. 默认模式为模拟运行，不会实际修改文件

#### 实际删除未使用代码

执行以下命令实际删除未使用的代码：

```
.\clean-unused-code.ps1 -DryRun:$false
```

#### 自动清理（不推荐）

执行以下命令自动处理所有未使用的代码项：

```
.\clean-unused-code.ps1 -Interactive:$false -DryRun:$false
```

## Cursor集成

在Cursor编辑器中，你可以使用以下方法与SonarQube集成：

1. 使用终端面板运行上述批处理文件
2. 查看生成的报告
3. 在Cursor中打开相应文件，定位到未使用的代码并进行清理

## 注意事项

1. 务必在清理代码前备份项目或使用版本控制
2. 某些看似未使用的代码可能通过反射或动态调用使用，清理前请确认
3. 定期运行SonarQube分析以保持代码库整洁

## 规则说明

本配置主要检测以下未使用的代码：

1. 未使用的私有方法 (S1144)
2. 空方法 (S1186)
3. 未使用的方法参数 (S1172)
4. 未使用的私有字段 (S1068)
5. 未使用的局部变量 (S1481)
6. 未使用的类型参数 (S1185)
7. 未使用的私有类 (S1144) 