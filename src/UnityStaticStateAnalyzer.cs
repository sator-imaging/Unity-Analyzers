// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/Unity-Analyzers

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace UnityAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnityStaticStateAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SR.StaticStateSurvivesAcrossPlayMode, SR.MissingStateResetInRuntimeInitializeOnLoadMethod);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedType = (INamedTypeSymbol)context.Symbol;
            if (namedType.IsImplicitlyDeclared || namedType.TypeKind == TypeKind.Enum) return;

            var members = namedType.GetMembers();
            var targetMembers = members.Where(IsTargetStaticMember).ToArray();
            if (targetMembers.Length == 0) return;

            var resetMethods = members.OfType<IMethodSymbol>().Where(m => m.IsStatic && IsResetMethod(m)).ToArray();

            if (resetMethods.Length == 0)
            {
                foreach (var member in targetMembers)
                {
                    string memberType = GetMemberTypeString(member);
                    context.ReportDiagnostic(Diagnostic.Create(
                        SR.StaticStateSurvivesAcrossPlayMode,
                        member.Locations[0],
                        memberType,
                        member.Name));
                }
            }
            else
            {
                var assignedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                foreach (var method in resetMethods)
                {
                    foreach (var reference in method.DeclaringSyntaxReferences)
                    {
                        var syntax = reference.GetSyntax();
                        var semanticModel = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
                        var operation = semanticModel.GetOperation(syntax);
                        var finder = new AssignmentFinder();
                        finder.Visit(operation);
                        assignedSymbols.UnionWith(finder.AssignedSymbols);
                    }
                }

                foreach (var member in targetMembers)
                {
                    if (!assignedSymbols.Contains(member))
                    {
                        string memberType = GetMemberTypeString(member);
                        context.ReportDiagnostic(Diagnostic.Create(
                            SR.MissingStateResetInRuntimeInitializeOnLoadMethod,
                            member.Locations[0],
                            memberType,
                            member.Name));
                    }
                }
            }
        }

        private static string GetMemberTypeString(ISymbol member)
        {
            if (member is IFieldSymbol) return "field";
            if (member is IPropertySymbol) return "property";
            if (member is IEventSymbol) return "event";
            return "member";
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
                if (field.HasConstantValue) return false;
                return !(field.IsReadOnly && IsImmutable(field.Type));
            }

            if (member is IPropertySymbol property)
            {
                return !(property.IsReadOnly && IsImmutable(property.Type));
            }

            if (member is IEventSymbol)
            {
                return true;
            }

            return false;
        }

        private static bool IsImmutable(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String) return true;
            if (type.IsReferenceType) return false;

            return type.IsReadOnly || type.TypeKind == TypeKind.Enum;
        }

        private sealed class AssignmentFinder : OperationWalker
        {
            public readonly HashSet<ISymbol> AssignedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            public override void Visit(IOperation operation)
            {
                if (operation is IAssignmentOperation assignment)
                {
                    AddSymbol(assignment.Target);
                }
                else if (operation is ICompoundAssignmentOperation compound)
                {
                    AddSymbol(compound.Target);
                }
                else if (operation is IIncrementOrDecrementOperation incDec)
                {
                    AddSymbol(incDec.Target);
                }
                base.Visit(operation);
            }

            private void AddSymbol(IOperation target)
            {
                var symbol = GetTargetSymbol(target);
                if (symbol != null)
                {
                    AssignedSymbols.Add(symbol);
                    if (symbol is IFieldSymbol field && field.AssociatedSymbol != null)
                    {
                        AssignedSymbols.Add(field.AssociatedSymbol);
                    }
                }
            }
        }

        private static ISymbol? GetTargetSymbol(IOperation target)
        {
            if (target is IFieldReferenceOperation fieldRef) return fieldRef.Field;
            if (target is IPropertyReferenceOperation propRef) return propRef.Property;
            if (target is IEventReferenceOperation eventRef) return eventRef.Event;
            return null;
        }
    }
}
