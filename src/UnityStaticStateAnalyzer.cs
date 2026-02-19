// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/Unity-Analyzers

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace UnityAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnityStaticStateAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SR.StaticStateSurvivesAcrossPlayMode);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedType = (INamedTypeSymbol)context.Symbol;
            var members = namedType.GetMembers();

            bool hasResetMethod = members.Any(m => m.IsStatic && m is IMethodSymbol method && IsResetMethod(method));
            if (hasResetMethod) return;

            foreach (var member in members)
            {
                if (IsTargetStaticMember(member))
                {
                    var memberType = member is IFieldSymbol ? "field" : "property";
                    context.ReportDiagnostic(Diagnostic.Create(
                        SR.StaticStateSurvivesAcrossPlayMode,
                        member.Locations[0],
                        memberType,
                        member.Name));
                }
            }
        }

        private static bool IsResetMethod(IMethodSymbol method)
        {
            return method.GetAttributes().Any(attr => attr.AttributeClass?.Name is "RuntimeInitializeOnLoadMethodAttribute" or "RuntimeInitializeOnLoadMethod");
        }

        private static bool IsTargetStaticMember(ISymbol member)
        {
            if (!member.IsStatic || member.IsImplicitlyDeclared) return false;

            if (member is IFieldSymbol field)
            {
                if (field.IsReadOnly)
                {
                    var type = field.Type;
                    if (type.SpecialType == SpecialType.System_String) return false;

                    if (type is INamedTypeSymbol namedType)
                    {
                        if (namedType.IsValueType)
                        {
                            // Primitive types (int, float, etc.) are essentially readonly
                            if (namedType.SpecialType != SpecialType.None) return false;

                            // User-defined readonly structs
                            if (namedType.IsReadOnly) return false;
                        }
                    }
                }
                return true;
            }

            return member is IPropertySymbol;
        }
    }
}
