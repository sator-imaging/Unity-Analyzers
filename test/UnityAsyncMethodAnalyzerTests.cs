using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis;

namespace UnityAnalyzers.Test
{
    public class UnityAsyncMethodAnalyzerTests
    {
        private const string UnityEngine = @"
namespace UnityEngine
{
    public class Object { }
    public class Component : Object { }
    public class MonoBehaviour : Component
    {
        public bool IsEnabled { get; set; }
        public string Name = string.Empty;
        public void Foo() { }
        public System.Threading.Tasks.Task<bool> FooAsync() => System.Threading.Tasks.Task.FromResult(false);
        public MonoBehaviour GetSelf() => this;
        public System.Threading.Tasks.Task<MonoBehaviour> GetSelfAsync() => System.Threading.Tasks.Task.FromResult(this);
        public event System.Action OnChanged;
        public Object this[int index] { get => this; set { } }
        public Object this[string name] { get => this; set { } }
        public static MonoBehaviour Create() => new();
        public static System.Threading.Tasks.Task<MonoBehaviour> CreateAsync() => System.Threading.Tasks.Task.FromResult(new MonoBehaviour());
    }
}
";

        [Fact]
        public async Task TestUnreliableMemberAccessAfterAwait()
        {
            var testCode = @"
using System.Threading.Tasks;
using UnityEngine;

public class TestClass
{
    public async Task TestMethod()
    {
        var behaviour = new MonoBehaviour();
        await Task.Delay(1);
        {|#0:behaviour.Foo()|};
    }
}
";

            var expected = new DiagnosticResult("SIUA001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("MonoBehaviour");

            var test = new CSharpAnalyzerTest<UnityAnalyzers.UnityAsyncMethodAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { testCode, UnityEngine },
                },
            };

            test.ExpectedDiagnostics.Add(expected);
            await test.RunAsync();
        }

        [Fact]
        public async Task TestNoUnreliableMemberAccessAfterAwaitWithNullCheck()
        {
            var testCode = @"
using System.Threading.Tasks;
using UnityEngine;

public class TestClass
{
    public async Task TestMethod()
    {
        var behaviour = new MonoBehaviour();
        await Task.Delay(1);
        if (behaviour != null)
        {
            behaviour.Foo();
        }
    }
}
";

            var test = new CSharpAnalyzerTest<UnityAnalyzers.UnityAsyncMethodAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { testCode, UnityEngine },
                },
            };

            await test.RunAsync();
        }
    }
}
