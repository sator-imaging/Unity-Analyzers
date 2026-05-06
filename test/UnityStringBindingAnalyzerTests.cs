using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

namespace UnityAnalyzers.Test
{
    public class UnityStringBindingAnalyzerTests
    {
        private const string UnityEngine = @"
namespace UnityEngine
{
    public class Object { }
    public class Component : Object { }
    public class MonoBehaviour : Component
    {
        public void StartCoroutine(string methodName) { }
        public void StartCoroutine(System.Collections.IEnumerator routine) { }
        public void StopCoroutine(string methodName) { }
        public void StopCoroutine(System.Collections.IEnumerator routine) { }
        public void Invoke(string methodName, float time) { }
        public void InvokeRepeating(string methodName, float time, float repeatRate) { }
        public void CancelInvoke(string methodName) { }
        public void CancelInvoke() { }
        public bool IsInvoking(string methodName) { return false; }
        public void SendMessage(string methodName) { }
        public void SendMessageUpwards(string methodName) { }
        public void BroadcastMessage(string methodName) { }
    }
}
";

        [Fact]
        public async Task TestStringBasedBindingApis()
        {
            var testCode = @"
using UnityEngine;

public class TestBehaviour : MonoBehaviour
{
    void Start()
    {
        {|#0:StartCoroutine(""MyRoutine"")|};
        {|#1:StopCoroutine(""MyRoutine"")|};
        {|#2:Invoke(""MyMethod"", 1f)|};
        {|#3:InvokeRepeating(""MyMethod"", 1f, 1f)|};
        {|#4:CancelInvoke(""MyMethod"")|};
        {|#5:IsInvoking(""MyMethod"")|};
        {|#6:SendMessage(""MyMethod"")|};
        {|#7:SendMessageUpwards(""MyMethod"")|};
        {|#8:BroadcastMessage(""MyMethod"")|};
    }

    System.Collections.IEnumerator MyRoutine() { yield return null; }
    void MyMethod() { }
}
";

            var test = new CSharpAnalyzerTest<UnityAnalyzers.UnityStringBindingAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { testCode, UnityEngine },
                },
            };

            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(0).WithArguments("StartCoroutine"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(1).WithArguments("StopCoroutine"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(2).WithArguments("Invoke"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(3).WithArguments("InvokeRepeating"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(4).WithArguments("CancelInvoke"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(5).WithArguments("IsInvoking"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(6).WithArguments("SendMessage"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(7).WithArguments("SendMessageUpwards"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA031", DiagnosticSeverity.Error).WithLocation(8).WithArguments("BroadcastMessage"));

            await test.RunAsync();
        }

        [Fact]
        public async Task TestNonStringBindingApis()
        {
            var testCode = @"
using UnityEngine;

public class TestBehaviour : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(MyRoutine());
        StopCoroutine(MyRoutine());
        CancelInvoke();
    }

    System.Collections.IEnumerator MyRoutine() { yield return null; }
}
";

            var test = new CSharpAnalyzerTest<UnityAnalyzers.UnityStringBindingAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { testCode, UnityEngine },
                },
            };

            await test.RunAsync();
        }

        [Fact]
        public async Task TestStringBindingOnNonMonoBehaviour()
        {
            var testCode = @"
public class NonUnity
{
    public void StartCoroutine(string s) { }
}

public class Test
{
    void Start()
    {
        var n = new NonUnity();
        n.StartCoroutine(""test"");
    }
}
";

            var test = new CSharpAnalyzerTest<UnityAnalyzers.UnityStringBindingAnalyzer, DefaultVerifier>
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
