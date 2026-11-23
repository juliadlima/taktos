USE [PortalEntregas];
GO

CREATE OR ALTER PROCEDURE dbo.PortalListarOrdens
    @EmpresaId int, @Fornecedora bit, @OrdemId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.Id, e.Nome AS Empresa, s.Nome AS Sistema, o.Tipo, o.Titulo, o.Descricao, o.Situacao,
        o.Prioridade, o.ResponsavelId, r.Nome AS Responsavel, u.Nome AS Solicitante,
        CONVERT(char(10), o.PrevisaoEntrega, 23) AS Previsao,
        CONVERT(char(10), o.PrimeiraPrevisao, 23) AS PrimeiraPrevisao,
        o.CriadaEm, o.AtualizadaEm, dados.PrimeiraEntrega, dados.VersoesDecididas, dados.VersoesDevolvidas,
        o.SolicitanteId
    FROM dbo.OrdensServico o
    JOIN dbo.Empresas e ON e.Id = o.EmpresaId
    JOIN dbo.Sistemas s ON s.Id = o.SistemaId
    JOIN dbo.Usuarios u ON u.Id = o.SolicitanteId
    LEFT JOIN dbo.Usuarios r ON r.Id = o.ResponsavelId
    OUTER APPLY (
        SELECT MIN(x.CriadaEm) AS PrimeiraEntrega,
            COUNT(CASE WHEN x.Resultado <> 'Pendente' THEN 1 END) AS VersoesDecididas,
            COUNT(CASE WHEN x.Resultado = 'Devolvida' THEN 1 END) AS VersoesDevolvidas
        FROM dbo.Entregas x WHERE x.OrdemId = o.Id
    ) AS dados
    WHERE ((@Fornecedora = 1 AND e.FornecedoraId = @EmpresaId)
        OR (@Fornecedora = 0 AND o.EmpresaId = @EmpresaId))
        AND (@OrdemId IS NULL OR o.Id = @OrdemId)
    ORDER BY o.AtualizadaEm DESC, o.Id DESC;
END;
GO
