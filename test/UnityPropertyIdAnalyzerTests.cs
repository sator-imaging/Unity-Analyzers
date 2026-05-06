using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

namespace UnityAnalyzers.Test
{
    public class UnityPropertyIdAnalyzerTests
    {
        private const string UnityEngine = @"
namespace UnityEngine
{
    public class Object { }
    public class Component : Object { }
    public class Animator : Component
    {
        public void SetTrigger(string name) { }
        public void SetTrigger(int id) { }
        public void SetInteger(string name, int value) { }
        public void SetInteger(int id, int value) { }
        public void SetFloat(string name, float value) { }
        public void SetFloat(int id, float value) { }
        public void SetBool(string name, bool value) { }
        public void SetBool(int id, bool value) { }
    }
    public class Material : Object
    {
        public void SetColor(string name, Color value) { }
        public void SetColor(int id, Color value) { }
        public void SetFloat(string name, float value) { }
        public void SetFloat(int id, float value) { }
        public void SetVector(string name, Vector4 value) { }
        public void SetVector(int id, Vector4 value) { }
        public void SetTexture(string name, Texture value) { }
        public void SetTexture(int id, Texture value) { }
    }
    public struct Vector4 { }
    public class Texture : Object { }
    public struct Color { }
}
";

        [Fact]
        public async Task TestStringBasedPropertyIds()
        {
            var testCode = @"
using UnityEngine;

public class TestBehaviour : MonoBehaviour
{
    public Animator animator;
    public Material material;

    void Start()
    {
        {|#0:animator.SetTrigger(""Jump"")|};
        {|#1:animator.SetBool(""IsGrounded"", true)|};
        {|#2:material.SetColor(""_Color"", new Color())|};
        {|#3:material.SetFloat(""_Glossiness"", 0.5f)|};
        {|#4:animator.SetInteger(""Status"", 1)|};
        {|#5:animator.SetFloat(""Speed"", 1.0f)|};
        {|#6:material.SetVector(""_MainTex_ST"", new Vector4())|};
        {|#7:material.SetTexture(""_MainTex"", new Texture())|};
    }
}

public class MonoBehaviour : UnityEngine.Component { }
";

            var test = new CSharpAnalyzerTest<UnityAnalyzers.UnityPropertyIdAnalyzer, DefaultVerifier>
            {
                TestState =
                {
                    Sources = { testCode, UnityEngine },
                },
            };

            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(0).WithArguments("SetTrigger"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(1).WithArguments("SetBool"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(2).WithArguments("SetColor"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(3).WithArguments("SetFloat"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(4).WithArguments("SetInteger"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(5).WithArguments("SetFloat"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(6).WithArguments("SetVector"));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA032", DiagnosticSeverity.Error).WithLocation(7).WithArguments("SetTexture"));

            await test.RunAsync();
        }

        [Fact]
        public async Task TestIntBasedPropertyIds()
        {
            var testCode = @"
using UnityEngine;

public class TestBehaviour : MonoBehaviour
{
    public Animator animator;
    public Material material;

    void Start()
    {
        animator.SetTrigger(1);
        animator.SetBool(2, true);
        material.SetColor(3, new Color());
        material.SetFloat(4, 0.5f);
    }
}

public class MonoBehaviour : UnityEngine.Component { }
";

            var test = new CSharpAnalyzerTest<UnityAnalyzers.UnityPropertyIdAnalyzer, DefaultVerifier>
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
