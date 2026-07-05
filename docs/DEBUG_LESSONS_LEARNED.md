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
