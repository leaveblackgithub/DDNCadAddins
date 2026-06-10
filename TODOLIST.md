# DDNCadAddins项目待办事项清单

## 1. SOLID原则优化
- [ ] 参照SOLIDCheck_Instructions.txt进行全面检查
- [ ] 重构违反单一职责原则(SRP)的类和方法
- [ ] 优化依赖关系，确保符合依赖倒置原则(DIP)
- [ ] 检查并修复Interface隔离原则(ISP)违规点
- [ ] 审查继承层次结构，确保符合里氏替换原则(LSP)

## 2. 单元测试覆盖
- [ ] 为核心业务逻辑补充单元测试（BlockService、TransactionService 等仍有扩展空间）
- [ ] 增加模拟(Mock)对象，隔离对AutoCAD API的依赖
- [ ] 生成代码覆盖率报告（当前仅有 ExtentReports HTML 测试结果）
- [ ] 准备 `xclip.dwg` 测试数据，消除 RUNTESTS 中依赖该图纸的 Skipped 用例

## 3. 构建与验证
- [ ] 使用根目录的 build.bat 执行完整构建（或确认与 VS 构建流程一致）
- [ ] 在 AutoCAD 中验证全部命令（不仅 RUNTESTS）正常加载与运行

## 进度记录
| 日期 | 完成任务 | 备注 |
|------|----------|------|
| 2026-06-10 | 修复编译错误 | 解决方案可成功构建；原 ITestFixture 问题属旧 reference 代码，当前 src 无此错误 |
| 2026-06-10 | 单元测试基础设施 | OpResult/PropertyUtils/ConstructorUtils 单元测试，ServiceTests 扩展测试，NUnit + RUNTESTS |
| 2026-06-10 | RUNTESTS 报告路径 | 输出目录改为本项目 `src\bin\Debug\ExtentReports\` |
| 2026-06-10 | 修复失败用例 | ConstructorUtils、PropertyUtils 实现修复；ExampleTests.TestFail 标记 Ignore |
| 2026-06-10 | RUNTESTS 命令说明 | 补充 xclip.dwg 依赖说明与当前图纸检测提示 |
| 2026-06-10 | RUNTESTS 异常处理 | 区分 AutoCAD API 异常与 System 异常 |
