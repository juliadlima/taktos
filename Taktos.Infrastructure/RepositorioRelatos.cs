using System.Data;
using Microsoft.Data.SqlClient;
using Taktos.Domain;

namespace Taktos.Infrastructure;

public sealed class RepositorioRelatos(RepositorioAcesso acesso)
{
    public async Task<List<RelatoInclusao>> ListarAsync(int usuarioId, CancellationToken token)
    {
        const string sql = """
            SELECT r.Id, r.OrdemId, r.Categoria, r.Descricao, r.Situacao, r.Resposta, r.CriadoEm, r.RevisadoEm,
                CAST(CASE WHEN r.AutorId = u.Id THEN 1 ELSE 0 END AS bit) AS Meu,
                CAST(CASE WHEN e.AdministradorId = u.Id AND u.EmpresaId = e.Id AND e.Ativa = 1
                          THEN 1 ELSE 0 END AS bit) AS PodeTratar
            FROM dbo.RelatosInclusao r
            JOIN dbo.Empresas e ON e.Id = r.FornecedoraId
            JOIN dbo.Usuarios u ON u.Id = @UsuarioId AND u.Ativo = 1
            JOIN dbo.Empresas v ON v.Id = u.EmpresaId AND v.Ativa = 1
            WHERE r.AutorId = u.Id OR (e.AdministradorId = u.Id AND u.EmpresaId = e.Id AND e.Ativa = 1)
            ORDER BY r.Id DESC;
            """;
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
        await using var leitor = await comando.ExecuteReaderAsync(token);
        var relatos = new List<RelatoInclusao>();

        while (await leitor.ReadAsync(token))
        {
            relatos.Add(new RelatoInclusao(leitor.GetInt32(0), leitor.IsDBNull(1) ? null : leitor.GetInt32(1),
                leitor.GetString(2), leitor.GetString(3), leitor.GetString(4),
                leitor.IsDBNull(5) ? null : leitor.GetString(5), DateTime.SpecifyKind(leitor.GetDateTime(6), DateTimeKind.Utc),
                leitor.IsDBNull(7) ? null : DateTime.SpecifyKind(leitor.GetDateTime(7), DateTimeKind.Utc),
                leitor.GetBoolean(8), leitor.GetBoolean(9)));
        }

        return relatos;
    }

    public async Task<int> RegistrarAsync(int usuarioId, string categoria, string descricao, int? ordemId,
        CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = NovoComando(conexao, "dbo.PortalRegistrarRelato", usuarioId);
        comando.Parameters.Add("@Categoria", SqlDbType.VarChar, 20).Value = categoria;
        comando.Parameters.Add("@Descricao", SqlDbType.NVarChar, 2000).Value = descricao;
        comando.Parameters.Add("@OrdemId", SqlDbType.Int).Value = (object?)ordemId ?? DBNull.Value;
        var resultado = await comando.ExecuteScalarAsync(token);
        return resultado is int id ? id : throw new InvalidOperationException("O banco não retornou o relato cadastrado.");
    }

    public async Task TratarAsync(int usuarioId, int relatoId, string situacao, string? resposta, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = NovoComando(conexao, "dbo.PortalTratarRelato", usuarioId);
        comando.Parameters.Add("@RelatoId", SqlDbType.Int).Value = relatoId;
        comando.Parameters.Add("@Situacao", SqlDbType.VarChar, 12).Value = situacao;
        comando.Parameters.Add("@Resposta", SqlDbType.NVarChar, 2000).Value = (object?)resposta ?? DBNull.Value;
        await comando.ExecuteNonQueryAsync(token);
    }

    private static SqlCommand NovoComando(SqlConnection conexao, string nome, int usuarioId)
    {
        var comando = new SqlCommand(nome, conexao) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
        return comando;
    }
}
