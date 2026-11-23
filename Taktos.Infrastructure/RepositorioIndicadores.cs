using System.Data;
using Microsoft.Data.SqlClient;
using Taktos.Domain;

namespace Taktos.Infrastructure;

public sealed class RepositorioIndicadores(RepositorioAcesso acesso)
{
    public async Task<List<IndicadorOrdem>> ListarAsync(int empresaId, CancellationToken token)
    {
        const string sql = """
            WITH Escopo AS (
                SELECT CASE WHEN Tipo = 'Fornecedora' THEN Id ELSE FornecedoraId END AS FornecedoraId
                FROM dbo.Empresas WHERE Id = @EmpresaId AND Ativa = 1
            )
            SELECT p.Id, COALESCE(a.Nome, p.Nome) COLLATE Latin1_General_100_CI_AI AS Nome,
                COALESCE(a.Criterio, p.Criterio) AS Criterio,
                CAST(1 AS bit) AS Padrao
            FROM Escopo e
            CROSS JOIN (VALUES
                (-3, N'OSs abertas sem previsão', 'SemPrevisao'),
                (-2, N'Primeiras entregas no prazo', 'PrimeirasNoPrazo'),
                (-1, N'Versões devolvidas', 'VersoesDevolvidas')
            ) p(Id, Nome, Criterio)
            LEFT JOIN dbo.IndicadoresPadraoOrdem a ON a.FornecedoraId = e.FornecedoraId AND a.IndicadorId = p.Id
            UNION ALL
            SELECT i.Id, i.Nome, i.Criterio, CAST(0 AS bit)
            FROM Escopo e JOIN dbo.IndicadoresOrdem i ON i.FornecedoraId = e.FornecedoraId
            ORDER BY Id;
            """;

        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        await using var leitor = await comando.ExecuteReaderAsync(token);
        var indicadores = new List<IndicadorOrdem>();

        while (await leitor.ReadAsync(token))
            indicadores.Add(new IndicadorOrdem(leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2), leitor.GetBoolean(3)));

        return indicadores;
    }

    public async Task<IndicadorOrdem> CadastrarAsync(int fornecedoraId, string nome, string criterio,
        CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = CriarComando(conexao, "dbo.PortalCadastrarIndicador", fornecedoraId, nome, criterio);
        var resultado = await comando.ExecuteScalarAsync(token);
        return resultado is int id
            ? new IndicadorOrdem(id, nome, criterio, false)
            : throw new InvalidOperationException("O banco não retornou o indicador cadastrado.");
    }

    public async Task<IndicadorOrdem> AtualizarAsync(int fornecedoraId, int id, string nome, string criterio,
        CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = CriarComando(conexao, "dbo.PortalAtualizarIndicador", fornecedoraId, nome, criterio);
        comando.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await comando.ExecuteNonQueryAsync(token);
        return new IndicadorOrdem(id, nome, criterio, id < 0);
    }

    private static SqlCommand CriarComando(SqlConnection conexao, string procedimento, int fornecedoraId,
        string nome, string criterio)
    {
        var comando = new SqlCommand(procedimento, conexao) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = nome;
        comando.Parameters.Add("@Criterio", SqlDbType.VarChar, 20).Value = criterio;
        return comando;
    }
}
