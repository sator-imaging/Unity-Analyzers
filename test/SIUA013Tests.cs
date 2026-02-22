using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis;

namespace UnityAnalyzers.Test
{
    public class SIUA013Tests
    {
        private const string UnityEngineSource = @"
namespace UnityEngine
{
    public class Object {}
    public class RuntimeInitializeOnLoadMethodAttribute : System.Attribute {}
}
";

        [Fact]
        public async Task TestPropertyWithBodyReturnsImmutableType()
        {
            var testCode = @"
public class TestClass
{
    public static int {|#0:PropertyWithExpressionBody|} => 0;
    public static int {|#1:PropertyWithBlockBody|} { get { return 0; } }
    public static int PropertyAuto { get; } = 0;
}
";
            // Currently these pass. We want them to fail with SIUA013.
            // Except for PropertyAuto.

            var expected0 = new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("PropertyWithExpressionBody");
            var expected1 = new DiagnosticResult("SIUA013", DiagnosticSeverity.Warning)
                .WithLocation(1)
                .WithArguments("PropertyWithBlockBody");

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
