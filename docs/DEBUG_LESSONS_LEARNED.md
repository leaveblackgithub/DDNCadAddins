# CROPHATCH 调试经验教训

> 日期: 2026-07-05 | 涉及: CROPHATCH / CROPCLOSEDCURVE / GENERATEHATCHBOUNDARY

---

## 问题1: CROPHATCH 生成折线而非曲线

### 症状
CROPHATCH 调用 GenerateHatchBoundary + CropClosedCurveMulti 后，生成的裁剪结果是折线而非保持曲线形态。

### 根因
`GenerateHatchBoundary` 对 NURBS 环调用 `CreateCurveFitPolylineFromNurb`，生成 `Polyline2d` + `CurveFit()`。但 `ConvertToExactSegments` 和 `ConvertCurveToPolygon` 都没有处理 `Polyline2d` 类型，走默认的 `ConvertGenericClosedCurve` 只用 50 个采样点，丢失了 CurveFit 后的曲线信息。

### 修复
在两个转换器中都添加 `Polyline2d` 专用分支，用 200 个采样点沿参数空间均匀采样。

### 教训
1. **任何新增的 AutoCAD 实体类型都必须检查所有转换器是否支持** — `CurveToPolygonConverter` 和 `CurveToExactSegmentConverter` 需要同步更新
2. **`Polyline2d` + `CurveFit()` 不是真正的参数化曲线** — 它只是拟合后的多段线，无法精确提取曲线参数
3. **采样密度不足是"折线感"的常见原因** — 默认 50 点对简单曲线够用，但对复杂 NURBS 环不够，需要 200+ 点

---

## 问题2: 事务嵌套导致实体读取失败

### 症状
CROPHATCH 将 GenerateHatchBoundary、CreateCurveSelection、CropClosedCurveMulti 全部包在 `ExecuteInCommandTransaction` 内，结果新生成的曲线无法被后续步骤读取。

### 根因
各方法内部各自开 `ExecuteInTransactions`，嵌套事务导致事务隔离 — 内层事务提交的实体在外层事务中不可见。

### 修复
移除 CROPHATCH 外层的事务包裹，让每个步骤自行管理事务。每个步骤独立事务，不嵌套。

### 教训
1. **不要在一个大事务中包裹多个内部事务** — 每个方法应该自己管理事务
2. **CROPCLOSEDCURVE 的 `CropClosedCurveMulti` 方法内部已经管理事务** — 调用方不应再包一层事务
3. **如果方法返回 ObjectId，优先使用返回的 ID 而非通过对比前后集合来推断**

---

## 问题3: Spline 构造函数不闭合

### 症状
尝试用 `Spline(fitPoints, 3, 0.0)` 替代 `Polyline2d`，结果生成的 Spline 没有闭合且与原始 NURBS 有偏差。

### 根因
AutoCAD 2019 的 `Spline(Point3dCollection, int, double)` 构造函数没有 `closed` 参数，且拟合精度与原始曲线有偏差。

### 修复
回退到 `Polyline2d` + `CurveFit()`，改为提高采样密度来保持精度。

### 教训
1. **不要轻易替换已经验证过的实体类型** — `Polyline2d` + `CurveFit()` 虽然不完美但经过验证
2. **AutoCAD API 的构造函数行为需要仔细查阅文档** — 不能假设有 `closed` 参数
3. **增量修复优于替换** — 在现有路径上提高采样密度比换用新实体类型更安全

---

## 问题4: 语义反转（Subject/Clip 搞反）

### 症状
第一次实现时把 Subject（被减曲线）和 Clip（减去曲线）的交互顺序搞反了。

### 根因
误解了"被减曲线"和"减去曲线"的语义 — 被减曲线应该是多选的 Subjects，减去曲线应该是单选的 Clip。

### 修复
交换 Step 1 和 Step 2 的交互顺序。

### 教训
1. **语义确认比代码实现更重要** — 先确认"谁减谁"再写代码
2. **其他 CROP 命令的模式是"先选边界，再选被剪对象"** — 新命令应遵循此模式

---

## 通用策略

### 调试流程
1. 查看 TestRecord JSON 确认输入/输出
2. 检查 ConvertToExactSegments 是否支持所有实体类型
3. 检查事务嵌套
4. 检查采样密度

### 优先原则
1. **精确优先** — 优先使用精确参数化（直线/圆弧/椭圆弧）
2. **高密度采样次之** — 无法精确时用高密度采样
3. **降级策略** — 主方案失败时降级到备选方案

### 代码审查清单
- [ ] 新实体类型是否在所有转换器中注册？
- [ ] 事务是否嵌套？
- [ ] 采样密度是否足够？
- [ ] 语义是否与现有命令一致？
- [ ] 是否遵循"先选边界，再选被剪对象"的模式？

---

## 问题5: CropBlockService 测试在侧数据库中卡死（BlockExploder）

### 症状
`CropBlockServiceTests` 中的 Intersects 测试在侧数据库（`new Database(true, true)`）中执行时 AutoCAD 完全卡死。

### 根因
`CropBlockService.CropBlocks` → `ExplodeAndCropChildren` → `BlockExploder.Explode()` → `AppendEntitiesToCurrentSpace(entitiesToAdd)` → `GetCurrentSpace()`。在侧数据库中无活动文档，`db.CurrentSpaceId` 行为未定义，导致死循环。

### 修复
移除会触发 Intersects → Explode 路径的测试。只保留 Inside/Outside 纯包围盒分类测试，不涉及 `BlockExploder`。

