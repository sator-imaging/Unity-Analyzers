[![nuget](https://img.shields.io/nuget/vpre/SatorImaging.UnityAnalyzers)](https://www.nuget.org/packages/SatorImaging.UnityAnalyzers)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/Unity-Analyzers)

[🇺🇸 English](./README.md)
&nbsp; ❘ &nbsp;
[🇯🇵 日本語版](./README.ja.md)
&nbsp; ❘ &nbsp;
[🇨🇳 简体中文版](./README.zh-CN.md)





用于 Unity 开发的 Roslyn 分析器，确保代码安全且正确。


# 异步方法分析

`UnityAsyncMethodAnalyzer` 用于确保在 `async` 方法中安全地使用 `UnityEngine.Object`（MonoBehaviour、ScriptableObject 等）。

它会检查所有类型中声明的 `async` 方法，包括普通的 C# 类、结构体、记录类型（不仅限于 `MonoBehaviour` 或 Unity 特定类型）。

## `SIUA001`: 不可靠的 Unity 对象访问

**严重性: Error**

在 `async` 方法中访问 Unity 对象的实例成员（方法、属性、字段、事件）是潜在不安全的，因为底层的原生对象可能被销毁，而托管包装对象仍然存在。

**规则:**
必须使用严格的 null 检查 `if (obj != null)` 来保护访问。

**命中原因:**
- 对 `UnityEngine.Object` 派生类型的任意实例成员访问。
- 使用 `?.` 运算符（例如 `obj?.Prop`）被视为 **不安全**，因为它绕过了 Unity 用于生命周期校验的自定义相等性检查。

**安全示例:**
```csharp
if (unityObject != null)
{
    unityObject.DoSomething(); // OK
}
```

**不安全示例:**
```csharp
await Task.Yield();

unityObject.DoSomething(); // Error: SIUA001
unityObject?.DoSomething(); // Error: SIUA001 (?. 被视为绕过)
```

**首个 await 的例外:**

在第一个 `await` 表达式 **之前与内部** 访问 Unity 对象是安全的，不会触发 SIUA001。

```csharp
// 安全：在任何 await 之前访问
unityObject.DoSomething();

// 安全：在首个 await 表达式内部访问
await unityObject.SomeAsyncMethod();

// 错误：首个 await 完成之后访问
unityObject.DoSomethingElse(); // Error: SIUA001
```

原因是 `async` 方法在首次让出控制权之前，Unity 对象不会被销毁。

## `SIUA002`: 安全块内的 await

**严重性: Warning**

在“安全块”（`if (obj != null)` 块）中使用 `await` 会使安全保证失效。当 `await` 后续恢复时，Unity 对象可能已经被销毁。

**规则:**
不要在受 Unity 对象 null 检查保护的块中使用 `await`。

**不安全示例:**
```csharp
if (unityObject != null)
{
    await Task.Delay(100);

    // 恢复时对象可能已被销毁
    unityObject.DoSomething(); 
}
// Warning: SIUA002 报告在 'await' 上
```

**正确示例:**
如果需要访问对象，请在 `await` 之后再进行 null 检查。

```csharp
await Task.Delay(100);

if (unityObject != null)
{
    unityObject.DoSomething();
}
```

---

## 通用分析行为

以下规则与限制适用于 `UnityAsyncMethodAnalyzer` 的所有诊断。

### 安全块定义（严格性）

分析器对“安全块”的判定非常严格。
- `if` 条件中的 **所有** 条件必须是 Unity 对象的 null 检查。
- 只要与其他条件组合（例如 `unityObj != null && someBool`），该块即被视为 **不安全**。
- 通常只接受 `if` 语句（三元 `? :` 通常不视为安全块）。
- 条件必须是直接与 `null` 比较的不等式（如 `x != null`），或这些比较通过 `&&`（逻辑与）组合。
- **复杂条件**（例如 `||`、`is not null` 的模式匹配、辅助方法）可能被 **误判**（视为不安全块）。

### 限制: 无数据流分析

分析器不会验证被检查的变量与使用的变量是否一致。只要检测到 **有效的 Unity 对象 null 检查**，就会将整个块视为“安全”。

**对 SIUA001 的影响:**
如果检测到任意 Unity 对象检查，就跳过该块的分析，即使实际访问的是另一个未检查的对象也不会报告。

```csharp
// 视为有效（跳过分析）
// 因为检查了 Unity 对象 'foo'，该块被当作“安全”。
// 即使访问了未检查的 'bar' 也不会报错（假阴性）。
if (foo != null) 
{
    bar.DoSomething(); // No Error (False negative)
}
```

**对 SIUA002 的影响:**
即便检查的对象与 await 的目标无关，也会严格禁止在该块内使用 `await`。

```csharp
// Warning（严格执行）
if (foo != null) 
{
    await Task.Delay(10); // Warning: SIUA002
}
```

### 限制: 早退模式

分析器不进行控制流分析，因此“早退”式的检查不会被视为安全块。

**解决方法:**
反转条件并使用 `else` 块（或把代码放进 `if` 内）。

```csharp
// 不安全（此形式不被识别）
if (unityObject == null) return;

unityObject.DoSomething(); // Error: SIUA001
```

```csharp
// 安全（使用 else 块）
if (unityObject != null)
{
    unityObject.DoSomething(); // OK
}
else
{
    return;
}
```

此解决方法同样适用于 **SIUA002**：

```csharp
// 不安全（await 在 if 内部，会触发 SIUA002）
if (unityObject != null)
{
    unityObject.DoSomething();
    await DoFurtherAsync(); // Warning: SIUA002
}
```

```csharp
// 安全（将 await 移出检查块）
if (unityObject != null)
{
    unityObject.DoSomething();
}
else
{
    return;
}

await DoFurtherAsync(); // OK
```
