# AUTOCMDTEST 测试体系

## 概述

AUTOCMDTEST 是在 AutoCAD 内部运行的 NUnit 测试套件，使用**侧数据库（Side Database）** 执行，不影响当前图纸。

## 测试入口

- **命令**: `AUTOCMDTESTS`
- **文件**: [`src/AddinsACAD/TestCommands/AutoCADTestsCommand.cs`](src/AddinsACAD/TestCommands/AutoCADTestsCommand.cs)
- **测试类**: [`src/AddinsACAD/ServiceTests/`](src/AddinsACAD/ServiceTests/)
- **测试结果**: `src/bin/Debug/ExtentReports/Report-NUnit.xml`

## 测试列表

| 测试类 | 测试数 | 说明 |
|--------|--------|------|
| `CropArcServiceTests` | 10 | 弧裁剪（基本4 + 拆分5 + 边界/异常3，实际10） |
| `CropCircleServiceTests` | 8 | 圆裁剪（基本4 + 拆分3 + 边界/异常3，实际8） |
| `CropLineServiceTests` | 9 | 直线裁剪（基本4 + 拆分3 + 边界/异常3，实际9） |
| `CropPolylineServiceTests` | 8 | 多段线裁剪（基本4 + 拆分3 + 边界/异常3，实际8） |
| `CropSplineServiceTests` | 7 | 样条曲线裁剪（基本4 + 拆分2 + 边界/异常3，实际7） |
| `CropEllipseServiceTests` | 8 | 椭圆裁剪（基本4 + 拆分3 + 边界/异常3，实际8） |
| `CropTextServiceTests` | 7 | 文字裁剪（基本4 + 边界2 + 边界/异常3，实际7） |
| `CropMTextServiceTests` | 7 | 多行文字裁剪（基本4 + 边界2 + 边界/异常3，实际7） |
| `CropPointServiceTests` | 7 | 点裁剪（基本4 + 边界2 + 边界/异常3，实际7） |
| `CropDimServiceTests` | 7 | 标注裁剪（基本4 + 边界2 + 边界/异常3，实际7） |
| `BlockServiceExtendedTests` | 3 | 块服务扩展测试 |
| `TransactionServiceTest` | 5 | 事务服务测试 |
| `TransactionServiceExtendedTests` | 18 | 事务服务扩展测试 |
| **合计** | **104** | |

> 注意：`CropServiceTestBase` 中的 2 个抽象方法不计入测试数。

## 测试对象

### LSP 脚本创建

- **文件**: [`scripts/create_test_objects.lsp`](scripts/create_test_objects.lsp)
- **命令**: `CREATETESTOBJECTS`
- 在 `AUTOCMDTEST` 图层上创建：裁剪边界矩形、直线、弧、圆、多段线、样条曲线、椭圆、文字、多行文字、点、Hatch

### 测试图纸

- **文件**: [`examples/XCLIP.dwg`](examples/XCLIP.dwg)
- 用于 BlockService 的 XClip 相关测试

## 编写新测试

### 步骤

1. 在 [`src/AddinsACAD/ServiceTests/`](src/AddinsACAD/ServiceTests/) 下创建 `CropXxxServiceTests.cs`
2. 继承 `CropServiceTestBase`
3. 实现 `NullBoundary_Fail()` 和 `EmptyList_Fail()` 抽象方法
4. 在 [`src/AddinsACAD/AddinsACAD.csproj`](src/AddinsACAD/AddinsACAD.csproj) 中添加编译项
5. 在 AutoCAD 中执行 `AUTOCMDTESTS` 验证

### 模板

```csharp
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DDNCadAddins.Core.Interfaces;
using NUnit.Framework;
using ServiceACAD;
using CorePoint2D = DDNCadAddins.Core.Models.Point2D;

namespace AddinsACAD.ServiceTests
{
    [TestFixture]
    public class CropXxxServiceTests : CropServiceTestBase
    {
        private CropXxxService CreateService() => new CropXxxService(Geometry);

        // 1. 基本 (4)
        [Test] public void Inside_Kept() => SideDb(tr => { ... });
        [Test] public void Outside_Deleted() => SideDb(tr => { ... });
        [Test] public void Outside_Kept_KeepOutside() => SideDb(tr => { ... });
        [Test] public void Inside_Deleted_KeepOutside() => SideDb(tr => { ... });

        // 2. 边界 (2)
        [Test] public void OnBoundary_Deleted_KeepInside() => SideDb(tr => { ... });
        [Test] public void OnBoundary_Kept_KeepOutside() => SideDb(tr => { ... });

        // 3. 边界/异常 (3)
        protected override void NullBoundary_Fail() => SideDb(tr => { ... });
        protected override void EmptyList_Fail() => SideDb(tr => { ... });
        [Test] public void InvalidId_Skipped() => SideDb(tr => { ... });

        private static List<ObjectId> Xxx(ITransactionService tr, ...) { ... }
    }
}
```

### 注意事项

- 侧数据库中没有有效文字样式，`GeometricExtents` 可能抛出 `eInvalidExtents`
- 对于非曲线实体（Text/MText/Point/Dim），使用 `Assert.GreaterOrEqual` 宽松断言
- 每个测试文件需在 `.csproj` 中添加 `<Compile Include="ServiceTests\CropXxxServiceTests.cs" />`
