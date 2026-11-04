---
name: clean-code-csharp
description: >
  Review, refactor, test, format, analyze, and security-check C# and .NET projects. Use when asked to "lint C#", "dotnet test", "dotnet format", "check nullability", "review .NET code", or "refactor C#". Works with .sln, .csproj.
compatibility: Requires .NET SDK 6+ with dotnet CLI.
metadata:
  author: clean-code-agent-skills
  version: "3.0.0"
  short-description: C#/.NET code review and refactoring
allowed-tools: Bash(dotnet:*) Read Grep Glob
argument-hint: <file-or-pattern>
---

# Clean Code for C# and .NET

## Preferred Tools

- Build: `dotnet build`
- Tests: `dotnet test`
- Formatting: `dotnet format`
- Analysis: Roslyn analyzers, StyleCop, SonarAnalyzer
- Coverage: Coverlet
- Dependencies: `dotnet list package --vulnerable`

## Suggested Commands

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable
```

## What to Check

- `async void` outside of event handlers (`CA1065`).
- Ignored `CancellationToken` (`CA2012`).
- Incorrect `IDisposable` implementation (`CA1001`, `CA2216`).
- Nullability not enabled or ignored (`CA1062`).
- Large service classes with many dependencies.
- Mixing domain logic with infrastructure.
- Unparameterized queries (`CA2100`).
- Exception leakage to public APIs (`CA2200`).
- Deadlocks from `.Result` or `.Wait()` (`CA2012`).

## Refactoring Guidance

- Separate domain, services, and infrastructure.
- Use dependency injection with reasonable bounds.
- Keep contracts as small interfaces.
- Use records and immutability where appropriate.
- Maintain compatibility with the project's current `TargetFramework`.

## Common Principles

> For full details, see [`_shared/COMMON_PRINCIPLES.md`](../_shared/COMMON_PRINCIPLES.md).

This skill does not assume build or test success. Tools must be run actually, and what succeeded and what failed must be recorded.

## Result Classification

> For full details, see [`_shared/RESULT_CLASSIFICATION.md`](../_shared/RESULT_CLASSIFICATION.md).

## What This Skill Does Not Do

- Enforce a specific `.editorconfig` or StyleCop ruleset — use the project's existing configuration.
- Migrate from .NET Framework to .NET Core — that is a separate task.
- Add ASP.NET-specific patterns to non-web projects.
- Handle non-C# files (Razor, F#, build scripts) unless they directly affect C# code quality.

## Examples

- **Review a .NET Web API:** Run dotnet test, format, and vulnerability scan. Flag `async void` methods, missing `CancellationToken` propagation, and `.Result` deadlocks.
- **Refactor a service class:** Split a 300-line service into focused interfaces. Verify behavior is preserved by running dotnet test before and after.
- **Fix a null reference:** Enable nullable reference types, add `?.` and `??` operators, and verify the fix compiles and passes tests.

## Self-Check Before Delivery

1. Did you run `dotnet build`, `dotnet test`, `dotnet format`, and vulnerability scan (or equivalents)?
2. Did you check for `async void`, missing `CancellationToken`, and `.Result`/`.Wait()` deadlocks?
3. Did you verify nullable reference types are enabled where applicable?
4. Did you verify no public API changed without user approval?
5. Did you re-run the test suite after any code changes?
6. Are all findings classified per [`_shared/RESULT_CLASSIFICATION.md`](../_shared/RESULT_CLASSIFICATION.md)?

If you cannot answer yes to every check, fix before shipping.
