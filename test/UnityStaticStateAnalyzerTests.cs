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
        public async Task TestStaticFieldWarning()
        {
            var testCode = @"
public class TestClass
{
    public static int {|#0:myField|};
}
";
            var expected = new DiagnosticResult("SIUA011", DiagnosticSeverity.Warning)
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
        public async Task TestStaticPropertyWarning()
        {
            var testCode = @"
public class TestClass
{
    public static int {|#0:MyProperty|} { get; set; }
}
";
            var expected = new DiagnosticResult("SIUA011", DiagnosticSeverity.Warning)
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
        public async Task TestNoWarningWithResetMethod()
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
        public async Task TestWarningOnConst()
        {
            var testCode = @"
public class TestClass
{
    public const int {|#0:myConst|} = 0;
}
";
            var expected = new DiagnosticResult("SIUA011", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("field", "myConst");

            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            test.ExpectedDiagnostics.Add(expected);
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
            var expected0 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("field", "ReadonlyMutableStruct");
            var expected1 = new DiagnosticResult("SIUA011", DiagnosticSeverity.Warning)
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
    }
}
