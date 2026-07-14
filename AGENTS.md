# DDNCadAddins 项目规则

## 项目基础信息
- 基于.NET Framework 4.7和AutoCAD API 2019开发
- 使用NUnit、NUnitLite和ExtentReports作为测试框架
- 使用事务处理确保AutoCAD操作的安全性

## ★★★ 最高原则：AutoCAD 异常安全（不可违反）★★★
- AutoCAD 进程中任何未捕获的异常都会导致致命错误（Crash），必须在所有代码层级严格防范
- 所有方法（包括服务层、命令层、工具类）的返回值必须使用 OpResult 或 OpResult\<T\>，禁止直接抛出异常
- 每个方法体必须用 try-catch(Exception) 包裹全部逻辑，catch 块中必须调用 Logger._.Error 记录异常，然后返回 OpResult.Fail(message)
- 严禁使用 throw、throw ex 将异常向上传播，包括在 catch 块中重新抛出
- void 方法需改为 OpResult 返回类型；确实无法修改签名的事件处理器，内部必须完整 try-catch
- 此原则优先级高于所有其他规则，包括 SOLID 原则和代码简洁性要求

## ★ csproj 同步规则（不可违反）★★★
- **新增/删除/重命名 .cs 文件后，必须同步更新对应项目的 .csproj 文件**
- 未在 .csproj 中注册的 .cs 文件不会被 MSBuild 编译，导致运行时找不到命令/类型
- 修改步骤：
  1. 新增 .cs 文件 → 在对应 .csproj 的 `<ItemGroup><Compile Include="..."/></ItemGroup>` 中添加条目
  2. 删除 .cs 文件 → 从 .csproj 中移除对应 `<Compile Include="..."/>` 条目
  3. 重命名 .cs 文件 → 更新 .csproj 中对应 `<Compile Include="..."/>` 的路径
- 此规则优先级高于 SOLID 原则和代码简洁性要求

## ★ AutoCAD 资源管理规则 ★★★
### 必须显式释放的对象
- **Transaction**: 使用 using 语句包裹，必须 Commit() 后自动 Dispose()
- **OpenCloseTransaction / SubTransaction**: 必须提交或回滚并释放
- **手动打开的 DBObject / BlockTableRecord / DBDictionary**: 需要 Close()
- **SelectionSet**: 使用完后需 Dispose()
- **Document.LockDocument()**: 必须配对调用 UnlockDocument()（try-finally 确保）
- **自定义创建的 Database**: 不再使用时 Dispose()

### 不应手动释放的对象
- **Document / Application / DocumentCollection**: 由 AutoCAD 管理生命周期
- **Database（绑定到 Document 的） / Editor**: 由 AutoCAD 管理
- **通过事务获取的 Entity/DBObject**: 事务负责管理，不要手动 Close
- **TransactionManager / LayerManager / BlockManager**: 由 AutoCAD 管理

### 事务使用规范
- 事务应尽可能短小，避免在事务中执行耗时操作
- 事务结束前必须调用 Commit 或 Abort
- 确保事务在所有情况下都能正确结束（try-finally）
- Document 锁定时间应尽可能短，避免在锁定区域执行 UI 操作或等待用户输入

## ★ AutoCAD 测试安全规则（血泪教训，不可违反）★★★
- **禁止在测试中操作全局图层状态**（UnlockAndThawAllLayers、RestoreLayerStates）- 与 NUnit 并发运行的其他测试产生写锁竞争，必然死锁
- **禁止在同一事务中混用创建和遍历写操作** - 用多个 Action 分离到不同事务
- **禁止在测试中调用 UpgradeOpen()** - 服务方法已自行管理打开模式
- **禁止在测试中修改 db.Clayer（当前图层）** - 会导致 AutoCAD 挂起
- **禁止在测试中使用 Assert.DoesNotThrow 包裹 AutoCAD 操作** - 直接断言 IsSuccess
- **全局状态类功能**（图层、线型、块表等）应通过手动集成测试验证，不在自动化测试套件中测试
- **图纸污染防护**：会永久修改图纸的测试不应加入自动化测试套件；如需自动化测试修改操作，必须在测试内部创建和清理专用数据

