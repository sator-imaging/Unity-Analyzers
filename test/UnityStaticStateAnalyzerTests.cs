using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis;

namespace UnityAnalyzers.Test
{
    public class UnityStaticStateAnalyzerTests
    {
        private const string UnityEngineSource = @"
namespace UnityEngine
{
    public class Object {}
    public class RuntimeInitializeOnLoadMethodAttribute : System.Attribute {}
}
";

        [Fact]
        public async Task TestStaticFieldError()
        {
            var testCode = @"
public class TestClass
{
    public static int {|#0:myField|};
}
";
            var expected = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("field", "myField");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestStaticPropertyError()
        {
            var testCode = @"
public class TestClass
{
    public static int {|#0:MyProperty|} { get; set; }
}
";
            var expected = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("property", "MyProperty");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestNoErrorWithResetMethod()
        {
            var testCode = @"
using UnityEngine;
public class TestClass
{
    public static int myField;

    [RuntimeInitializeOnLoadMethod]
    static void Reset() { myField = 0; }
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task TestNoErrorOnConst()
        {
            var testCode = @"
public class TestClass
{
    public const int MyConstInt = 0;
    public const string MyConstString = ""foo"";
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task TestReadonlyFields()
        {
            var testCode = @"
public readonly struct MyReadOnlyStruct { public readonly int X; }
public struct MyMutableStruct { public int X; }
public class MyClass { public int X; }

public class TestClass
{
    public static readonly int ReadonlyInt = 0;
    public static readonly string ReadonlyString = """";
    public static readonly MyReadOnlyStruct ReadonlyReadOnlyStruct = new MyReadOnlyStruct();
    public static readonly MyMutableStruct {|#0:ReadonlyMutableStruct|} = new MyMutableStruct();
    public static readonly MyClass {|#1:ReadonlyClass|} = new MyClass();
}
";
            var expected0 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("field", "ReadonlyMutableStruct");
            var expected1 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(1)
                .WithArguments("field", "ReadonlyClass");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            test.ExpectedDiagnostics.Add(expected1);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestStaticEvents()
        {
            var testCode = @"
using System;
public class TestClass
{
    public static event Action {|#0:OnSomething|};
    public static event Func<int> {|#1:OnFunc|};
}
";
            var expected0 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("event", "OnSomething");
            var expected1 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(1)
                .WithArguments("event", "OnFunc");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            test.ExpectedDiagnostics.Add(expected1);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestReadonlyPropertiesAndEnums()
        {
            var testCode = @"
public enum MyEnum { A, B }
public readonly struct MyReadOnlyStruct { public readonly int X; }

public class TestClass
{
    public static MyEnum {|#2:ReadonlyEnumProperty|} => MyEnum.A;
    public static int {|#3:ReadonlyIntProperty|} => 0;
    public static MyReadOnlyStruct {|#4:ReadonlyStructProperty|} => new MyReadOnlyStruct();

    public static int {|#0:MutableProperty|} { get; set; }
    public static System.Action {|#1:ReadonlyDelegateProperty|} => null;
}
";
            var expected0 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("property", "MutableProperty");
            var expected1 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(1)
                .WithArguments("property", "ReadonlyDelegateProperty");
            var expected1_SIUA013 = new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning)
                .WithLocation(1)
                .WithArguments("ReadonlyDelegateProperty");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            test.ExpectedDiagnostics.Add(expected1);
            test.ExpectedDiagnostics.Add(expected1_SIUA013);
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning).WithLocation(2).WithArguments("ReadonlyEnumProperty"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning).WithLocation(3).WithArguments("ReadonlyIntProperty"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning).WithLocation(4).WithArguments("ReadonlyStructProperty"));
            await test.RunAsync();
        }

        [Fact]
        public async Task TestMissingResetError()
        {
            var testCode = @"
using UnityEngine;
using System;
public class TestClass
{
    public static int myField;
    public static string myString;
    public static event Action myEvent;
    public static event Func<int> myFunc;

    [RuntimeInitializeOnLoadMethod]
    static void {|#0:Reset|}()
    {
        myField = 0;
    }
}
";
            var expected1 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error).WithLocation(0).WithArguments("field", "myString");
            var expected2 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error).WithLocation(0).WithArguments("event", "myEvent");
            var expected3 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error).WithLocation(0).WithArguments("event", "myFunc");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected1);
            test.ExpectedDiagnostics.Add(expected2);
            test.ExpectedDiagnostics.Add(expected3);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestAllResets()
        {
            var testCode = @"
using UnityEngine;
using System;
public class TestClass
{
    public static int myField;
    public static int MyProperty { get; set; }
    public static event Action OnSomething;

    [RuntimeInitializeOnLoadMethod]
    static void Reset()
    {
        myField = 0;
        MyProperty = 0;
        OnSomething = null;
    }
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task TestCompoundAndIncrementResets()
        {
            var testCode = @"
using UnityEngine;
public class TestClass
{
    public static int myField1;
    public static int myField2;

    [RuntimeInitializeOnLoadMethod]
    static void {|#0:Reset|}()
    {
        myField1 += 1;
        myField2++;
    }
}
";
            var expected0 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error).WithLocation(0).WithArguments("field", "myField1");
            var expected1 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error).WithLocation(0).WithArguments("field", "myField2");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            test.ExpectedDiagnostics.Add(expected1);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestErrorOnReadOnlyMutableMembers()
        {
            var testCode = @"
using UnityEngine;
using System.Collections.Generic;
public class TestClass
{
    public static readonly List<int> myField = new List<int>();
    public static List<int> MyProperty { get; } = new List<int>();

    [RuntimeInitializeOnLoadMethod]
    static void {|#0:Reset|}()
    {
    }
}
";
            var expected0 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("field", "myField");
            var expected1 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("property", "MyProperty");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            test.ExpectedDiagnostics.Add(expected1);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestDateTimeAndTimeSpan()
        {
            var testCode = @"
using UnityEngine;
using System;
public class TestClass1
{
    public static readonly DateTime ReadonlyDateTime = DateTime.Now;
    public static readonly TimeSpan ReadonlyTimeSpan = TimeSpan.Zero;

    public static DateTime {|#0:MutableDateTime|};
    public static TimeSpan {|#1:MutableTimeSpan|};
}

public class TestClass2
{
    public static DateTime MutableDateTime;
    public static TimeSpan MutableTimeSpan;

    [RuntimeInitializeOnLoadMethod]
    static void {|#2:Reset|}()
    {
        // MutableDateTime is not reset
        MutableTimeSpan = TimeSpan.Zero;
    }
}
";
            var expected0 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("field", "MutableDateTime");
            var expected1 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(1)
                .WithArguments("field", "MutableTimeSpan");
            var expected2 = new DiagnosticResult("SIUA012", DiagnosticSeverity.Error)
                .WithLocation(2)
                .WithArguments("field", "MutableDateTime");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            test.ExpectedDiagnostics.Add(expected1);
            test.ExpectedDiagnostics.Add(expected2);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestPropertyWithBodyReturnsImmutableType()
        {
            var testCode = @"
public class TestClass
{
    public static int {|#0:PropertyWithExpressionBody|} => 0;
    public static int {|#1:PropertyWithBlockBody|} { get { return 0; } }
    public static int PropertyAuto { get; } = 0;
    private const int _c = 0;
    public static int {|#2:PropertyWithBypassAttempt|} { get => _c; }
}
";
            var expected0 = new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("PropertyWithExpressionBody");
            var expected1 = new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning)
                .WithLocation(1)
                .WithArguments("PropertyWithBlockBody");
            var expected2 = new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning)
                .WithLocation(2)
                .WithArguments("PropertyWithBypassAttempt");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            test.ExpectedDiagnostics.Add(expected1);
            test.ExpectedDiagnostics.Add(expected2);
            // PropertyWithBypassAttempt returns int (immutable), so it should NOT trigger SIUA011

            await test.RunAsync();
        }

        [Fact]
        public async Task TestPropertyWithBodyAndResetMethod()
        {
            var testCode = @"
using UnityEngine;
public class TestClass
{
    public static int {|#0:PropertyWithBody|} => 0;

    [RuntimeInitializeOnLoadMethod]
    static void Reset() { }
}
";
            var expected0 = new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("PropertyWithBody");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected0);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestStaticEventWithBody()
        {
            var testCode = @"
using System;
using UnityEngine;
public class TestClass
{
    private static Action {|#0:_onSomething|};
    public static event Action OnSomething { add { _onSomething += value; } remove { _onSomething -= value; } }
}
";
            var expected = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("field", "_onSomething");
            // OnSomething should NOT trigger SIUA011 because it has bodies.

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestStaticEventWithBodyAndResetMethod()
        {
            var testCode = @"
using System;
using UnityEngine;
public class TestClass
{
    private static Action _onSomething;
    public static event Action OnSomething { add { _onSomething += value; } remove { _onSomething -= value; } }

    [RuntimeInitializeOnLoadMethod]
    static void Reset()
    {
        _onSomething = null;
    }
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            // Should have no diagnostics. OnSomething is ignored, _onSomething is reset.
            await test.RunAsync();
        }

        [Fact]
        public async Task TestStaticEventAutoImplementedStillTriggers()
        {
            var testCode = @"
using System;
public class TestClass
{
    public static event Action {|#0:OnSomething|};
}
";
            var expected = new DiagnosticResult("SIUA011", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("event", "OnSomething");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected);
            await test.RunAsync();
        }
    }
}
