// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/Unity-Analyzers

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace UnityAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class AsyncInvocationAnalyzer : DiagnosticAnalyzer
    {
        private const string PromiseTypeNameOptionKey = "unity_analyzers_promise_type_name";
        private const string DefaultPromiseTypeName = "Promise";
        private static string? s_cachedPromiseTypeName;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SR.AsyncInvocationDetected);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
            context.RegisterOperationAction(AnalyzeAnonymousFunction, OperationKind.AnonymousFunction);
            context.RegisterOperationAction(AnalyzeDelegateCreation, OperationKind.DelegateCreation);
            context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
            context.RegisterOperationAction(AnalyzeFieldReference, OperationKind.FieldReference);
            context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
            context.RegisterOperationAction(AnalyzeEventReference, OperationKind.EventReference);
            context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var operation = (IInvocationOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (TryGetEnclosingAnonymousFunction(operation, out var anonymousFunction) &&
                (anonymousFunction.Symbol?.IsAsync == true || IsTaskLike(anonymousFunction.Symbol?.ReturnType)))
            {
                return;
            }

            var returnType = operation.TargetMethod?.ReturnType;
            if (IsTaskLike(returnType))
            {
                if (operation.Parent is IAwaitOperation)
                {
                    return;
                }

                ReportAsyncInvocation(context, operation, "invocation", operation.Syntax, promiseTypeName);
                return;
            }

            if (IsWatchedType(operation.TargetMethod?.ContainingType))
            {
                ReportAsyncInvocation(context, operation, "invocation", operation.Syntax, promiseTypeName);
            }
        }

        private static void AnalyzeAnonymousFunction(OperationAnalysisContext context)
        {
            var operation = (IAnonymousFunctionOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (operation.Symbol?.IsAsync == true || IsTaskLike(operation.Symbol?.ReturnType))
            {
                ReportAsyncInvocation(context, operation, "anonymous function", operation.Syntax, promiseTypeName);
            }
        }

        private static void AnalyzeDelegateCreation(OperationAnalysisContext context)
        {
            var operation = (IDelegateCreationOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (operation.Target is IMethodReferenceOperation methodRef &&
                IsTaskLike(methodRef.Method.ReturnType))
            {
                ReportAsyncInvocation(context, operation, "delegate assignment", operation.Syntax, promiseTypeName);
            }
        }

        private static void AnalyzeMethodReference(OperationAnalysisContext context)
        {
            var operation = (IMethodReferenceOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (operation.Parent is IInvocationOperation or IDelegateCreationOperation)
            {
                // Reported by invocation/delegate analysis.
                return;
            }

            if (IsTaskLike(operation.Method.ReturnType) || IsWatchedType(operation.Method.ContainingType))
            {
                ReportAsyncInvocation(context, operation, "method reference", operation.Syntax, promiseTypeName);
            }
        }

        private static void AnalyzeFieldReference(OperationAnalysisContext context)
        {
            var operation = (IFieldReferenceOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (IsWatchedType(operation.Field.ContainingType))
            {
                ReportAsyncInvocation(context, operation, "field reference", operation.Syntax, promiseTypeName);
            }
        }

        private static void AnalyzePropertyReference(OperationAnalysisContext context)
        {
            var operation = (IPropertyReferenceOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (IsWatchedType(operation.Property.ContainingType))
            {
                ReportAsyncInvocation(context, operation, "property reference", operation.Syntax, promiseTypeName);
            }
        }

        private static void AnalyzeEventReference(OperationAnalysisContext context)
        {
            var operation = (IEventReferenceOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (IsWatchedType(operation.Event.ContainingType))
            {
                ReportAsyncInvocation(context, operation, "event reference", operation.Syntax, promiseTypeName);
            }
        }

        private static void AnalyzeObjectCreation(OperationAnalysisContext context)
        {
            var operation = (IObjectCreationOperation)context.Operation;
            var promiseTypeName = GetPromiseTypeName(context);
            if (IsWatchedType(operation.Type))
            {
                ReportAsyncInvocation(context, operation, "object creation", operation.Syntax, promiseTypeName);
            }
        }

        private static void ReportAsyncInvocation(OperationAnalysisContext context, IOperation operation, string kind, SyntaxNode locationNode, string promiseTypeName)
        {
            if (IsPromiseException(operation, promiseTypeName))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                SR.AsyncInvocationDetected,
                locationNode.GetLocation(),
                kind));
        }

        private static bool IsPromiseException(IOperation operation, string promiseTypeName)
        {
            for (IOperation? current = operation; current != null; current = current.Parent)
            {
                if (current is not IArgumentOperation argument)
                {
                    continue;
                }

                if (argument.Parameter?.Type?.Name == promiseTypeName)
                {
                    return true;
                }

                if (argument.Parent is IInvocationOperation invocation &&
                    invocation.TargetMethod.ContainingType?.Name == promiseTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetPromiseTypeName(OperationAnalysisContext context)
        {
            var cached = Volatile.Read(ref s_cachedPromiseTypeName);
            if (cached != null)
            {
                return cached;
            }

            // Cache once for the process lifetime. Config changes are picked up after app restart.
            var resolved = DefaultPromiseTypeName;
            string? normalized = null;

            if (context.Options.AnalyzerConfigOptionsProvider.GlobalOptions
                .TryGetValue(PromiseTypeNameOptionKey, out var configured))
            {
                normalized = NormalizeConfiguredPromiseTypeName(configured);
            }

            if (normalized == null &&
                context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Operation.Syntax.SyntaxTree)
                .TryGetValue(PromiseTypeNameOptionKey, out configured))
            {
                normalized = NormalizeConfiguredPromiseTypeName(configured);
            }

            resolved = normalized ?? resolved;

            var prior = Interlocked.CompareExchange(ref s_cachedPromiseTypeName, resolved, null);
            return prior ?? resolved;
        }

        private static string? NormalizeConfiguredPromiseTypeName(string? configured)
        {
            if (string.IsNullOrWhiteSpace(configured))
            {
                return null;
            }

            return configured!.Trim();
        }

        private static bool TryGetEnclosingAnonymousFunction(IOperation operation, out IAnonymousFunctionOperation anonymousFunction)
        {
            for (IOperation? current = operation.Parent; current != null; current = current.Parent)
            {
                if (current is IAnonymousFunctionOperation)
                {
                    anonymousFunction = (IAnonymousFunctionOperation)current;
                    return true;
                }
            }

            anonymousFunction = null!;
            return false;
        }

        private static bool IsTaskLike(ITypeSymbol? type)
        {
            if (type == null)
            {
                return false;
            }

            if (MatchesType(type, "System.Threading.Tasks", "ValueTask"))
            {
                return true;
            }

            for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
            {
                if (MatchesType(current, "System.Threading.Tasks", "Task"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWatchedType(ITypeSymbol? type)
        {
            if (type == null)
            {
                return false;
            }

            if (MatchesType(type, "System.Threading.Tasks", "Task") ||
                MatchesType(type, "System.Threading", "Thread", 0) ||
                MatchesType(type, "System.Threading", "ThreadPool", 0) ||
                MatchesType(type, "System.Threading.Tasks", "Parallel", 0) ||
                MatchesType(type, "System.Threading", "SynchronizationContext", 0) ||
                MatchesType(type, "System.Threading", "Timer", 0) ||
                MatchesType(type, "System.Threading", "PeriodicTimer", 0) ||
                MatchesType(type, "System.Timers", "Timer", 0))
            {
                return true;
            }

            if (type is INamedTypeSymbol named &&
                named.ConstructedFrom != null &&
                MatchesType(named.ConstructedFrom, "System.Threading.Tasks", "TaskCompletionSource", 1))
            {
                return true;
            }

            return MatchesType(type, "System.Threading.Tasks", "TaskCompletionSource", 0);
        }

        private static bool MatchesType(ITypeSymbol? type, string namespaceName, string name, int? typeArgumentCount = null)
        {
            if (type is not INamedTypeSymbol namedType)
            {
                return false;
            }

            if (!string.Equals(namedType.Name, name, StringComparison.Ordinal))
            {
                return false;
            }

            if (typeArgumentCount.HasValue && namedType.Arity != typeArgumentCount.Value)
            {
                return false;
            }

            return MatchesNamespace(namedType.ContainingNamespace, namespaceName);
        }

        private static bool MatchesNamespace(INamespaceSymbol? namespaceSymbol, string namespaceName)
        {
            var actual = namespaceSymbol?.ToDisplayString() ?? string.Empty;
            return string.Equals(actual, namespaceName, StringComparison.Ordinal);
        }
    }
}
