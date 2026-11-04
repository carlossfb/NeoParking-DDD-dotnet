# C#/.NET Edge Cases and Harness Fallbacks

## Edge Cases

- **`async void`** — only for event handlers. Use `async Task` everywhere else.
- **`.Result` / `.Wait()`** — deadlocks in UI/SynchronizationContext. Use `await`.
- **`IDisposable` without `Dispose()`** — resource leaks. Use `using` statements.
- **`null` reference exceptions** — enable nullable reference types and use `?.` / `??`.
- **`string` comparison with `==`** — works but `string.Equals` with `Ordinal` is explicit.
- **`Task.Run` in async** — thread pool starvation. Use `await` instead.
- **`IEnumerable` deferred execution** — multiple enumeration. Materialize with `.ToList()` when needed.
- **Mass assignment** — bind only expected properties. Use DTOs, not entity models.

## Harness Fallbacks

- **dotnet CLI not installed** — skip build/test, note it. Do not fabricate results.
- **dotnet format not available** — skip formatting check, note it.
- **Roslyn analyzers not configured** — skip static analysis, note it.
- **Coverlet not installed** — skip coverage, note it.
