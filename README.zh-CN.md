[![nuget](https://img.shields.io/nuget/vpre/SatorImaging.UnityAnalyzers)](https://www.nuget.org/packages/SatorImaging.UnityAnalyzers)
&nbsp;
[![🇯🇵](https://img.shields.io/badge/🇯🇵-日本語-789)](./README.ja.md)
[![🇨🇳](https://img.shields.io/badge/🇨🇳-简体中文-789)](./README.zh-CN.md)
[![🇺🇸](https://img.shields.io/badge/🇺🇸-English-789)](./README.md)





用于 Unity 开发的 Roslyn 分析器，确保代码安全且正确。

- [异步方法分析](#异步方法分析)
  - [SIUA001](#siua001-不可靠的-unity-对象访问): 不可靠的 Unity 对象访问
  - [SIUA002](#siua002-安全块内的-await): 安全块内的 await
- [异步调用分析](#异步调用分析)
  - [SIUA021](#siua021-检测到异步调用): 检测到异步调用
- [静态状态分析](#静态状态分析)
  - [SIUA011](#siua011-静态状态在播放模式之间存留): 静态状态在播放模式之间存留
  - [SIUA012](#siua012-runtimeinitializeonloadmethod-中缺少状态重置): RuntimeInitializeOnLoadMethod 中缺少状态重置
  - [SIUA013](#siua013-带有主体的静态属性可能返回无效的静态状态): 带有主体的静态属性可能返回无效的静态状态
  - [SIUA014](#siua014-带有主体的静态事件): 带有主体的静态事件





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

# 异步调用分析

`AsyncInvocationAnalyzer` 用于检测未被显式追踪的异步执行来源。

## `SIUA021`: 检测到异步调用

**严重性: Error**

此规则会对以下代码报错：
- 调用返回 `Task`、`Task<T>`、`ValueTask` 或 `ValueTask<T>` 的方法（直接 `await` 的调用除外）(e.g. `AsyncMethod();`)。
- 创建或赋值为 `async` / 返回 task-like 的匿名函数 (e.g. `Action a = async () => await Task.Delay(1);`, `Func<Task> f = () => Task.CompletedTask;`)。
- 将返回 task-like 的方法组赋给委托或事件处理器 (e.g. `eventHandler += TaskReturningMethod;`, `handler = TaskReturningMethod;`)。
- 对返回 task-like 的方法或受监视 API 类型进行方法组引用 (e.g. `var m = TaskReturningMethod;`)。
- 访问受监视 API 类型的字段/属性/事件 (e.g. `var p = synchronizationContext.Post;`, `var e = timer.Elapsed;`)。
- 创建受监视 API 类型的对象 (e.g. `new System.Threading.Timer(_ => { });`)。
- 调用受监视 API 类型的方法 (e.g. `Task.Run(() => { });`, `ThreadPool.QueueUserWorkItem(_ => { });`, `Parallel.For(0, 10, _ => { });`)。
- 受监视的线程/任务 API：
  - `System.Threading.Tasks.Task`
  - `System.Threading.Thread`
  - `System.Threading.ThreadPool`
  - `System.Threading.Tasks.Parallel`
  - `System.Threading.SynchronizationContext`
  - `System.Threading.Timer`
  - `System.Threading.PeriodicTimer`
  - `System.Timers.Timer`
  - `System.Threading.Tasks.TaskCompletionSource`
  - `System.Threading.Tasks.TaskCompletionSource<T>`

## Promise 类型名自定义

你可以通过 `.editorconfig` 自定义 Promise 例外类型名：

```ini
[*.cs]
unity_analyzers_promise_type_name = MyCustomPromise # Default: Promise
```

请使用精确键名 `unity_analyzers_promise_type_name`（`analyzers` 为复数）。

该值在分析器启动时只加载一次，随后会被缓存（修改后需要重启/重新加载才会生效）。

## 限制: `async void`

`async void` 没有可供调用方持有的 awaitable 句柄（`Task`/`ValueTask`），调用方无法可靠地追踪其完成与异常传播。  
因此，在调用追踪模式下，`async void` 流程在技术上无法被完整追踪。

# 静态状态分析

`UnityStaticStateAnalyzer` 确保在禁用域重新加载（Domain Reloading）时，静态状态不会在播放模式之间存留。

![](./docs/UnityStaticStateAnalyzer.gif)

## `SIUA011`: 静态状态在播放模式之间存留

**严重程度: Error**

在 Unity 中，如果禁用了域重新加载，静态字段和属性将在播放模式之间保持。这可能导致上一个会话的状态持久化到下一个会话中，从而引起意外行为。

**规则:**
静态字段和属性应在项目加载或进入播放模式时重置。这通常使用带有 `[RuntimeInitializeOnLoadMethod]` 属性的方法来完成。

**匹配原因:**
- 任何静态字段（不包括 `const` 或 `string`、原始类型或 `readonly struct` 等不可变类型的 `readonly` 字段）。
- 任何静态属性（不包括不可变类型的仅有 getter 的属性）。
- 类中不包含带有 `[RuntimeInitializeOnLoadMethod]` 或 `[RuntimeInitializeOnLoadMethodAttribute]` 属性的静态方法。

**安全模式:**
```csharp
public class MyService
{
    public static int Counter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Counter = 0;
    }
}
```

**不安全模式:**
```csharp
public class MyService
{
    public static int Counter; // Error: SIUA011
}
```

## `SIUA012`: RuntimeInitializeOnLoadMethod 中缺少状态重置

**严重性: Error**

需要重置的静态字段、属性和事件（参见 SIUA011）必须在带有 `[RuntimeInitializeOnLoadMethod]` 属性的方法中显式赋值。

**规则:**
类中的所有可变静态状态必须在初始化方法中通过简单赋值（`=`）进行重置。

**不安全模式:**
```csharp
public class MyService
{
    public static int Counter;
    public static string Status;

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
        Counter = 0;
        // Status 没有被重置！ -> Error: SIUA012 报告在 'Init' 上
    }
}
```

## `SIUA013`: 带有主体的静态属性可能返回无效的静态状态

**严重性: Warning**

带有 getter 主体（非自动实现）的静态属性在禁用域重新加载时可能会返回无效或过时的静态状态。

**规则:**
考虑改用自动实现的属性（例如 `static int Property { get; } = 0;`），或确保返回的值得到了正确管理。

**命中原因:**
- 任何非自动实现的（具有主体或表达式主体的）静态只读属性。

**不安全模式:**
```csharp
public class MyService
{
    // Warning: SIUA013
    public static int Counter => 123;
}
```

**安全模式:**
```csharp
public class MyService
{
    public static int Counter { get; } = 123;
}
```

## `SIUA014`: 带有主体的静态事件

**严重性: Warning**

不允许带有自定义 `add` 或 `remove` 主体的静态事件。分析器只能可靠地追踪和重置自动实现的静态事件。

**规则:**
静态事件必须是自动实现的。（例如 `public static event Action OnSomething;`）

**不安全模式:**
```csharp
public class MyService
{
    // Error: SIUA014
    public static event Action OnSomething { add { ... } remove { ... } }
}
```

**安全模式:**
```csharp
public class MyService
{
    public static event Action OnSomething;
}
```

> [!TIP]
> 如果您尝试在 `Directory.Build.props` 或类似的共享文件中引用分析器，请使用以下条件来仅在 C# 项目为 Unity 项目时启用它：
> `Condition=" $([System.String]::Copy('$(DefineConstants)').IndexOf('UNITY_5_6_OR_NEWER')) != -1 "`
>
> **完整的 `.props` 示例：**
> ```xml
> <Project>
>
>     <!-- UnityAnalyzers -->
>     <ItemGroup Condition=" $([System.String]::Copy('$(DefineConstants)').IndexOf('UNITY_5_6_OR_NEWER')) != -1 ">
>         <PackageReference Include="SatorImaging.UnityAnalyzers" Version="*-*">
>             <PrivateAssets>all</PrivateAssets>
>             <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
>         </PackageReference>
>     </ItemGroup>
>
> </Project>
> ```
