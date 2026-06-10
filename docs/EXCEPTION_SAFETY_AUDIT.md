# AutoCAD 异常安全审计清单

## 背景
**最高原则**: AutoCAD 进程中任何未捕获的异常都会导致致命错误（Crash）。所有方法必须使用 `OpResult/OpResult<T>` 作为返回类型，并在内部完整捕获所有异常。

## 审计规则

### ✅ 符合规范的代码模式
```csharp
public OpResult<Data> GetData()
{
    try
    {
        // 操作逻辑
        return OpResult<Data>.Success(result);
    }
    catch (Exception ex)
    {
        Logger._.Error($"操作失败: {ex.Message}", ex);
        return OpResult<Data>.Fail($"操作失败: {ex.Message}");
    }
}
```

### ❌ 需要修复的代码模式

1. **直接抛出异常**
```csharp
// ❌ 错误
public void DoSomething()
{
    throw new InvalidOperationException("错误");
}

// ✅ 正确
public OpResult DoSomething()
{
    try
    {
        // 逻辑
        return OpResult.Success();
    }
    catch (Exception ex)
    {
        Logger._.Error($"操作失败: {ex.Message}", ex);
        return OpResult.Fail($"操作失败: {ex.Message}");
    }
}
```

2. **void 方法（无法传递错误状态）**
```csharp
// ❌ 错误
public void UpdateData()
{
    // 如果出错，无法告知调用者
}

// ✅ 正确
public OpResult UpdateData()
{
    try
    {
        // 逻辑
        return OpResult.Success();
    }
    catch (Exception ex)
    {
        Logger._.Error($"更新失败: {ex.Message}", ex);
        return OpResult.Fail($"更新失败: {ex.Message}");
    }
}
```

3. **缺少 try-catch 保护的 AutoCAD API 调用**
```csharp
// ❌ 错误
public ObjectId CreateEntity(Entity entity)
{
    return modelSpace.AppendEntity(entity); // 可能抛出异常
}

// ✅ 正确
public OpResult<ObjectId> CreateEntity(Entity entity)
{
    try
    {
        var id = modelSpace.AppendEntity(entity);
        return OpResult<ObjectId>.Success(id);
    }
    catch (Exception ex)
    {
        Logger._.Error($"创建实体失败: {ex.Message}", ex);
        return OpResult<ObjectId>.Fail($"创建实体失败: {ex.Message}");
    }
}
```

## 待审计文件清单

### ServiceACAD 核心服务层
- [x] `OpResult.cs` - 已符合规范（工具类）
- [ ] `TransactionService.cs` - 需审计
- [ ] `BlockService.cs` - 需审计
- [ ] `DocumentService.cs` - 需审计
- [ ] `EditorService.cs` - 需审计
- [ ] `TransactionServiceForBlock.cs` - 需审计
- [ ] `TransactionServiceForEntity.cs` - 需审计
- [ ] `TransactionServiceForStyle.cs` - 需审计
- [ ] `CadServiceManager.cs` - 需审计
- [x] `PropertyUtils.cs` - 已符合规范（静态工具类）
- [x] `ConstructorUtils.cs` - 已符合规范（静态工具类）
- [ ] `Logger.cs` - 需审计（异常记录本身不应抛异常）

### AddinsACAD 命令层
- [ ] `BlockCleanupCommand.cs` - 需审计
- [ ] `ExplodeAsShownCommand.cs` - 需审计
- [ ] `GenerateXclipBoundaryCommand.cs` - 需审计

## 优先级排序

### P0 - 最高优先级（直接与 AutoCAD API 交互）
1. `TransactionService.cs` - 核心事务管理
2. `BlockService.cs` - 块操作
3. `DocumentService.cs` - 文档管理
4. `EditorService.cs` - 编辑器交互

### P1 - 高优先级（组合多个 AutoCAD 操作）
1. `TransactionServiceForBlock.cs`
2. `TransactionServiceForEntity.cs`
3. `TransactionServiceForStyle.cs`

### P2 - 中优先级（命令入口）
1. 所有 Command 类

### P3 - 低优先级（工具类）
1. `CadServiceManager.cs`
2. `Logger.cs`

## 审计步骤

1. **搜索 void 方法**: 检查是否应改为 `OpResult`
2. **搜索 throw 语句**: 确认是否在 try-catch 内部且不向上传播
3. **检查 AutoCAD API 调用**: 确认是否被 try-catch 保护
4. **验证接口签名**: 确保接口方法也返回 `OpResult`
5. **测试验证**: 故意触发错误，确认不会 Crash AutoCAD

## 修复模板

```csharp
// 修复前
public void MethodName()
{
    var obj = Transaction.GetObject(id); // 可能抛异常
    obj.DoSomething();
}

// 修复后
public OpResult MethodName()
{
    try
    {
        var obj = Transaction.GetObject(id);
        if (obj == null)
        {
            return OpResult.Fail("对象不存在");
        }
        
        obj.DoSomething();
        return OpResult.Success();
    }
    catch (Exception ex)
    {
        Logger._.Error($"MethodName 失败: {ex.Message}", ex);
        return OpResult.Fail($"操作失败: {ex.Message}");
    }
}
```

## 进度跟踪

| 日期 | 审计文件 | 发现问题数 | 已修复 | 审计人 |
|------|----------|-----------|--------|--------|
|      |          |           |        |        |
