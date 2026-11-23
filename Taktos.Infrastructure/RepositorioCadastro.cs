using System.Data;
using Microsoft.Data.SqlClient;
using Taktos.Domain;

namespace Taktos.Infrastructure;

public sealed class RepositorioCadastro(RepositorioAcesso acesso)
{
    public async Task SolicitarAsync(string empresa, string nome, string email, string senhaHash,
        CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalSolicitarCadastro", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@Empresa", SqlDbType.NVarChar, 120).Value = empresa;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = nome;
        comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
        comando.Parameters.Add("@SenhaHash", SqlDbType.NVarChar, 500).Value = senhaHash;
        await comando.ExecuteNonQueryAsync(token);
    }

    public async Task<string?> BuscarSenhaPendenteAsync(string email, CancellationToken token)
    {
        const string sql = """
            SELECT s.SenhaHash FROM dbo.SolicitacoesCadastro s JOIN dbo.Empresas e ON e.Id = s.EmpresaId
            WHERE s.Email = @Email AND s.Situacao = 'Pendente' AND e.Ativa = 1;
            """;
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
        return await comando.ExecuteScalarAsync(token) as string;
    }

    public async Task<List<SolicitacaoCadastro>> ListarPendentesAsync(int fornecedoraId, CancellationToken token)
    {
        const string sql = """
            SELECT s.Id, e.Nome, s.Nome, s.Email, s.SolicitadaEm, e.Ativa
            FROM dbo.SolicitacoesCadastro s JOIN dbo.Empresas e ON e.Id=s.EmpresaId
            WHERE e.Tipo='Cliente' AND e.FornecedoraId=@FornecedoraId AND s.Situacao='Pendente'
            ORDER BY s.SolicitadaEm, s.Id;
            """;
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        await using var leitor = await comando.ExecuteReaderAsync(token);
        var itens = new List<SolicitacaoCadastro>();
        while (await leitor.ReadAsync(token))
        {
            itens.Add(new SolicitacaoCadastro(leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2),
                leitor.GetString(3), DateTime.SpecifyKind(leitor.GetDateTime(4), DateTimeKind.Utc), leitor.GetBoolean(5)));
        }

        return itens;
    }

    public async Task DecidirAsync(int id, int fornecedoraId, int usuarioId, bool aprovar, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalDecidirCadastro", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@SolicitacaoId", SqlDbType.Int).Value = id;
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        comando.Parameters.Add("@DecididaPor", SqlDbType.Int).Value = usuarioId;
        comando.Parameters.Add("@Aprovar", SqlDbType.Bit).Value = aprovar;
        await comando.ExecuteNonQueryAsync(token);
    }

    public async Task<List<ContaCliente>> ListarContasAsync(int fornecedoraId, CancellationToken token)
    {
        const string sql = """
            SELECT u.Id, u.Nome, u.Email, e.Id, e.Nome, u.Ativo, e.Ativa
            FROM dbo.Usuarios u JOIN dbo.Empresas e ON e.Id = u.EmpresaId
            WHERE e.Tipo = 'Cliente' AND e.FornecedoraId = @FornecedoraId
            ORDER BY e.Nome, u.Nome, u.Id;
            """;
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        await using var leitor = await comando.ExecuteReaderAsync(token);

        var contas = new List<ContaCliente>();
        while (await leitor.ReadAsync(token))
        {
            contas.Add(new ContaCliente(leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2),
                leitor.GetInt32(3), leitor.GetString(4), leitor.GetBoolean(5), leitor.GetBoolean(6)));
        }

        return contas;
    }

    public async Task<int> CriarContaAsync(int fornecedoraId, int empresaId, string nome, string email,
        string senhaHash, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalCriarContaCliente", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = nome;
        comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
        comando.Parameters.Add("@SenhaHash", SqlDbType.NVarChar, 500).Value = senhaHash;
        var resultado = await comando.ExecuteScalarAsync(token);
        return resultado is int id
            ? id
            : throw new InvalidOperationException("O banco não retornou a conta cadastrada.");
    }

    public async Task AtualizarContaAsync(int fornecedoraId, int usuarioId, string? nome, string? email,
        string? novoHash, bool? ativo, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalAtualizarContaCliente", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@FornecedoraId", SqlDbType.Int).Value = fornecedoraId;
        comando.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = (object?)nome ?? DBNull.Value;
        comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = (object?)email ?? DBNull.Value;
        comando.Parameters.Add("@NovoHash", SqlDbType.NVarChar, 500).Value = (object?)novoHash ?? DBNull.Value;
        comando.Parameters.Add("@Ativo", SqlDbType.Bit).Value = (object?)ativo ?? DBNull.Value;
        await comando.ExecuteNonQueryAsync(token);
    }
}
