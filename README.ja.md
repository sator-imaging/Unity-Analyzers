[![nuget](https://img.shields.io/nuget/vpre/SatorImaging.UnityAnalyzers)](https://www.nuget.org/packages/SatorImaging.UnityAnalyzers)
&nbsp;
[![🇯🇵](https://img.shields.io/badge/🇯🇵-日本語-789)](./README.ja.md)
[![🇨🇳](https://img.shields.io/badge/🇨🇳-简体中文-789)](./README.zh-CN.md)
[![🇺🇸](https://img.shields.io/badge/🇺🇸-English-789)](./README.md)





Unity 開発時のコードを安全かつ正しく保つための Roslyn アナライザーです。

- [非同期メソッドの解析](#非同期メソッドの解析)
  - [SIUA001](#siua001-信頼できない-unity-オブジェクトアクセス): 信頼できない Unity オブジェクトアクセス
  - [SIUA002](#siua002-安全ブロック内での-await): 安全ブロック内での await
- [非同期呼び出しの解析](#非同期呼び出しの解析)
  - [SIUA021](#siua021-非同期呼び出しを検出): 非同期呼び出しを検出
- [静的状態の解析](#静的状態の解析)
  - [SIUA011](#siua011-静的状態がプレイモードを跨いで残っている): 静的状態がプレイモードを跨いで残っている
  - [SIUA012](#siua012-runtimeinitializeonloadmethod-での状態リセット漏れ): RuntimeInitializeOnLoadMethod での状態リセット漏れ
  - [SIUA013](#siua013-ボディを持つ静的プロパティが不正な状態を返す可能性がある): ボディを持つ静的プロパティが不正な状態を返す可能性がある
  - [SIUA014](#siua014-ボディを持つ静的イベント): ボディを持つ静的イベント





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

# 非同期呼び出しの解析

`AsyncInvocationAnalyzer` は、明示的に追跡されていない非同期実行の起点を検出します。

## `SIUA021`: 非同期呼び出しを検出

**重大度: Error**

このルールは、次のコードに対してエラーを報告します:
- `Task`、`Task<T>`、`ValueTask`、`ValueTask<T>` を返すメソッド呼び出し（直接 `await` している場合を除く）(e.g. `AsyncMethod();`)。
- `async` である、または task-like を返す匿名関数の作成・代入 (e.g. `Action a = async () => await Task.Delay(1);`, `Func<Task> f = () => Task.CompletedTask;`)。
- task-like を返すメソッドグループのデリゲート/イベントハンドラー代入 (e.g. `eventHandler += TaskReturningMethod;`, `handler = TaskReturningMethod;`)。
- task-like 戻り値メソッド、または監視対象 API 型へのメソッドグループ参照 (e.g. `var m = TaskReturningMethod;`)。
- 監視対象 API 型のフィールド/プロパティ/イベント参照 (e.g. `var p = synchronizationContext.Post;`, `var e = timer.Elapsed;`)。
- 監視対象 API 型のオブジェクト生成 (e.g. `new System.Threading.Timer(_ => { });`)。
- 監視対象 API 型へのメソッド呼び出し (e.g. `Task.Run(() => { });`, `ThreadPool.QueueUserWorkItem(_ => { });`, `Parallel.For(0, 10, _ => { });`)。
- 監視対象のスレッド/タスク API:
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

## Promise 型名のカスタマイズ

`.editorconfig` で Promise 例外型の名前をカスタマイズできます:

```ini
[*.cs]
unity_analyzers_promise_type_name = MyCustomPromise # Default: Promise
```

キー名は `unity_analyzers_promise_type_name`（`analyzers` は複数形）を正確に使用してください。

この値はアナライザー起動時に一度だけ読み込まれ、その後キャッシュされます（変更を反映するには再起動/再読み込みが必要です）。

## 制限: `async void`

`async void` は呼び出し元が保持できる await 可能ハンドル（`Task`/`ValueTask`）を持たないため、呼び出し地点から完了や例外を信頼して追跡できません。  
そのため、呼び出し追跡パターンでは `async void` フローを完全には追跡できません。

# 静的状態の解析

`UnityStaticStateAnalyzer` は、ドメインリロードが無効になっている場合に静的状態がプレイモードを跨いで残らないことを確認します。

![](./docs/UnityStaticStateAnalyzer.gif)

## `SIUA011`: 静的状態がプレイモードを跨いで残っている

**重大度: Error**

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
    public static int Counter; // Error: SIUA011
}
```

## `SIUA012`: RuntimeInitializeOnLoadMethod での状態リセット漏れ

**重大度: Error**

リセットが必要な静的フィールド、プロパティ、イベント（SIUA011 参照）は、`[RuntimeInitializeOnLoadMethod]` 属性が付いたメソッド内で明示的に値を代入する必要があります。

**ルール:**
クラス内のすべての可変の静的状態は、初期化メソッド内で単純な代入（`=`）によってリセットされなければなりません。

**危険なパターン:**
```csharp
public class MyService
{
    public static int Counter;
    public static string Status;

    [RuntimeInitializeOnLoadMethod]
    static void Init()
    {
        Counter = 0;
        // Status がリセットされていない！ -> Error: SIUA012 が 'Init' に報告される
    }
}
```

## `SIUA013`: ボディを持つ静的プロパティが不正な状態を返す可能性がある

**重大度: Warning**

ゲッターボディを持つ（自動実装されていない）静的プロパティは、ドメインリロードが無効になっている場合に、不正な、あるいは古い静的状態を返す可能性があります。

**ルール:**
自動実装プロパティの使用を検討するか（例: `static int Property { get; } = 0;`）、返される値が正しく管理されていることを確認してください。

**マッチ理由:**
- 自動実装されていない（ボディまたは式形式のボディを持つ）任意の静的読み取り専用プロパティ。

**危険なパターン:**
```csharp
public class MyService
{
    // Warning: SIUA013
    public static int Counter => 123;
}
```

**安全なパターン:**
```csharp
public class MyService
{
    public static int Counter { get; } = 123;
}
```

## `SIUA014`: ボディを持つ静的イベント

**重大度: Error**

カスタムの `add` または `remove` ボディを持つ静的イベントは許可されません。自動実装された静的イベントのみが、アナライザーによって確実な追跡とリセットが可能です。

**ルール:**
静的イベントは自動実装されている必要があります。（例: `public static event Action OnSomething;`）

**危険なパターン:**
```csharp
public class MyService
{
    // Error: SIUA014
    public static event Action OnSomething { add { ... } remove { ... } }
}
```

**安全なパターン:**
```csharp
public class MyService
{
    public static event Action OnSomething;
}
```

> [!TIP]
> `Directory.Build.props` などの共有設定ファイルでアナライザーを参照する場合、C# プロジェクトが Unity プロジェクトである場合のみ有効にするには、以下の条件（Condition）を使用してください。
> `Condition=" $([System.String]::Copy('$(DefineConstants)').IndexOf('UNITY_5_6_OR_NEWER')) != -1 "`
>
> **`.props` ファイルの全体サンプル:**
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
