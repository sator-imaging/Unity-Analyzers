using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;

namespace UnityAnalyzers.Test
{
    public class AsyncInvocationAnalyzerTests
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
        public async Task ReportsAwaitAndTaskInvocation()
        {
            var source = @"
using System.Threading.Tasks;

public class C
{
    private static Task AsyncMethod() => {|#2:Task.CompletedTask|};

    public static async Task M()
    {
        await {|#0:AsyncMethod()|};
        {|#1:AsyncMethod()|};
    }
}
";

            var test = CreateTest(source);

            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(2));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(1));

            await test.RunAsync();
        }

        [Fact]
        public async Task ReportsAsyncAndTaskReturningAnonymousFunctions()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public class C
{
    public void M()
    {
        Action a = {|#0:async () => { await Task.Delay(1); }|};
        Func<Task> b = {|#1:() => {|#2:Task.CompletedTask|}|};
    }
}
";

            var test = CreateTest(source);

            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(0));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(1));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(2));

            await test.RunAsync();
        }

        [Fact]
        public async Task ReportsAsyncLambdaReturningValue()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public class C
{
    public void M()
    {
        Func<Task<int>> f = {|#0:async () => 0|};
    }
}
";

            var test = CreateTest(source);
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(0));

            await test.RunAsync();
        }

        [Fact]
        public async Task ReportsDelegateAndEventAssignment()
        {
            var source = @"
using System;
using System.Threading.Tasks;

    public class C
    {
        private delegate Task D();
        private event D E;
        private D F;

    private static Task AsyncMethod() => {|#2:Task.CompletedTask|};

        public void M()
        {
        E += {|#0:AsyncMethod|};
        F = {|#1:AsyncMethod|};
        }
    }
";

            var test = CreateTest(source);

            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(2));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(0));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(1));

            await test.RunAsync();
        }

        [Fact]
        public async Task ReportsWatchedTypeMemberAccess()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;

public class C
{
    public void M()
    {
        {|#0:Task.Run(() => { })|};
        {|#1:ThreadPool.QueueUserWorkItem(_ => { })|};
        {|#2:Parallel.For(0, 1, _ => { })|};
        {|#3:Thread.Sleep(1)|};
    }
}
";

            var test = CreateTest(source);

            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(0));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(1));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(2));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(3));

            await test.RunAsync();
        }

        [Fact]
        public async Task AllowsPromiseException()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public sealed class Promise
{
    public Action Exec(Action a) => a;
    public Func<Task<int>> Exec(Func<Task<int>> f) => f;
}

public sealed class CustomPromise
{
    public Action Exec(Action a) => a;
}

public class C
{
    private event Action E;
    private event Func<Task<int>> E2;
    private static Task AsyncMethod() => {|#0:Task.CompletedTask|};

    public void M(Promise promise, CustomPromise custom)
    {
        E += promise.Exec(async () => await Task.Delay(1));
        E += promise.Exec(() => AsyncMethod());
        E += {|#1:custom.Exec(() => AsyncMethod())|};
        E2 += promise.Exec(async () => 0);
    }
}
";

            var test = CreateTest(source);

            // Promise Exec calls are exception targets; CustomPromise is not (default is Promise).
            // Helper Task property reference is also detected.
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(0));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(1));

            await test.RunAsync();
        }

        // TODO: Re-enable when the test host/workspace supports AnalyzerConfigFiles.
        [Fact(Skip = "AnalyzerConfigFiles is not supported by the current test host/workspace.")]
        public async Task AllowsConfiguredPromiseException()
        {
            var source = @"
using System;
using System.Threading.Tasks;

public sealed class Promise
{
    public Action Exec(Action a) => a;
}

public sealed class CustomPromise
{
    public Action Exec(Action a) => a;
}

public class C
{
    private event Action E;
    private static Task AsyncMethod() => {|#1:Task.CompletedTask|};

    public void M(Promise promise, CustomPromise custom)
    {
        E += {|#0:promise.Exec(() => AsyncMethod())|};
        E += custom.Exec(() => AsyncMethod());
    }
}
";

            var test = CreateTest(source);
            test.TestState.AnalyzerConfigFiles.Add(("test.globalconfig", """
is_global = true
unity_analyzers_promise_type_name = CustomPromise
"""));

            // Configured CustomPromise is exception; Promise is not. Helper Task property reference is also detected.
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(0));
            test.ExpectedDiagnostics.Add(new DiagnosticResult("SIUA021", DiagnosticSeverity.Error).WithLocation(1));

            await test.RunAsync();
        }
    }
}
