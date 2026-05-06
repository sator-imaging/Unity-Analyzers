# Documentation Coverage Report

## Overall Check Result

The documentation in `README.md` (English) fully covers all implemented features and diagnostic rules.

## Detailed Result Follows

| Rule ID | Title | Status | Notes |
|---|---|---|---|
| **SIUA001** | Unreliable Unity object access | Covered | Detailed explanation, rules, safe/unsafe patterns, and exceptions are documented. |
| **SIUA002** | Await in safe block | Covered | Rule, unsafe/correct patterns, and strictness details are documented. |
| **SIUA011** | Static state survives across play modes | Covered | Explained the issue with Domain Reloading, rules, and patterns. |
| **SIUA012** | Missing state reset in `RuntimeInitializeOnLoadMethod` | Covered | Rule and unsafe pattern for missing reset assignments are documented. |
| **SIUA013** | Static property with body may return invalid static state | Covered | Explained the issue with non-auto-implemented static properties. |
| **SIUA021** | Async invocation detected | Covered | Comprehensive documentation on what is detected and how to suppress it (Promise type). |
| **SIUA031** | String-based Binding API | Covered | List of target methods and safe/unsafe patterns are documented. |
| **SIUA032** | String-based property ID | Covered | List of target methods and proper patterns using IDs are documented. |

### Additional Information Coverage
- **Common Analysis Behavior**: Documented for Async Method Analysis.
- **Promise Type Customization**: Config via `.editorconfig` is documented.
- **Suppressing by Category**: `AsyncPromise` category suppression is documented.
- **Unity Project Detection**: Usage in `Directory.Build.props` is documented.
- **Localized READMEs**: Links to Japanese and Chinese versions are present.
