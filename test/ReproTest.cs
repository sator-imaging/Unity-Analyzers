using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis;

namespace UnityAnalyzers.Test
{
    public class ReproTest
    {
        private const string UnityEngineSource = @"
namespace UnityEngine
{
    public class Object {}
    public class RuntimeInitializeOnLoadMethodAttribute : System.Attribute {}
}
";

        [Fact]
        public async Task TestStaticClassWithOnlyMethods()
        {
            var testCode = @"
public static class TestClass
{
    public static void Foo() {}
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            // If it's emitting an error, this will fail because we expect no diagnostics.
            await test.RunAsync();
        }

        [Fact]
        public async Task TestStaticClassWithNestedClass()
        {
            var testCode = @"
public static class TestClass
{
    public static class Nested {}
}
";
            var test = new CSharpAnalyzerTest<UnityStaticStateAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { testCode, UnityEngineSource } },
            };

            await test.RunAsync();
        }
    }
}