## ★ 代码审查检查点 ★★★
### 方法级别检查
- [ ] 方法是否被 try-catch(Exception) 完全包裹？
- [ ] catch 块中是否调用了 Logger._.Error？
- [ ] 方法是否返回 OpResult/OpResult\<T\> 而非 void？
- [ ] catch 块中是否没有 throw/throw ex？

### 日志检查
- [ ] 每个 catch 块都有 Logger._.Error 调用？
- [ ] 关键操作入口/出口都有 Logger._.Info 记录？
- [ ] 参数验证失败时是否使用了 Logger._.Warn？

### 调用链检查
- [ ] 调用 OpResult 方法后是否检查了 IsSuccess？
- [ ] 失败时是否返回了有意义的错误消息？

## ★ 未使用代码检测规则 ★★★
- 发现未使用的 using 指令应立即移除
- 未使用的私有字段应在确认无反射引用后删除
- 临时注释掉的代码块应在提交前清理
- 由反射调用的私有方法（如 AutoCAD CommandMethod 标记的方法）不在检测范围内
- 事件处理器的 `sender` 参数即使未在方法体中引用也应保留

## ★ 修改代码后验证流程（必须执行）★★★
- **每次修改代码后**，必须依次执行以下两步验证，**全部通过**后才能提交：

### 步骤 1：MSBuild 编译
- MSBuild 路径：`C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\amd64\MSBuild.exe`
- 解决方案：`d:\leaveblackgithub\DDNCadAddins\src\DDNCadAddins.sln`
- 运行命令：
  ```
  cmd /c ""C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\amd64\MSBuild.exe" "d:\leaveblackgithub\DDNCadAddins\src\DDNCadAddins.sln" /p:Configuration=Debug /t:Rebuild"
  ```
- 编译错误必须全部修复后才能继续

### 步骤 2：NUnit 单元测试验证
- Console Runner 路径：`C:\Users\CFDDN\.nuget\packages\nunit.consolerunner\3.16.3\tools\nunit3-console.exe`
- 测试 DLL：`d:\leaveblackgithub\DDNCadAddins\src\bin\Debug\DDNCadAddins.Core.Tests.dll`
- 运行全部测试：
  ```
  cmd /c ""C:\Users\CFDDN\.nuget\packages\nunit.consolerunner\3.16.3\tools\nunit3-console.exe" "d:\leaveblackgithub\DDNCadAddins\src\bin\Debug\DDNCadAddins.Core.Tests.dll" --noheader"
  ```
- **所有测试必须全部通过**，存在失败测试时不能提交

## ★ 测试驱动开发流程（TDD）★★★
- 开发流程必须以测试驱动，按以下优先级排列：
  1. **第一优先级：纯逻辑 NUnit 测试**（`DDNCadAddins.Core.Tests`）- 不依赖 AutoCAD API
  2. **第二优先级：CAD 环境自动测试**（`AddinsACAD.ServiceTests`）- 通过 `AUTOCMDTESTS` 命令在 AutoCAD 内运行，仅使用内存侧数据库
  3. **第三优先级（最后考虑）：CAD 环境手工测试**（`MANUALCMDTESTS` 命令）- 每个子命令必须返回 TestRecords

## 开发工作流程
- 命令行必须严格采用Windows CMD语法，不允许使用PowerShell特有语法
- 命令行中多条命令必须用;分割，严禁使用&&连接符
- 使用cmd /c执行批处理命令
- 编译代码使用Visual Studio
- 提交代码前必须参考SOLIDCheck_Instructions.txt检查SOLID原则遵循情况

## SOLID 原则
- **单一职责原则 (SRP)**：每个类应该只有一个职责，每个方法应该只做一件事
- **开闭原则 (OCP)**：代码应该对扩展开放，对修改关闭
- **里氏替换原则 (LSP)**：子类必须能够替换其基类
- **接口隔离原则 (ISP)**：接口应该小而精炼，方法数量控制在7个以内
- **依赖倒置原则 (DIP)**：高层模块不应依赖低层模块，两者都应依赖抽象

