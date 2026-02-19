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
            if (namedType.TypeKind == TypeKind.Enum) return;

            var members = namedType.GetMembers();

            bool hasResetMethod = members.Any(m => m.IsStatic && m is IMethodSymbol method && IsResetMethod(method));
            if (hasResetMethod) return;

            foreach (var member in members)
            {
                if (IsTargetStaticMember(member))
                {
                    string memberType;
                    if (member is IFieldSymbol) memberType = "field";
                    else if (member is IPropertySymbol) memberType = "property";
                    else continue;

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
                if (field.IsReadOnly && IsImmutable(field.Type))
                {
                    return false;
                }
                return true;
            }

            if (member is IPropertySymbol property)
            {
                if (property.IsReadOnly && IsImmutable(property.Type))
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        private static bool IsImmutable(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String) return true;

            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.TypeKind == TypeKind.Enum) return true;
                if (namedType.IsReadOnly) return true;
            }

            return false;
        }
    }
}
