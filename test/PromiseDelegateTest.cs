using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

namespace UnityAnalyzers.Test
{
    public class PromiseDelegateTest
    {
        private static CSharpAnalyzerTest<UnityAnalyzers.AsyncInvocationAnalyzer, DefaultVerifier> CreateTest(string source)
        {
            var test = new CSharpAnalyzerTest<UnityAnalyzers.AsyncInvocationAnalyzer, DefaultVerifier>
            {
                TestState = { Sources = { source } },
            };

            test.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId)!;
                var options = project.CompilationOptions!;
                options = options.WithSpecificDiagnosticOptions(
                    options.SpecificDiagnosticOptions.SetItem("SIUA021", ReportDiagnostic.Error));
                return solution.WithProjectCompilationOptions(projectId, options);
            });

            return test;
        }

        [Fact]
        public async Task ReportsAsyncInvocationInPromiseDelegate()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public delegate void Promise(Func<Task> func);

public class C
{
    private static Task AsyncMethod() => {|#1:Task.CompletedTask|};

    public void M(Promise promise)
    {
        promise({|#0:() => AsyncMethod()|});
    }
}
";

            var test = CreateTest(source);

            // Currently, this should FAIL to find a diagnostic at #0 because it's suppressed.
            // After the fix, it should find a diagnostic at #0.
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(0));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(1));

            await test.RunAsync();
        }
    }
}
