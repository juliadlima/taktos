using System.Data;
using Microsoft.Data.SqlClient;
using Taktos.Domain;

namespace Taktos.Infrastructure;

public sealed class RepositorioEmpresas(RepositorioAcesso acesso)
{
    public async Task<List<EmpresaCliente>> ListarAsync(int fornecedoraId, CancellationToken token)
    {
        const string sql = """
            SELECT e.Id, e.Nome, e.Codigo, e.Ativa, s.Id, s.Nome
            FROM dbo.Empresas e
            LEFT JOIN dbo.Sistemas s ON s.EmpresaId = e.Id
            WHERE e.Tipo = 'Cliente' AND e.FornecedoraId = @FornecedoraId
            ORDER BY e.Nome, e.Id, s.Nome, s.Id;
            """;

        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        await using var leitor = await comando.ExecuteReaderAsync(token);

        var empresas = new List<EmpresaCliente>();
        EmpresaCliente? atual = null;
        while (await leitor.ReadAsync(token))
        {
            var id = leitor.GetInt32(0);
            if (atual is null || atual.Id != id)
            {
                atual = new EmpresaCliente(id, leitor.GetString(1), leitor.GetString(2), leitor.GetBoolean(3), []);
                empresas.Add(atual);
            }

            if (!leitor.IsDBNull(4))
                atual.Sistemas.Add(new OpcaoOrdem(leitor.GetInt32(4), leitor.GetString(5)));
        }

        return empresas;
    }

    public async Task<EmpresaCliente> CadastrarAsync(int fornecedoraId, string nome, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalCadastrarEmpresa", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = nome;
        await using var leitor = await comando.ExecuteReaderAsync(token);
        if (!await leitor.ReadAsync(token))
            throw new InvalidOperationException("O banco não retornou a empresa cadastrada.");

        return new EmpresaCliente(leitor.GetInt32(0), nome, leitor.GetString(1), true, []);
    }

    public async Task AtualizarAsync(int fornecedoraId, int empresaId, string? nome, bool? ativa,
        CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalAtualizarEmpresa", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = (object?)nome ?? DBNull.Value;
        comando.Parameters.Add("@Ativa", SqlDbType.Bit).Value = (object?)ativa ?? DBNull.Value;
        await comando.ExecuteNonQueryAsync(token);
    }

    public async Task<OpcaoOrdem> CadastrarSistemaAsync(
        int fornecedoraId, int empresaId, string nome, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalCadastrarSistema", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 100).Value = nome;
        var resultado = await comando.ExecuteScalarAsync(token);
        return resultado is int id
            ? new OpcaoOrdem(id, nome)
            : throw new InvalidOperationException("O banco não retornou o sistema cadastrado.");
    }
}
