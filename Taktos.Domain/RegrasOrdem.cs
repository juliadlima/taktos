namespace Taktos.Domain;

public static class RegrasOrdem
{
    public static bool TipoAceito(string? tipo) => tipo is "Correcao" or "Melhoria";

    public static bool SituacaoDeTriagem(string? situacao) =>
        situacao is "Aberta" or "EmAnalise" or "EmExecucao";

    public static bool PrioridadeAceita(string? prioridade) =>
        prioridade is null or "Baixa" or "Media" or "Alta";

    public static bool ResultadoAceito(string? resultado) => resultado is "Aprovada" or "Devolvida";
}
