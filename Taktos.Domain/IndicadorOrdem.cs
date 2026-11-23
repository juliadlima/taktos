namespace Taktos.Domain;

public sealed record IndicadorOrdem(int Id, string Nome, string Criterio, bool Padrao)
{
    public static bool CriterioAceito(string? criterio) => criterio is
        "Total" or "Aberta" or "EmAnalise" or "EmExecucao" or "EmHomologacao" or "Concluida" or
        "SemResponsavel" or "Correcao" or "Melhoria" or "SemPrevisao" or "PrimeirasNoPrazo" or "VersoesDevolvidas";
}
