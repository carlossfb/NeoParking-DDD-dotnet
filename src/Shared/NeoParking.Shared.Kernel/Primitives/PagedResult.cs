namespace NeoParking.Shared.Kernel.Primitives;

/// <summary>
/// Envelope genérico para respostas paginadas. TotalCount é o total de itens que
/// atendem ao filtro (antes da paginação) — necessário pro client calcular quantas
/// páginas existem, já que Items só traz a página atual.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
