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
            var expected = new DiagnosticResult("SIUA003", DiagnosticSeverity.Warning)
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
            var expected = new DiagnosticResult("SIUA003", DiagnosticSeverity.Warning)
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
        public async Task TestNoWarningOnConst()
        {
            var testCode = @"
public class TestClass
{
    public const int myConst = 0;
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task TestNoWarningWithoutUnityEngine()
        {
            var testCode = @"
public class TestClass
{
    public static int myField;
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode } },
            };

            await test.RunAsync();
        }
    }
}
