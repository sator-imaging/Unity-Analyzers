// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/Unity-Analyzers

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
            ImmutableArray.Create(
                SR.StaticStateSurvivesAcrossPlayMode,
                SR.MissingStateResetInRuntimeInitializeOnLoadMethod,
                SR.StaticPropertyWithBodyMayReturnInvalidState,
                SR.StaticEventWithBodyIsNotAllowed);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
            context.RegisterOperationAction(AnalyzeMethodBody, OperationKind.MethodBody);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var namedType = (INamedTypeSymbol)context.Symbol;
            if (namedType.IsImplicitlyDeclared || namedType.TypeKind == TypeKind.Enum) return;

            var members = namedType.GetMembers();

            bool hasResetMethod = members.Any(m => m.IsStatic && m is IMethodSymbol method && IsResetMethod(method));

            foreach (var member in members)
            {
                if (!hasResetMethod && IsTargetStaticMember(member))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SR.StaticStateSurvivesAcrossPlayMode,
                        member.Locations[0],
                        GetMemberTypeDisplayName(member),
                        member.Name));
                }

                if (member is IPropertySymbol property && property.IsStatic && !property.IsImplicitlyDeclared)
                {
                    if (property.IsReadOnly && !IsAutoImplemented(property))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            SR.StaticPropertyWithBodyMayReturnInvalidState,
                            property.Locations[0],
                            property.Name));
                    }
                }

                if (member is IEventSymbol @event && @event.IsStatic && !@event.IsImplicitlyDeclared)
                {
                    if (!IsAutoImplemented(@event))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            SR.StaticEventWithBodyIsNotAllowed,
                            @event.Locations[0],
                            @event.Name));
                    }
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
                if (field.HasConstantValue) return false;
                return !(field.IsReadOnly && IsImmutable(field.Type));
            }

            if (member is IPropertySymbol property)
            {
                return !(property.IsReadOnly && IsImmutable(property.Type));
            }

            if (member is IMethodSymbol method)
            {
                return method.MethodKind is not (MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove);
            }

            if (member is IEventSymbol @event)
            {
                return IsAutoImplemented(@event);
            }

            return true;
        }

        private static bool IsImmutable(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String) return true;
            if (type.IsReferenceType) return false;

            return type.IsReadOnly || type.TypeKind == TypeKind.Enum;
        }

        private static bool IsAutoImplemented(IPropertySymbol property)
        {
            foreach (var syntaxRef in property.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax() is PropertyDeclarationSyntax propertyDeclaration)
                {
                    if (propertyDeclaration.ExpressionBody != null) return false;
                    if (propertyDeclaration.AccessorList == null) return false;

                    foreach (var accessor in propertyDeclaration.AccessorList.Accessors)
                    {
                        if (accessor.Body != null || accessor.ExpressionBody != null)
                            return false;
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool IsAutoImplemented(IEventSymbol @event)
        {
            return (@event.AddMethod?.IsImplicitlyDeclared ?? true) && (@event.RemoveMethod?.IsImplicitlyDeclared ?? true);
        }

        private static string GetMemberTypeDisplayName(ISymbol member)
        {
            if (member is IFieldSymbol) return "field";
            if (member is IPropertySymbol) return "property";
            if (member is IEventSymbol) return "event";
            return "member";
        }

        private static void AnalyzeMethodBody(OperationAnalysisContext context)
        {
            if (context.Operation is not IMethodBodyOperation methodBody) return;
            if (context.ContainingSymbol is not IMethodSymbol method) return;

            if (!IsResetMethod(method)) return;

            var type = method.ContainingType;
            var members = type.GetMembers();

            var walker = new AssignmentWalker();
            walker.Visit(methodBody);

            foreach (var member in members)
            {
                if (member is IMethodSymbol) continue;

                if (IsTargetStaticMember(member) && !walker.AssignedSymbols.Contains(member))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SR.MissingStateResetInRuntimeInitializeOnLoadMethod,
                        method.Locations[0],
                        GetMemberTypeDisplayName(member),
                        member.Name));
                }
            }
        }

        private class AssignmentWalker : OperationWalker
        {
            public HashSet<ISymbol> AssignedSymbols { get; } = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

            public override void VisitSimpleAssignment(ISimpleAssignmentOperation operation)
            {
                RegisterTarget(operation.Target);
                base.VisitSimpleAssignment(operation);
            }

            private void RegisterTarget(IOperation target)
            {
                if (target is IMemberReferenceOperation memberReference)
                {
                    var member = memberReference.Member;
                    AssignedSymbols.Add(member);
                    if (member is IFieldSymbol field && field.AssociatedSymbol != null)
                    {
                        AssignedSymbols.Add(field.AssociatedSymbol);
                    }
                }
            }
        }
    }
}
