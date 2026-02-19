[![nuget](https://img.shields.io/nuget/vpre/SatorImaging.UnityAnalyzers)](https://www.nuget.org/packages/SatorImaging.UnityAnalyzers)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/Unity-Analyzers)

[🇺🇸 English](./README.md)
&nbsp; ❘ &nbsp;
[🇯🇵 日本語版](./README.ja.md)
&nbsp; ❘ &nbsp;
[🇨🇳 简体中文版](./README.zh-CN.md)





Roslyn analyzers to ensure safe and correct code when developing with Unity.


# Async Method Analysis

`UnityAsyncMethodAnalyzer` ensures safe usage of `UnityEngine.Object` (MonoBehaviour, ScriptableObject, etc.) within `async` methods.

It checks all `async` methods declared in any type, including standard C# classes, structs, and records (not just `MonoBehaviour` or Unity-specific types).

## `SIUA001`: Unreliable Unity object access

**Severity: Error**

Accessing instance members (methods, properties, fields, events) of a Unity object inside an `async` method is potentially unsafe because the underlying native object might be destroyed while the managed wrapper still exists.

**Rule:**
You must guard the access with a robust null check using `if (obj != null)`.

**Why it matches:**
- Any instance member access on a `UnityEngine.Object` derivative.
- Usage of `?.` operator (e.g., `obj?.Prop`) is considered **unsafe** because it bypasses Unity's custom equality check that handles lifetime validity.

**Safe Pattern:**
```csharp
if (unityObject != null)
{
    unityObject.DoSomething(); // OK
}
```

**Unsafe Pattern:**
```csharp
await Task.Yield();

unityObject.DoSomething(); // Error: SIUA001
unityObject?.DoSomething(); // Error: SIUA001 (?. is bypassed)
```

**First Await Exception:**

Unity object access **before and within** the first `await` expression is safe and will not trigger SIUA001.

```csharp
// Safe: access before any await
unityObject.DoSomething();

// Safe: access within the first await expression
await unityObject.SomeAsyncMethod();

// Error: access after the first await completes
unityObject.DoSomethingElse(); // Error: SIUA001
```

This is because Unity objects cannot be destroyed before the async method yields control for the first time.

## `SIUA002`: Await in safe block

**Severity: Warning**

Using `await` inside a "Safe Block" (an `if (obj != null)` block) invalidates the safety guarantee. When the async method resumes after the `await`, the Unity object may have been destroyed in the meantime.

**Rule:**
Shall not use `await` inside a block guarded by a Unity object null check.

**Unsafe Pattern:**
```csharp
if (unityObject != null)
{
    await Task.Delay(100);

    // unityObject might be destroyed here!
    unityObject.DoSomething(); 
}
// Warning: SIUA002 reported on 'await'
```

**Correct Pattern:**
Check for null *after* the `await` if you need to access the object.

```csharp
await Task.Delay(100);

if (unityObject != null)
{
    unityObject.DoSomething();
}
```

---

## Common Analysis Behavior

The following rules and limitations apply to ALL diagnostics in `UnityAsyncMethodAnalyzer`.

### Safe Block Definition (Strictness)

The analyzer is strict about what constitutes a "Safe Block".
- **ALL** conditions in the `if` statement must be Unity object null checks.
- If you combine a Unity check with ANY other condition (e.g., `unityObj != null && someBool`), the block is treated as **UNSAFE**.
- It typically only accepts `if` statements (not ternary `? :`).
- The condition must be a direct inequality check against `null` (e.g., `x != null`) or composed of them using `&&` (Logic AND).
- **Complex conditions** (e.g., `||`, pattern matching `is not null`, or helper methods) may be **misdetected** (treated as unsafe blocks).

### Limitation: No Data Flow Analysis

The analyzer does **not** verify that the checked variable matches the accessed/usage variable. It simply treats any block guarded by a **valid Unity Object null check** as "Safe".

**Implication for SIUA001:**
It skips analysis for the block if *any* Unity Object check is detected, even if you access a different object.

```csharp
// Valid (Analysis skipped)
// The analyzer considers the block "safe" because Unity object 'foo' is checked, 
// even though Unity object 'bar' is being accessed without a check.
if (foo != null) 
{
    bar.DoSomething(); // No Error (False negative)
}
```

**Implication for SIUA002:**
It strictly enforces "No Await" inside the block, even if the check is unrelated to the awaited task.

```csharp
// Warning (Strict enforcement)
if (foo != null) 
{
    await Task.Delay(10); // Warning: SIUA002
}
```

### Limitation: Early Returns

The analyzer does not perform control flow analysis, so "Early Return" style checks are **not** recognized as creating a Safe Block.

**Workaround:**
Invert the condition and use an `else` block (or put the code inside the `if`).

```csharp
// Unsafe (Analysis does NOT support this)
if (unityObject == null) return;

unityObject.DoSomething(); // Error: SIUA001
```

```csharp
// Safe (Using else block)
if (unityObject != null)
{
    unityObject.DoSomething(); // OK
}
else
{
    return;
}
```

This workaround applies to **SIUA002** as well:

```csharp
// Unsafe (SIUA002 triggers because await is INSIDE the if)
if (unityObject != null)
{
    unityObject.DoSomething();
    await DoFurtherAsync(); // Warning: SIUA002
}
```

```csharp
// Safe (await is OUTSIDE the checked block)
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

# Static State Analysis

`UnityStaticStateAnalyzer` ensures that static state doesn't survive across play modes when Domain Reloading is disabled.

## `SIUA011`: Static state survives across play modes

**Severity: Warning**

Static fields and properties survive across play modes in Unity if Domain Reloading is disabled. This can lead to unexpected behavior where state from a previous session persists into the next one.

**Rule:**
Static fields and properties should be reset when the project is loaded or entering play mode. This is typically done using a method marked with the `[RuntimeInitializeOnLoadMethod]` attribute.

**Why it matches:**
- Any static field (except `readonly` fields of immutable types like `string`, primitive types, or `readonly struct`).
- Any static property (except `readonly` properties of immutable types).
- The class does NOT contain a static method marked with `[RuntimeInitializeOnLoadMethod]` or `[RuntimeInitializeOnLoadMethodAttribute]`.

**Safe Pattern:**
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

**Unsafe Pattern:**
```csharp
public class MyService
{
    public static int Counter; // Warning: SIUA011
}
```
