// Licensed under the Apache-2.0 License
// https://github.com/sator-imaging/Unity-Analyzers

using Microsoft.CodeAnalysis;

#pragma warning disable RS2008 // Enable analyzer release tracking

namespace UnityAnalyzers
{
    internal static class SR
    {
        const string Category = nameof(UnityAnalyzers);
        const string IdPrefix = "SIUA";

        public static readonly DiagnosticDescriptor UnreliableMemberAccessInAyncMethod = new DiagnosticDescriptor(
            id: IdPrefix + "001",
            title: "Unreliable Unity object access",
            messageFormat: "Accessing the instance member of Unity object '{0}' outside of nullcheck in async method.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor AwaitInSafeBlock = new DiagnosticDescriptor(
            id: IdPrefix + "002",
            title: "Await in safe block",
            messageFormat: "Avoiding usage of 'await' in the safe block. It may break the safe context.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor StaticStateSurvivesAcrossPlayMode = new DiagnosticDescriptor(
            id: IdPrefix + "011",
            title: "Static state survives across play modes",
            messageFormat: "Static {0} '{1}' survives across play modes when Domain Reloading is disabled. Consider using '[RuntimeInitializeOnLoadMethod]' to reset it.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor MissingStateResetInRuntimeInitializeOnLoadMethod = new DiagnosticDescriptor(
            id: IdPrefix + "012",
            title: "Missing state reset in RuntimeInitializeOnLoadMethod",
            messageFormat: "Static {0} '{1}' is not reset in this [RuntimeInitializeOnLoadMethod] method.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor StaticPropertyWithBodyMayReturnInvalidState = new DiagnosticDescriptor(
            id: IdPrefix + "013",
            title: "Static property with body may return invalid static state",
            messageFormat: "Static property '{0}' with getter body may return invalid static state when Domain Reloading is disabled. Consider using an auto-implemented property instead. (e.g. `static int Property { get; } = 0;`)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor StaticEventWithBodyIsNotAllowed = new DiagnosticDescriptor(
            id: IdPrefix + "014",
            title: "Static event with body",
            messageFormat: "Static event '{0}' with body is not allowed. Consider using an auto-implemented event instead. (e.g. `static event Action Event;`)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor AsyncInvocationDetected = new DiagnosticDescriptor(
            id: IdPrefix + "021",
            title: "Async invocation detected",
            messageFormat: "Detected untracked async invocation source: {0}.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }
}