### 教训
1. **BlockExploder.Explode() 不可在侧数据库中使用** — `AppendEntitiesToCurrentSpace` 依赖活动文档
2. **侧数据库测试只适合纯逻辑/纯数学运算** — 任何涉及文档上下文的操作（爆炸、XClip 复制等）都会卡死
3. **测试新服务时先验证最小路径（Inside/Outside）再添加复杂路径（Intersects/Explode）**
4. **二分法排查卡死** — 逐个排除测试文件，每次只加回一个，快速定位

---

## 问题6: .csproj 显式文件列表遗漏新文件

### 症状
`MSBuild` 编译成功但 `AUTOCMDTESTS` 找不到新测试类，提示 `NullReferenceException`，堆栈行号指向旧代码。

### 根因
[`AddinsACAD.csproj`](src/AddinsACAD/AddinsACAD.csproj) 使用显式 `<Compile Include="..." />` 列表。新创建的 `.cs` 文件不会被自动包含，需要手动添加。

### 教训
1. **创建新文件后必须在 `.csproj` 中注册** — VS 自动添加但 CLI MSBuild 不会
2. **检查 MSBuild 输出中是否包含新文件路径** — 无则说明未编译
3. **堆栈行号指向旧代码** — 说明 DLL 没变，检查编译步骤

---

## 问题7: `OpResult<T>` 双命名空间歧义

### 症状
```
error CS0104: “OpResult<>”是“DDNCadAddins.Core.Models.OpResult<T>”和“ServiceACAD.OpResult<T>”之间的不明确的引用
```

### 根因
`AddinsACAD` 同时引用了 `DDNCadAddins.Core`（其中 `Models/OpResult.cs` 定义了 `OpResult<T>`）和 `ServiceACAD`（其中 `OpResult.cs` 也定义了 `OpResult<T>`）。`using ServiceACAD;` 和 `using DDNCadAddins.Core.Models;` 同时存在时导致歧义。

### 修复
移除 `using DDNCadAddins.Core.Models;`，使用全限定名 `ServiceACAD.OpResult<T>`。

### 教训
1. **ServiceACAD 和 DDNCadAddins.Core 都有 OpResult<T>** — 不可同时 using
2. **AddinsACAD 层应始终用 `ServiceACAD.OpResult<T>`** — 因为 ServiceACAD 包含了 AutoCAD 相关的 OpResult

---

## 问题8: MLine 托管 API 限制

### 症状
```csharp
var mline = new Mline();
mline.SetElevation(0.0); // CS1061
mline.SetScale(1.0);     // CS1061
mline.AddVertex(...);    // CS1061
```

### 根因
`Mline` 的 `SetElevation`、`SetScale`、`AddVertex` 方法是 COM/ActiveX 接口 `IMLine` 的成员，不在托管 API `Autodesk.AutoCAD.DatabaseServices.Mline` 中。托管 API 通过构造函数参数设置顶点。

### 教训
1. **MLine 不可通过托管 API 编程创建** — 只能通过 `vla-addmline`（LSP）或通过读取已有图纸对象
2. **MLine 裁剪测试仅限 LSP + 手动验证** — 用 `CREATETESTMLINE` 创建，手动执行裁剪命令

---

## 问题9: Polyline3d 构造函数需要 Point3dCollection

### 症状
```csharp
new Polyline3d(Poly3dType.SimplePoly, new[] { pt1, pt2 }, false); // CS1503
```

### 根因
`Polyline3d` 第二个参数要求 `Point3dCollection`，不是 `Point3d[]`。数组不能隐式转换为 `Point3dCollection`。

### 教训
1. **AutoCAD 集合类型（Point3dCollection/ObjectIdCollection 等）是专用类不是数组** — 必须 `new Point3dCollection()` + `.Add()`
2. **检查构造函数重载签名** — `.NET API` 文档中参数类型优先于猜测

---

## 问题10: 侧数据库中 `AppendEntityToCurrentSpace` 不可用

### 症状
```
NullReferenceException at TransactionService.GetCurrentSpace()
```

### 根因
[`GetCurrentSpace`](src/ServiceACAD/TransactionService.cs:392) 在侧数据库中调用 `db.CurrentSpaceId`（纸空间模式）返回无效 ID，导致 `GetObject` 返回 null。侧数据库默认为模型空间，需要用 `GetModelSpace` 替代。

### 教训
1. **`AppendEntityToCurrentSpace` → `AppendEntityToModelSpace`** — 侧数据库必须用后者
2. **`CreateBlockRefInCurrentSpace` 也依赖 `GetCurrentSpace`** — 侧数据库中不可用
3. **全项目搜索 `GetCurrentSpace` 调用** — 所有侧数据库场景都要规避

---

## 问题11: AUTOCMDTESTS 报告路径

### 规则
`AUTOCMDTESTS` 的 NUnit XML 报告输出到：
```
D:\leaveblackgithub\DDNCadAddins\src\bin\Debug\ExtentReports\Report-NUnit.xml
```

调试测试失败时优先查看此文件，而非项目根目录的 `TestResult.xml`（那是 Core.Tests 的结果）。

### Core.Tests 命令行
```cmd
cmd /c ""C:\Users\CFDDN\.nuget\packages\nunit.consolerunner\3.16.3\tools\nunit3-console.exe" "d:\leaveblackgithub\DDNCadAddins\src\bin\Debug\DDNCadAddins.Core.Tests.dll" --noheader"
```
