using System.Data;
using Microsoft.Data.SqlClient;
using Taktos.Domain;

namespace Taktos.Infrastructure;

public sealed class RepositorioEquipe(RepositorioAcesso acesso)
{
    public async Task<List<UsuarioFornecedora>> ListarAsync(int empresaId, int administradorId, CancellationToken token)
    {
        const string sql = """
            SELECT u.Id, u.Nome, u.Email, e.Nome, u.Ativo,
                CAST(CASE WHEN u.Id = e.AdministradorId THEN 1 ELSE 0 END AS bit)
            FROM dbo.Usuarios u JOIN dbo.Empresas e ON e.Id = u.EmpresaId
            JOIN dbo.Usuarios a ON a.Id = e.AdministradorId AND a.EmpresaId = e.Id AND a.Ativo = 1
            WHERE e.Id = @EmpresaId AND e.Tipo = 'Fornecedora' AND e.Ativa = 1 AND a.Id = @AdministradorId
            ORDER BY u.Nome, u.Id;
            """;

        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        comando.Parameters.Add("@AdministradorId", SqlDbType.Int).Value = administradorId;
        await using var leitor = await comando.ExecuteReaderAsync(token);

        var usuarios = new List<UsuarioFornecedora>();
        while (await leitor.ReadAsync(token))
        {
            usuarios.Add(new UsuarioFornecedora(leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2),
                leitor.GetString(3), leitor.GetBoolean(4), leitor.GetBoolean(5)));
        }

        return usuarios;
    }

    public async Task<int> CriarAsync(int empresaId, int administradorId, string nome, string email,
        string senhaHash, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = CriarComando(conexao, "dbo.PortalCriarUsuarioFornecedora", empresaId, administradorId);
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = nome;
        comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
        comando.Parameters.Add("@SenhaHash", SqlDbType.NVarChar, 500).Value = senhaHash;
        var id = await comando.ExecuteScalarAsync(token);
        return id is int usuarioId
            ? usuarioId
            : throw new InvalidOperationException("O banco não retornou o usuário cadastrado.");
    }

    public async Task AtualizarAsync(int empresaId, int administradorId, int usuarioId, string? nome,
        string? email, string? novoHash, bool? ativo, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = CriarComando(conexao, "dbo.PortalAtualizarUsuarioFornecedora", empresaId, administradorId);
        comando.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = (object?)nome ?? DBNull.Value;
        comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = (object?)email ?? DBNull.Value;
        comando.Parameters.Add("@NovoHash", SqlDbType.NVarChar, 500).Value = (object?)novoHash ?? DBNull.Value;
        comando.Parameters.Add("@Ativo", SqlDbType.Bit).Value = (object?)ativo ?? DBNull.Value;
        await comando.ExecuteNonQueryAsync(token);
    }

    public async Task AtualizarIdentidadeAsync(int empresaId, int administradorId, string nomeEmpresa,
        string nomePerfil, CancellationToken token)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = CriarComando(conexao, "dbo.PortalAtualizarIdentidadeFornecedora", empresaId, administradorId);
        comando.Parameters.Add("@NomeEmpresa", SqlDbType.NVarChar, 120).Value = nomeEmpresa;
        comando.Parameters.Add("@NomePerfil", SqlDbType.NVarChar, 120).Value = nomePerfil;
        await comando.ExecuteNonQueryAsync(token);
    }

    private static SqlCommand CriarComando(SqlConnection conexao, string procedure, int empresaId, int administradorId)
    {
        var comando = new SqlCommand(procedure, conexao) { CommandType = CommandType.StoredProcedure };
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        comando.Parameters.Add("@AdministradorId", SqlDbType.Int).Value = administradorId;
        return comando;
    }
}
