namespace Taktos.Domain;

public sealed record RelatoInclusao(
    int Id, int? OrdemId, string Categoria, string Descricao, string Situacao,
    string? Resposta, DateTime CriadoEm, DateTime? RevisadoEm, bool Meu, bool PodeTratar);
