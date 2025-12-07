// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/Unity-Analyzers

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace UnityAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnityAsyncMethodAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SR.UnreliableMemberAccessInAyncMethod, SR.AwaitInSafeBlock);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationBlockAction(AnalyzeMethodBody);
        }

        private void AnalyzeMethodBody(OperationBlockAnalysisContext context)
        {
            if (context.OwningSymbol is not IMethodSymbol method || !method.IsAsync)
            {
                return;
            }

            var finder = new ViolationStartPositionFinder();
            var walker = new UnityAsyncOperationWalker(context);

            foreach (var operation in context.OperationBlocks)
            {
                // must reset here!!
                finder.ViolationStart = -1;
                finder.RootNode = operation.Syntax;

                finder.Visit(operation);

                var start = finder.ViolationStart;
                if (start < 0)
                {
                    start = int.MaxValue;  // Everything is safe if no await
                }

                //context.ReportDiagnostic(Diagnostic.Create(
                //    SR.UnreliableMemberAccessInAyncMethod,
                //    Location.Create(operation.Syntax.SyntaxTree, new Microsoft.CodeAnalysis.Text.TextSpan(start, 1)),
                //    "Violation Start Position: " + start));

                walker.ViolationStart = start;
                walker.Visit(operation);
            }
        }

        // Workaround as visitor cannot visit descendants in Visit() method.
        // --> Descendants().Count() is zero; assumes that first walker is running while building hierarchy...!!
        private sealed class ViolationStartPositionFinder : OperationWalker
        {
            public SyntaxNode? RootNode;
            public int ViolationStart;

            public override void Visit(IOperation operation)
            {
                // Skip unnecessary traversal (for all operation types)
                if (ViolationStart >= 0)
                {
                    return;
                }

                base.Visit(operation);
            }

            public override void VisitLabeled(ILabeledOperation operation)
            {
                // Skip unnecessary traversal (not necessary but making sure)
                if (ViolationStart >= 0)
                {
                    return;
                }

                ViolationStart = operation.Syntax.SpanStart;
            }

            public override void VisitAwait(IAwaitOperation operation)
            {
                // Skip unnecessary traversal (not necessary but making sure)
                if (ViolationStart >= 0)
                {
                    return;
                }

                var parent = operation.Syntax.Parent;

                // await only line: e.g., 'await Foo()' + ';'
                if (parent is ExpressionStatementSyntax)
                {
                    parent = parent.Parent;
                }
                // localOrField = await unityObj.FooAsync();
                else if (parent is AssignmentExpressionSyntax)
                {
                    var candidate = parent.Parent;
                    if (candidate is ExpressionStatementSyntax)
                    {
                        candidate = candidate.Parent;
                        if (candidate is BlockSyntax)
                        {
                            parent = candidate;
                        }
                    }
                }
                // var x = await unityObj.FooAsync();  // with 'var'
                else if (parent is EqualsValueClauseSyntax)
                {
                    var candidate = parent.Parent;
                    if (candidate is VariableDeclaratorSyntax)
                    {
                        candidate = candidate.Parent;
                        if (candidate is VariableDeclarationSyntax)
                        {
                            candidate = candidate.Parent;
                            if (candidate is LocalDeclarationStatementSyntax)
                            {
                                candidate = candidate.Parent;
                                if (candidate is BlockSyntax)
                                {
                                    parent = candidate;
                                }
                            }
                        }
                    }
                }
                // 'return await ...'
                else if (parent is ReturnStatementSyntax)
                {
                    parent = RootNode;  // Set root to ignore first 'return await' in any depth
                }

                // Unwrap root try block
                if (parent != RootNode && parent?.Parent is TryStatementSyntax)
                {
                    parent = parent.Parent.Parent;
                }

                ViolationStart = parent == RootNode || parent == null
                    // Points to the end of expression if it's on method root
                    ? operation.Syntax.Span.End
                    // Otherwise points to start of parent (foreach, while, block or etc)
                    : parent.SpanStart;
            }
        }

        private sealed class UnityAsyncOperationWalker : OperationWalker
        {
            private readonly OperationBlockAnalysisContext _context;
            public int ViolationStart;

            public UnityAsyncOperationWalker(OperationBlockAnalysisContext context)
            {
                _context = context;
            }

            public override void VisitConditional(IConditionalOperation operation)
            {
                // "explicitly visit 'if' statements ONLY"
                if (operation.Syntax is IfStatementSyntax)
                {
                    if (IsUnityObjectNotNullCondition(operation.Condition))
                    {
                        // Check for 'await' in the safe block
                        foreach (var op in operation.WhenTrue.Descendants())
                        {
                            if (op.Kind == OperationKind.Await)
                            {
                                _context.ReportDiagnostic(Diagnostic.Create(
                                    SR.AwaitInSafeBlock,
                                    op.Syntax.GetLocation()));
                            }
                        }

                        Visit(operation.Condition);
                        Visit(operation.WhenFalse);

                        // Skip base.VisitConditional() to avoid analyzing WhenTrue (safe block)
                        return;
                    }
                }

                base.VisitConditional(operation);
            }


            public override void VisitFieldReference(IFieldReferenceOperation operation)
            {
                CheckUnsafeMemberAccess(operation.Syntax, operation.Instance);
                base.VisitFieldReference(operation);
            }

            public override void VisitPropertyReference(IPropertyReferenceOperation operation)
            {
                CheckUnsafeMemberAccess(operation.Syntax, operation.Instance);
                base.VisitPropertyReference(operation);
            }

            public override void VisitEventReference(IEventReferenceOperation operation)
            {
                CheckUnsafeMemberAccess(operation.Syntax, operation.Instance);
                base.VisitEventReference(operation);
            }

            public override void VisitMethodReference(IMethodReferenceOperation operation)
            {
                CheckUnsafeMemberAccess(operation.Syntax, operation.Instance);
                base.VisitMethodReference(operation);
            }


            public override void VisitInvocation(IInvocationOperation operation)
            {
                CheckUnsafeMemberAccess(operation.Syntax, operation.Instance);
                base.VisitInvocation(operation);
            }

            public override void VisitArgument(IArgumentOperation operation)
            {
                var cv = operation.Value.ConstantValue;
                if (!cv.HasValue || cv.Value != null)
                {
                    CheckUnsafeMemberAccess(operation.Syntax, operation.Value);
                }

                base.VisitArgument(operation);
            }


            private void CheckUnsafeMemberAccess(SyntaxNode syntax, IOperation? instance)
            {
                // "no static. only instance member of any Unity type"
                // "this.Foo and Foo must be considered" -> operation.Instance handles 'this'
                if (instance != null && IsUnityObject(instance.Type))
                {
                    // Ignore access before first await
                    if (syntax.SpanStart < ViolationStart)
                    {
                        return;
                    }

                    ReportDiagnostic(syntax, instance.Type);
                }
            }

            private void ReportDiagnostic(SyntaxNode syntax, ITypeSymbol receiverType)
            {
                _context.ReportDiagnostic(Diagnostic.Create(
                    SR.UnreliableMemberAccessInAyncMethod,
                    syntax.GetLocation(), // Use syntax location for the squiggle
                    receiverType.Name));
            }
        }

        private static bool IsUnityObject(ITypeSymbol? typeSymbol)
        {
            var currentType = typeSymbol as INamedTypeSymbol;
            while (currentType != null)
            {
                const string UnityEngine = nameof(UnityEngine);

                if (currentType.ContainingNamespace?.ContainingNamespace?.IsGlobalNamespace == true &&
                    currentType.ContainingNamespace.Name is UnityEngine)
                {
                    const string Object = nameof(Object);
                    const string Component = nameof(Component);
                    const string MonoBehaviour = nameof(MonoBehaviour);

                    if (currentType.Name is MonoBehaviour or Component or Object)
                    {
                        return true;
                    }
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        private static bool IsUnityObjectNotNullCondition(IOperation? operation)
        {
            if (operation == null)
            {
                return false;
            }

            // Unwrap parentheses
            while (operation is IParenthesizedOperation parenthesized)
            {
                operation = parenthesized.Operand;
            }

            if (operation is IBinaryOperation binaryOp)
            {
                if (binaryOp.OperatorKind == BinaryOperatorKind.ConditionalAnd)
                {
                    return IsUnityObjectNotNullCondition(binaryOp.LeftOperand) && IsUnityObjectNotNullCondition(binaryOp.RightOperand);
                }

                if (binaryOp.OperatorKind == BinaryOperatorKind.NotEquals)
                {
                    var left = UnwrapImplicitConversion(binaryOp.LeftOperand);
                    var right = UnwrapImplicitConversion(binaryOp.RightOperand);

                    if (IsUnityObject(left.Type) && IsNullLiteral(right))
                    {
                        return IsSafeToUseAsGuard(left);
                    }

                    if (IsUnityObject(right.Type) && IsNullLiteral(left))
                    {
                        return IsSafeToUseAsGuard(right);
                    }
                }
            }

            return false;
        }

        private static bool IsSafeToUseAsGuard(IOperation operation)
        {
            // Unwrap again just in case, though usually done by caller
            operation = UnwrapImplicitConversion(operation);

            if (operation is ILocalReferenceOperation or IParameterReferenceOperation)
            {
                return true;
            }

            // If it's a member access (Prop, Field, Method), it's safe ONLY if the Instance is NOT a Unity Object.
            // (e.g. plainCsharpObj.unityProperty is safe to read. unityObj.unityProperty is NOT).
            if (operation is IMemberReferenceOperation memberRef)
            {
                if (memberRef.Instance == null)
                {
                    return true; // Static
                }

                return !IsUnityObject(memberRef.Instance.Type);
            }

            if (operation is IInvocationOperation invocation)
            {
                if (invocation.Instance == null)
                {
                    return true; // Static
                }

                return !IsUnityObject(invocation.Instance.Type);
            }

            // By constrast from operation walker, this method doesn't traverse hierarchy.
            //   ex) array[0]
            //       * walker: local or member access --> property (indexer) access
            //       *   this: local or member access (does't descend into array indexer)
            // So need to explicitly check for simple nullchecks: e.g., if (array[0] != null)
            if (operation is IArrayElementReferenceOperation arrayRef)
            {
                // Check array instance type
                return !IsUnityObject(arrayRef.ArrayReference.Type);
            }

            // Default to unsafe for complex expressions
            return false;
        }

        private static IOperation UnwrapImplicitConversion(IOperation operation)
        {
            while (operation is IConversionOperation conversion && conversion.IsImplicit)
            {
                operation = conversion.Operand;
            }
            return operation;
        }

        private static bool IsNullLiteral(IOperation operation)
        {
            // IsNullLiteral already unwraps conversions logic, but let's be consistent.
            operation = UnwrapImplicitConversion(operation);

            return operation is ILiteralOperation literal &&
                   literal.ConstantValue.HasValue &&
                   literal.ConstantValue.Value == null;
        }
    }
}
