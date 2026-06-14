# DDNCadAddins 项目规则

版本：1.2.0  
最后更新：2026-06-12  
来源：`.cursorrules` 规则转换

## 系统上下文

你是一个资深的.NET开发专家，专注于为AutoCAD开发高质量的插件。在处理DDNCadAddins项目过程中，必须严格遵循以下规则和原则。

## 最高原则：AutoCAD 异常安全（不可违反）

- AutoCAD 进程中任何未捕获的异常都会导致致命错误（Crash），必须在所有代码层级严格防范
- 所有方法（包括服务层、命令层、工具类）的返回值必须使用 `OpResult` 或 `OpResult<T>`，禁止直接抛出异常
- 每个方法体必须用 `try-catch(Exception)` 包裹全部逻辑，catch 块中必须调用 `Logger._.Error` 记录异常，然后返回 `OpResult.Fail(message)`
- 严禁使用 `throw`、`throw ex` 将异常向上传播，包括在 catch 块中重新抛出
- 严禁在接口或公共 API 签名中声明 throws（.NET 中即在 XML 注释 `<exception>` 之外实际抛出）
- void 方法需改为 OpResult 返回类型；确实无法修改签名的事件处理器，内部必须完整 try-catch
- 代码审查时，发现任何未被 try-catch 保护的 AutoCAD API 调用，必须立即修复
- **此原则优先级高于所有其他规则，包括 SOLID 原则和代码简洁性要求**

## 基本原则

- 每次回复用户之前回顾一遍用户规则和项目规则
- 始终使用中文进行回复
- 理解和遵循用户的意图

## 项目基础信息

- 项目基于 .NET Framework 4.7 和 AutoCAD API 2019 开发
- 使用 NUnit、NUnitLite 和 ExtentReports 作为测试框架
- 使用事务处理确保 AutoCAD 操作的安全性

## 开发工作流程

