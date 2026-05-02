// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/Unity-Analyzers

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace UnityAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnityStringBindingAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SR.StringBasedBindingApi);

        private static readonly HashSet<string> TargetMethods = new HashSet<string>
        {
            "StartCoroutine",
            "StopCoroutine",
            "Invoke",
            "InvokeRepeating",
            "CancelInvoke",
            "SendMessage",
            "SendMessageUpwards",
            "BroadcastMessage"
        };

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private static void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var operation = (IInvocationOperation)context.Operation;
            var method = operation.TargetMethod;

            if (method == null) return;
            if (!TargetMethods.Contains(method.Name)) return;

            var instance = operation.Instance;
            if (instance == null) return;

            if (!IsInheritedFromMonoBehaviour(instance.Type)) return;

            if (operation.Arguments.Length > 0)
            {
                var firstArg = operation.Arguments[0];
                var value = firstArg.Value;

                // Unwrap implicit conversion to check the actual provided type
                while (value is IConversionOperation conversion && conversion.IsImplicit)
                {
                    value = conversion.Operand;
                }

                if (value.Type?.SpecialType == SpecialType.System_String)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SR.StringBasedBindingApi,
                        operation.Syntax.GetLocation(),
                        method.Name));
                }
            }
        }

        private static bool IsInheritedFromMonoBehaviour(ITypeSymbol? typeSymbol)
        {
            var currentType = typeSymbol as INamedTypeSymbol;
            while (currentType != null)
            {
                const string UnityEngine = nameof(UnityEngine);
                if (currentType.Name == "MonoBehaviour" &&
                    currentType.ContainingNamespace?.Name == UnityEngine &&
                    currentType.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true)
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            return false;
        }
    }
}
