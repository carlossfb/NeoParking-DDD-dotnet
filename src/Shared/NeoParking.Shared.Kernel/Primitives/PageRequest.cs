namespace NeoParking.Shared.Kernel.Primitives;

/// <summary>Página pedida por uma listagem, sempre dentro dos limites. Page é 1-based.</summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;

    public PageRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        Page = Math.Clamp(page, 1, int.MaxValue / MaxPageSize);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }
}