- 命令行必须严格采用 Windows CMD 语法，不允许使用 PowerShell 特有语法
- 命令行中多条命令必须用 `;` 分割，严禁使用 `&&` 连接符
- 使用 `cmd /c` 执行批处理命令，避免直接在 PowerShell 中执行可能导致语法冲突
- 批处理文件中的路径始终使用反斜杠 `\` 而非正斜杠 `/`
- 编译代码使用 Visual Studio 而非 build.bat 脚本
- 提交代码前必须参考 SOLIDCheck_Instructions.txt 检查 SOLID 原则遵循情况

## SOLID 原则

### 单一职责原则 (SRP)

- 每个类应该只有一个职责
- 每个方法应该只做一件事
- 避免"上帝类"，即包含过多功能的大类
- 服务类名称应该清晰表达其功能职责
- 不应使用部分类（partial class）来规避单一职责原则，大类应拆分为多个独立类而非部分类
- 部分类应仅用于分离设计器生成代码或特定框架需求，不应作为避免拆分大类的方式

### 开闭原则 (OCP)

- 代码应该对扩展开放，对修改关闭
- 使用接口和抽象类进行扩展
- 优先考虑组合而非继承
- 避免在现有方法中添加条件判断

### 里氏替换原则 (LSP)

- 子类必须能够替换其基类
- 确保继承关系正确表达"是一种"关系
- 子类不应该抛出父类方法没有的异常
- 避免子类重写父类方法导致行为改变

### 接口隔离原则 (ISP)

- 接口应该小而精炼，只包含客户端需要的方法
- 避免胖接口（也就是包含过多方法的接口）
- 根据客户端需求分离接口
- 优先使用多个特定的接口而非一个通用接口
- 接口方法数量应控制在 7 个以内，超过时应考虑拆分为多个更小的接口
- 接口命名应准确反映其功能职责，避免使用模糊的通用名称
- 当不同客户端仅使用接口的部分方法时，必须拆分为多个接口

### 依赖倒置原则 (DIP)

- 高层模块不应依赖低层模块，两者都应依赖抽象
- 抽象不应依赖细节，细节应依赖抽象
- 使用依赖注入传递依赖
- 服务注册应在程序入口点集中管理

## AutoCAD 访问规则

- 对 CAD 的访问必须通过唯一的封装访问点（如 AcadService）
- 避免在多处直接访问 Application.DocumentManager 等 AutoCAD 对象
- 所有 AutoCAD API 调用应集中在服务层
- CAD 访问服务不应包含复杂业务逻辑，应仅作为 AutoCAD API 的简单封装
- 复杂业务逻辑应放在专门的业务服务类中，由业务服务调用 CAD 访问服务
- **数据库对象必须在 Transaction 内访问**：通过 `TransactionService.ExecuteInTransaction` 或服务层 `GetObject<T>` 打开，禁止在事务外读写 DBObject
- **禁止使用 dynamic 类型访问 AutoCAD 对象**；必须使用强类型（Entity、BlockReference、LayerTableRecord 等），通过 `Transaction.GetObject(id, OpenMode)` 获取

## 异常处理规则

- 捕获所有可能的异常，不允许任何异常传播到调用者
- 使用 `OperationResult<T>` 或 `OpResult` 作为返回值类型，包含执行状态和结果
- 在日志中详细记录异常信息，但在用户界面中只显示简洁的错误消息

## 命令结构规则

- 所有命令都必须支持 CANCEL 操作取消
- 命令必须严格分为三个部分：输入获取、主体逻辑、输出显示
- 输入获取和输出显示应位于命令类中，主体逻辑应位于服务类中
- 非命令和非输入输出模块不应直接进行输入输出操作
- 通过参数获取输入，通过返回值提供输出给命令模块

## 命令行交互规则

- 隐藏自动调用命令的命令行输入
- 命令行输出信息应简洁明了，仅显示用户需要的核心信息
- 避免在命令行显示技术细节和 Debug 信息

## 日志和错误处理

- 日志应详细记录操作过程、参数和结果
- 每个异常必须记录到日志，包括异常类型、消息和堆栈跟踪
- 系统发生错误时，自动读取日志获得更多信息并提供给报告机制

## 代码注释规则

为每个函数添加标准 XML 文档注释，包含：
- `<summary>`：说明函数目的
- `<param>`：描述每个参数的作用和类型约束
- `<returns>`：说明返回值含义
- `<exception>`：列出可能抛出的异常
- `<remarks>`：说明实现方法或注意事项（如有必要）

## 代码审查违规检查

### SOLID 原则违规

- **SRP**：类有多个修改原因，方法行数超过 20 行；使用部分类分散职责而非真正拆分类；类总行数超过 200 行；类中方法和属性组合超过 15 个
- **OCP**：修改现有类而不是扩展，条件判断语句过多
- **LSP**：子类重写方法改变了基类行为，子类抛出基类方法没有的异常
- **ISP**：接口包含客户端不需要的方法，实现类存在空实现；接口方法数量超过 7 个；不同客户端仅使用接口的不同部分方法；接口命名不精确或过于通用
- **DIP**：直接依赖具体类而非接口，使用 new 操作符直接创建依赖对象

### AutoCAD 访问违规

- 在多处直接访问 AutoCAD 对象而不通过封装服务
- CAD 访问服务包含复杂业务逻辑
- 业务逻辑和 CAD 访问逻辑混合在一起
- 在 Transaction 外打开或修改数据库对象（DBObject、Entity 等）
- 使用 dynamic、ExpandoObject 或反射动态访问 AutoCAD 对象属性，而非强类型 API

### 异常处理违规

- 允许异常传播到调用者而不捕获
- 没有使用 `OperationResult<T>` 或 `OpResult` 返回操作结果
- 异常信息没有记录到日志

### 命令结构违规

- 命令不支持 CANCEL 操作
- 命令中混合了输入获取、业务逻辑和输出显示
- 非命令模块直接进行输入输出操作

### 命令行交互违规

- 在命令行显示技术细节和 Debug 信息
- 没有隐藏自动调用命令的命令行输入
- 使用了 PowerShell 特有语法而非 Windows CMD 语法

### 代码注释违规

- 函数缺少 XML 文档注释
- 注释不完整或不准确

## AutoCAD 单元测试安全规则（血泪教训，不可违反）

### 1. 禁止在测试中操作全局图层状态

- 严禁在单元测试中调用 `UnlockAndThawAllLayers()`、`RestoreLayerStates()` 等遍历整个图层表并以写模式打开所有图层的方法
- 这类方法会与 NUnit 并发运行的其他测试产生写锁竞争，必然导致死锁卡死
- 此类方法只能在命令层（Command）中调用，不能在测试中直接测试

### 2. 禁止在同一事务中混用创建和遍历写操作

- 在一个 `ExecuteInTransaction` 中，禁止同时：
  - 创建图层/线型（CreateLayer、CreateLineType）
  - 遍历图层表写操作（UnlockAndThawAllLayers 等）
- 正确做法：用多个 Action 分离到不同事务，如 Action1 创建、Action2 操作

### 3. 禁止在测试中调用 UpgradeOpen()

- 在 `ExecuteInTransaction` 的 Action 中，禁止对已通过服务方法打开的对象再调用 `UpgradeOpen()`
- 同一事务内对同一对象重复请求写锁会导致死锁
- 服务方法（GetObject、CreateLayer 等）已自行管理打开模式，测试代码不应干预

### 4. 禁止在测试中修改 db.Clayer（当前图层）

- 在事务内修改 db.Clayer 会导致 AutoCAD 挂起
- 如需测试"当前图层保护"逻辑，直接读取 db.Clayer 的 ID 传入快照即可，不要切换当前图层

### 5. 禁止在测试中使用 Assert.DoesNotThrow 包裹 AutoCAD 操作

- NUnit 的 Assert.DoesNotThrow 会拦截 AutoCAD 内部异常，行为不可预期
- AutoCAD 操作的异常安全性应在服务层用 try-catch + OpResult 保证，测试层直接断言 IsSuccess 即可

### 6. 全局状态类功能的测试策略

- 凡是操作 AutoCAD 全局状态（图层、线型、块表等）的服务方法，应通过集成测试验证：
  - 直接在 AutoCAD 中运行对应命令（如 BlockCleanup），观察命令是否正常完成
  - 不要在 RunTests 自动化测试套件中测试这类方法
- 自动化单元测试只适合测试：只读操作、局部状态修改、纯逻辑计算

### 7. 新测试加入项目前的验证流程（必须遵守）

- 新建测试类后，必须先将其排除在 csproj 之外
- 在排除新测试的情况下确认 RunTests 正常完成（作为基准）
- 再将新测试加入 csproj，重新运行 RunTests，确认不卡死
- 如果加入后卡死，立即从 csproj 移除，用二分法逐个方法排查
- 不要反复修改测试逻辑猜测原因，应先定位到具体的卡死方法再修复

### 8. 添加新功能必须同步提供测试覆盖（不可忽略）

- 每次新增服务方法、命令或工具类，必须同步编写对应的单元测试
- 测试覆盖范围：正常路径、边界情况（null 输入、空集合、无效 ID）、失败路径
- 测试必须与功能代码在同一次 git 提交中提交，不允许先提交功能再补测试
- 若新功能涉及 AutoCAD 全局状态（图层表、块表等），按规则 6 改用手动集成测试，但必须在提交说明中注明"已通过 [命令名] 手动验证"
- 测试文件命名规范：`{服务类名}Tests.cs` 放在 ServiceTests 目录下

### 9. 图纸污染问题（2026-06-12 血泪教训）

- 会永久修改图纸的测试（爆炸块、删除实体等）不应加入自动化测试套件
- 这类测试每次运行都会消耗共享测试数据，导致后续测试失败或结果翻倍
- 解决方案：改为手动集成测试，在 AutoCAD 中直接运行对应命令验证
- 如需自动化测试修改操作，必须在测试内部创建和清理专用数据，不依赖共享图纸
- 依赖共享图纸数据的断言应使用宽松条件（GreaterOrEqual），不硬编码具体数量

## AutoCAD 资源管理规则

版本：1.0.0  
最后更新：2024-06-28  
来源：`.cursor/rules/autocad_resource_management.mdc` 规则转换

### 一、必须显式释放的 AutoCAD 对象

#### 1. 事务类对象
- **Transaction**：必须通过 `Commit()` 或 `Abort()` 后调用 `Dispose()`
  ```csharp
  using (Transaction tr = db.TransactionManager.StartTransaction())
  {
      // 操作代码
      tr.Commit();
  } // 自动调用Dispose()
  ```
- **OpenCloseTransaction**：使用完必须释放
- **SubTransaction**：必须提交或回滚并释放

#### 2. 数据库对象
- **DBObject**：手动通过 `UpgradeOpen()`/`DowngradeOpen()` 打开的对象需要 `Close()`
- **BlockTableRecord** 和其他 **TableRecord**：手动打开后需要 `Close()`
- **DBDictionary**：手动打开后需要 `Close()`
- **自定义创建的 Database 对象**：不再使用时应调用 `Dispose()`

#### 3. 选择集对象
- **SelectionSet**：使用完后需调用 `Dispose()`
- **Editor.GetSelection()** 返回的 **PromptSelectionResult**：使用完需释放

#### 4. 锁定对象
- **Document.LockDocument()**：必须配对调用 `UnlockDocument()`
  ```csharp
  doc.LockDocument();
  try
  {
      // 操作代码
  }
  finally
  {
      doc.UnlockDocument();
  }
  ```

#### 5. 图形对象
- **自定义创建并添加到数据库的 Entity 对象**：确保在事务中提交
- **临时图形对象（Transient Entity）**：使用完后需移除并释放

### 二、不应手动释放的 AutoCAD 对象

#### 1. 文档和应用程序对象
- **Document 对象**：由 AutoCAD 管理生命周期，不要手动 Dispose
- **Application 对象**：全局单例，不要尝试释放
- **DocumentCollection**：由 AutoCAD 管理，不要释放

#### 2. 管理器对象
- **Database**：通常绑定到 Document，不要手动释放（除非是自己创建的）
- **TransactionManager**：由 Database 管理，不要释放
- **LayerManager**：由 AutoCAD 管理，不要释放
- **BlockManager**：由 AutoCAD 管理，不要释放

#### 3. 编辑器和命令对象
- **Editor**：由 Document 管理，不要释放
- **Command 对象**：由 AutoCAD 管理，不要释放
- **CommandMethod 类型的方法**：框架负责管理

#### 4. 已加入事务的对象
- **通过事务获取的 Entity/DBObject**：事务负责管理，不要手动释放
  ```csharp
  using (Transaction tr = db.TransactionManager.StartTransaction())
  {
      // 通过事务获取的对象由事务管理，不要手动Close
      BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
      
      // 事务结束时自动处理这些对象
      tr.Commit();
  }
  ```

### 三、资源管理最佳实践

#### 1. 使用 using 语句自动处理 IDisposable 对象
```csharp
using (Transaction tr = db.TransactionManager.StartTransaction())
{
    // 操作代码
    tr.Commit();
} // 自动调用Dispose()
```

#### 2. 使用 try-finally 结构确保资源释放
```csharp
Document doc = Application.DocumentManager.MdiActiveDocument;
doc.LockDocument();
try
{
    // 文档操作代码
}
finally
{
    doc.UnlockDocument();
}
```

#### 3. 通过 AcadService 统一管理资源
- 封装常用操作，确保资源正确释放
- 避免在多处手动管理相同资源
- 集中处理异常和资源释放逻辑

#### 4. 事务使用规范
- 事务应尽可能短小
- 避免在事务中执行耗时操作
- 事务结束前必须调用 Commit 或 Abort
- 确保事务在所有情况下都能正确结束（try-finally）

#### 5. Document 锁定规范
- 锁定时间应尽可能短
- 避免在锁定区域执行 UI 操作或等待用户输入
- 确保在异常情况下也能解锁（try-finally）
- 考虑使用 `DisableUndoRecording` 简化非关键操作
