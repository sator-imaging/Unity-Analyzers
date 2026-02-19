[![nuget](https://img.shields.io/nuget/vpre/SatorImaging.UnityAnalyzers)](https://www.nuget.org/packages/SatorImaging.UnityAnalyzers)
&nbsp;
[![DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/sator-imaging/Unity-Analyzers)

[🇺🇸 English](./README.md)
&nbsp; ❘ &nbsp;
[🇯🇵 日本語版](./README.ja.md)
&nbsp; ❘ &nbsp;
[🇨🇳 简体中文版](./README.zh-CN.md)





Unity 開発時のコードを安全かつ正しく保つための Roslyn アナライザーです。


# 非同期メソッドの解析

`UnityAsyncMethodAnalyzer` は `async` メソッド内での `UnityEngine.Object`（MonoBehaviour、ScriptableObject など）の安全な利用を検証します。

`MonoBehaviour` や Unity 固有の型だけでなく、通常の C# クラス・構造体・レコードに宣言されたすべての `async` メソッドが対象です。

## `SIUA001`: 信頼できない Unity オブジェクトアクセス

**重大度: Error**

`async` メソッド内で Unity オブジェクトのインスタンスメンバー（メソッド・プロパティ・フィールド・イベント）にアクセスするのは危険です。ネイティブオブジェクトが破棄された後もマネージド側は生きている場合があるためです。

**ルール:**
`if (obj != null)` のような厳密な null チェックでガードしてください。

**マッチ理由:**
- `UnityEngine.Object` 派生型の任意のインスタンスメンバーアクセス。
- `?.` 演算子（例: `obj?.Prop`）は Unity の生存チェックを回避するため **安全ではありません**。

**安全なパターン:**
```csharp
if (unityObject != null)
{
    unityObject.DoSomething(); // OK
}
```

**危険なパターン:**
```csharp
await Task.Yield();

unityObject.DoSomething(); // Error: SIUA001
unityObject?.DoSomething(); // Error: SIUA001 (?. はバイパス扱い)
```

**最初の await の例外:**

最初の `await` 前、および「最初の await 式の内部」での Unity オブジェクトアクセスは安全で、SIUA001 は発生しません。

```csharp
// 安全: await 前
unityObject.DoSomething();

// 安全: 最初の await 式の内部
await unityObject.SomeAsyncMethod();

// エラー: 最初の await 完了後
unityObject.DoSomethingElse(); // Error: SIUA001
```

これは、`async` メソッドが初めて制御を手放すまでは Unity オブジェクトが破棄されないためです。

## `SIUA002`: 安全ブロック内での await

**重大度: Warning**

"安全ブロック"（`if (obj != null)` ブロック）内で `await` を使うと安全性が失われます。`await` 後に再開した時点で Unity オブジェクトが破棄されている可能性があるためです。

**ルール:**
Unity オブジェクトの null チェックでガードされたブロック内では `await` を使用しないでください。

**危険なパターン:**
```csharp
if (unityObject != null)
{
    await Task.Delay(100);

    // 再開時には破棄されているかもしれない
    unityObject.DoSomething(); 
}
// Warning: SIUA002 は 'await' に報告される
```

**正しいパターン:**
オブジェクトにアクセスしたい場合、`await` の後で null チェックを行ってください。

```csharp
await Task.Delay(100);

if (unityObject != null)
{
    unityObject.DoSomething();
}
```

---

## 共通の解析挙動

以下のルールと制限は `UnityAsyncMethodAnalyzer` の全診断に適用されます。

### 安全ブロックの定義（厳密さ）

アナライザーは「安全ブロック」を厳密に判定します。
- `if` 条件内の **すべて** が Unity オブジェクトの null チェックである必要があります。
- Unity チェックに **他の条件を1つでも** 組み合わせると（`unityObj != null && someBool` など）、そのブロックは **安全ではない** とみなされます。
- 基本的に `if` 文のみを許容し、三項演算子（`? :`）は通常サポートしません。
- 条件は `x != null` のような直接の不等式、もしくはそれらを `&&`（論理積）で構成したものに限ります。
- **複雑な条件**（`||`、`is not null` といったパターンマッチ、ヘルパーメソッドなど）は **誤検出**（安全でない扱い）されることがあります。

### 制限: データフロー解析なし

アナライザーはチェック対象の変数と使用している変数が一致するかを検証しません。**有効な Unity オブジェクト null チェックが見つかったブロック** を丸ごと「安全」と見なします。

**SIUA001 への影響:**
ブロック内の解析をスキップするため、チェックとは別のオブジェクトを使っていても報告しません。

```csharp
// 有効（解析スキップ）
// Unity オブジェクト 'foo' をチェックしているためブロックは「安全」と判定される。
// 実際には 'bar' をチェックなしでアクセスしていてもエラーにならない（偽陰性）。
if (foo != null) 
{
    bar.DoSomething(); // No Error (False negative)
}
```

**SIUA002 への影響:**
チェック対象と await 対象が無関係でも、ブロック内の `await` を厳格に禁止します。

```csharp
// Warning（厳格適用）
if (foo != null) 
{
    await Task.Delay(10); // Warning: SIUA002
}
```

### 制限: 早期リターン

アナライザーは制御フロー解析を行わないため、「早期リターン」スタイルのチェックは安全ブロックとして扱われません。

**回避策:**
条件を反転して `else` ブロックを用いる（または中にコードを入れる）。

```csharp
// 危険（この形はサポートされない）
if (unityObject == null) return;

unityObject.DoSomething(); // Error: SIUA001
```

```csharp
// 安全（else ブロックを使用）
if (unityObject != null)
{
    unityObject.DoSomething(); // OK
}
else
{
    return;
}
```

この回避策は **SIUA002** にも当てはまります。

```csharp
// 危険（await が if 内にあるため SIUA002 が発生）
if (unityObject != null)
{
    unityObject.DoSomething();
    await DoFurtherAsync(); // Warning: SIUA002
}
```

```csharp
// 安全（await をチェックブロックの外に出す）
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

# 静的状態の解析

`UnityStaticStateAnalyzer` は、ドメインリロードが無効になっている場合に静的状態がプレイモードを跨いで残らないことを確認します。

## `SIUA011`: 静的状態がプレイモードを跨いで残っている

**重大度: Warning**

Unity ではドメインリロードが無効になっている場合、静的フィールドやプロパティはプレイモードを跨いで保持されます。これにより、前のセッションの状態が次のセッションに引き継がれ、予期しない動作を引き起こす可能性があります。

**ルール:**
静的フィールドやプロパティは、プロジェクトのロード時やプレイモードへの進入時にリセットされるべきです。これは通常、`[RuntimeInitializeOnLoadMethod]` 属性が付いたメソッドを使用して行われます。

**マッチ理由:**
- 静的フィールド（`const` および `string`、プリミティブ型、`readonly struct` などの不変型の `readonly` フィールドを除く）。
- 静的プロパティ（不変型のゲッターのみのプロパティを除く）。
- クラス内に `[RuntimeInitializeOnLoadMethod]` または `[RuntimeInitializeOnLoadMethodAttribute]` が付いた静的メソッドが存在しない。

**安全なパターン:**
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

**危険なパターン:**
```csharp
public class MyService
{
    public static int Counter; // Warning: SIUA011
}
```