## AutoCAD 访问规则
- 对CAD的访问必须通过唯一的封装访问点（如AcadService）
- 避免在多处直接访问Application.DocumentManager等AutoCAD对象
- 所有AutoCAD API调用应集中在服务层
- CAD访问服务不应包含复杂业务逻辑，应仅作为AutoCAD API的简单封装
- 复杂业务逻辑应放在专门的业务服务类中，由业务服务调用CAD访问服务
- ★ 数据库对象必须在 Transaction 内访问：通过 TransactionService.ExecuteInTransaction 或服务层 GetObject\<T\> 打开，禁止在事务外读写 DBObject
- ★ 禁止使用 dynamic 类型访问 AutoCAD 对象；必须使用强类型（Entity、BlockReference、LayerTableRecord 等），通过 Transaction.GetObject(id, OpenMode) 获取

## 异常处理规则
- 捕获所有可能的异常，不允许任何异常传播到调用者
- 使用 OperationResult\<T\> 或 OpResult 作为返回值类型，包含执行状态和结果
- 在日志中详细记录异常信息，但在用户界面中只显示简洁的错误消息

## OpResult 标准方法模板
```csharp
public OpResult DoSomething(string input)
{
    try
    {
        if (string.IsNullOrEmpty(input))
        {
            Logger._.Warn("DoSomething: input 为空");
            return OpResult.Fail("输入参数不能为空");
        }
        return OpResult.Ok("操作成功");
    }
    catch (Exception ex)
    {
        Logger._.Error(ex, "DoSomething 执行失败, input={Input}", input);
        return OpResult.Fail($"操作失败: {ex.Message}");
    }
}
```

## Logger 使用规范
| 级别 | 使用场景 |
|------|----------|
| `Logger._.Error()` | 异常捕获、操作失败（所有 catch 块中必须调用） |
| `Logger._.Warn()` | 参数无效、边界情况 |
| `Logger._.Info()` | 操作开始/结束、关键步骤 |
| `Logger._.Debug()` | 详细调试信息 |
- 禁止在日志中记录密码、Token 等敏感信息
- 禁止在 Info 级别记录循环内的每步细节（应使用 Debug 级别）
- 禁止在用户界面中显示完整的异常堆栈（应统一使用简洁错误消息）

## 命令结构规则
- 所有命令都必须支持 CANCEL 操作取消
- 命令必须严格分为三个部分：输入获取、主体逻辑、输出显示
- 非命令和非输入输出模块不应直接进行输入输出操作

## 文档维护规则
- `docs/ARCHITECTURE.md` 是项目的核心架构文档，修改架构后必须更新
- 圆弧/椭圆几何计算禁止分段采样，必须使用参数化公式

## 架构参考（详细内容见 docs/ARCHITECTURE.md）
- **三层架构**：`DDNCadAddins.Core`（纯逻辑，无 AutoCAD 依赖）→ `ServiceACAD`（AutoCAD 服务层）→ `AddinsACAD`（命令层）
- **命令注册**：通过 `[CommandMethod("命令名")]` 注册，大小写不敏感。完整命令表见 `docs/ARCHITECTURE.md#三命令注册体系`
- **裁剪服务**：`ICropService` 接口下有 20+ 实现类（CropArcService、CropLineService 等），`CropService` 为主调度器
- **测试体系**：`DDNCadAddins.Core.Tests`（纯逻辑 NUnit）+ `AddinsACAD/ServiceTests`（CAD 环境自动测试）+ `MANUALCMDTESTS`（手工测试）
- **关键接口**：`ITransactionService` / `ICropService` / `IBlockService` / `IBlockRepository` / `ILayerRepository`
- **MANUALCMDTESTS 维护规则**：快捷键不可复用，删除/新增子命令时同步清理 `AskSubCommand()` 和 `MapToCommand()`

## 代码注释规则
- 为每个函数添加标准XML文档注释，包含 summary、param、returns、exception、remarks
