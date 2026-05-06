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
    public sealed class UnityPropertyIdAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SR.StringBasedPropertyId);

        private static readonly Dictionary<string, HashSet<string>> TargetMethods = new Dictionary<string, HashSet<string>>
        {
            { "Animator", new HashSet<string> { "SetTrigger", "SetInteger", "SetFloat", "SetBool" } },
            { "Material", new HashSet<string> {
                "SetVectorArray", "SetVector", "SetTextureScale", "SetTextureOffset", "SetTexture", "SetPropertyLock",
                "SetMatrixArray", "SetMatrix", "SetInteger", "SetInt", "SetFloatArray", "SetFloat", "SetConstantBuffer",
                "SetColorArray", "SetColor", "SetBuffer"
            } },
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

            var type = method.ContainingType;
            if (type == null) return;

            if (!IsUnityType(type, out var typeName)) return;
            if (typeName == null) return;

            if (!TargetMethods.TryGetValue(typeName, out var methods)) return;
            if (!methods.Contains(method.Name)) return;

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
                        SR.StringBasedPropertyId,
                        operation.Syntax.GetLocation(),
                        method.Name));
                }
            }
        }

        private static bool IsUnityType(ITypeSymbol? typeSymbol, out string? typeName)
        {
            typeName = null;
            var currentType = typeSymbol as INamedTypeSymbol;
            while (currentType != null)
            {
                if (currentType.ContainingNamespace?.Name == "UnityEngine" &&
                    currentType.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true)
                {
                    if (currentType.Name == "Animator" || currentType.Name == "Material")
                    {
                        typeName = currentType.Name;
                        return true;
                    }
                }
                currentType = currentType.BaseType;
            }
            return false;
        }
    }
}
