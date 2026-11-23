using System.Data;
using Microsoft.Data.SqlClient;
using Taktos.Domain;

namespace Taktos.Infrastructure;

public sealed class RepositorioAcesso(string? stringConexao)
{
    public SqlConnection CriarConexao()
    {
        if (string.IsNullOrWhiteSpace(stringConexao))
        {
            throw new InvalidOperationException("Configure a conexão PortalEntregas antes de acessar o banco.");
        }

        return new SqlConnection(stringConexao);
    }

    public Task<UsuarioAcesso?> BuscarUsuarioAsync(string email, CancellationToken token) =>
        BuscarAsync("u.Email = @Busca", new SqlParameter("@Busca", SqlDbType.NVarChar, 254) { Value = email }, token);

    public Task<UsuarioAcesso?> BuscarUsuarioPorIdAsync(int usuarioId, CancellationToken token) =>
        BuscarAsync("u.Id = @Busca", new SqlParameter("@Busca", SqlDbType.Int) { Value = usuarioId }, token);

    private async Task<UsuarioAcesso?> BuscarAsync(string criterio, SqlParameter parametro, CancellationToken token)
    {
        var sql = """
            SELECT u.Id, u.Nome, u.Email, u.SenhaHash, e.Id, e.Nome, e.Tipo,
                u.Ativo, e.Ativa, u.VersaoSessao, e.VersaoSessao,
                COALESCE(e.NomePerfil, CASE WHEN e.Tipo = 'Fornecedora' THEN N'Equipe fornecedora' ELSE N'TI do cliente' END),
                CAST(CASE WHEN e.Tipo = 'Fornecedora' AND e.AdministradorId = u.Id THEN 1 ELSE 0 END AS bit)
            FROM dbo.Usuarios AS u INNER JOIN dbo.Empresas AS e ON e.Id = u.EmpresaId
            WHERE
            """ + " " + criterio;

        await using var conexao = CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add(parametro);
        await using var leitor = await comando.ExecuteReaderAsync(token);

        if (!await leitor.ReadAsync(token))
            return null;

        return new UsuarioAcesso(leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2),
            leitor.GetString(3), leitor.GetInt32(4), leitor.GetString(5), leitor.GetString(6),
            leitor.GetBoolean(7), leitor.GetBoolean(8), leitor.GetInt32(9), leitor.GetInt32(10),
            leitor.GetString(11), leitor.GetBoolean(12));
    }

    public async Task<DadosSessao?> BuscarSessaoAtivaAsync(int usuarioId, CancellationToken token)
    {
        const string sql = """
            SELECT u.Nome, e.Id, e.Nome, e.Tipo, u.VersaoSessao, e.VersaoSessao,
                COALESCE(e.NomePerfil, CASE WHEN e.Tipo = 'Fornecedora' THEN N'Equipe fornecedora' ELSE N'TI do cliente' END),
                CAST(CASE WHEN e.Tipo = 'Fornecedora' AND e.AdministradorId = u.Id THEN 1 ELSE 0 END AS bit)
            FROM dbo.Usuarios u JOIN dbo.Empresas e ON e.Id = u.EmpresaId
            WHERE u.Id = @Id AND u.Ativo = 1 AND e.Ativa = 1;
            """;
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@Id", SqlDbType.Int).Value = usuarioId;
        await using var leitor = await comando.ExecuteReaderAsync(token);
        if (!await leitor.ReadAsync(token))
            return null;

        return new DadosSessao(leitor.GetString(0), leitor.GetInt32(1), leitor.GetString(2),
            leitor.GetString(3), leitor.GetInt32(4), leitor.GetInt32(5), leitor.GetString(6), leitor.GetBoolean(7));
    }

    public async Task<int> AtualizarPerfilAsync(UsuarioAcesso usuario, string nome, string email, string? novoHash,
        CancellationToken token)
    {
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand("dbo.PortalAtualizarPerfil", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuario.Id;
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = usuario.EmpresaId;
        comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = nome;
        comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email;
        comando.Parameters.Add("@EmailAnterior", SqlDbType.NVarChar, 254).Value = usuario.Email;
        comando.Parameters.Add("@SenhaHashAnterior", SqlDbType.NVarChar, 500).Value = usuario.SenhaHash;
        comando.Parameters.Add("@NovoHash", SqlDbType.NVarChar, 500).Value = (object?)novoHash ?? DBNull.Value;
        var resultado = await comando.ExecuteScalarAsync(token);
        return resultado is int versao
            ? versao
            : throw new InvalidOperationException("O banco não retornou a versão da sessão.");
    }

    public async Task<List<UsuarioEquipe>> ListarUsuariosEmpresaAsync(int empresaId, CancellationToken token)
    {
        const string sql = """
            SELECT u.Nome, u.Email
            FROM dbo.Usuarios u JOIN dbo.Empresas e ON e.Id=u.EmpresaId
            WHERE u.EmpresaId=@EmpresaId AND e.Tipo='Cliente' AND u.Ativo=1 AND e.Ativa=1
            ORDER BY u.Nome, u.Email;
            """;
        await using var conexao = CriarConexao();
        await conexao.OpenAsync(token);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        await using var leitor = await comando.ExecuteReaderAsync(token);
        var usuarios = new List<UsuarioEquipe>();
        while (await leitor.ReadAsync(token))
        {
            usuarios.Add(new UsuarioEquipe(leitor.GetString(0), leitor.GetString(1)));
        }

        return usuarios;
    }

    public async Task<List<EmpresaVisivel>> ListarClientesAsync(
        int empresaId, bool fornecedora, CancellationToken cancelamento)
    {
        const string sql = """
            SELECT Id, Nome
            FROM dbo.Empresas
            WHERE Tipo = 'Cliente'
                AND ((@Fornecedora = 1 AND FornecedoraId = @EmpresaId)
                     OR (@Fornecedora = 0 AND Id = @EmpresaId))
            ORDER BY Nome;
            """;

        await using var conexao = CriarConexao();
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add("@EmpresaId", SqlDbType.Int).Value = empresaId;
        comando.Parameters.Add("@Fornecedora", SqlDbType.Bit).Value = fornecedora;
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);

        var empresas = new List<EmpresaVisivel>();
        while (await leitor.ReadAsync(cancelamento))
        {
            empresas.Add(new EmpresaVisivel(leitor.GetInt32(0), leitor.GetString(1)));
        }

        return empresas;
    }
}
