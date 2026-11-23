using System.Data;
using Microsoft.Data.SqlClient;
using Taktos.Domain;

namespace Taktos.Infrastructure;

public sealed class RepositorioOrdens(RepositorioAcesso acesso)
{
    public async Task<IReadOnlyList<OrdemServico>> ListarAsync(
        Operador operador, int? id, CancellationToken cancelamento)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand("dbo.PortalListarOrdens", conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add(P("@EmpresaId", SqlDbType.Int, operador.EmpresaId));
        comando.Parameters.Add(P("@Fornecedora", SqlDbType.Bit, operador.Fornecedora));
        comando.Parameters.Add(P("@OrdemId", SqlDbType.Int, id));
        var ordens = new List<OrdemServico>();

        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);
        while (await leitor.ReadAsync(cancelamento))
        {
            ordens.Add(new OrdemServico(
                leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2), leitor.GetString(3),
                leitor.GetString(4), leitor.GetString(5), leitor.GetString(6), Texto(leitor, 7),
                leitor.IsDBNull(8) ? null : leitor.GetInt32(8), Texto(leitor, 9), leitor.GetString(10),
                Texto(leitor, 11), Texto(leitor, 12), DataUtc(leitor, 13), DataUtc(leitor, 14),
                DataUtcOpcional(leitor, 15), leitor.GetInt32(16), leitor.GetInt32(17), leitor.GetInt32(18)));
        }

        return ordens;
    }

    public async Task<DetalheOrdem?> DetalharAsync(Operador operador, int id, CancellationToken cancelamento)
    {
        var ordens = await ListarAsync(operador, id, cancelamento);
        var ordem = ordens.SingleOrDefault();
        if (ordem is null)
        {
            return null;
        }

        const string sql = """
            SELECT m.Tipo, m.Texto, u.Nome, m.CriadaEm
            FROM dbo.MensagensOrdem m JOIN dbo.Usuarios u ON u.Id=m.AutorId
            WHERE m.OrdemId=@Id ORDER BY m.Id;

            SELECT e.Id, e.Versao, e.Gmud, e.LinkGmud, e.ResumoTestes, e.Resultado, e.Motivo,
                e.CriadaEm, u.Nome, e.ValidadaEm, v.Nome
            FROM dbo.Entregas e JOIN dbo.Usuarios u ON u.Id=e.CriadaPor
            LEFT JOIN dbo.Usuarios v ON v.Id=e.ValidadaPor
            WHERE e.OrdemId=@Id ORDER BY e.Id DESC;

            SELECT h.Evento, u.Nome, h.CriadoEm
            FROM dbo.HistoricoOrdem h LEFT JOIN dbo.Usuarios u ON u.Id=h.AutorId
            WHERE h.OrdemId=@Id ORDER BY h.Id;
            """;
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add(P("@Id", SqlDbType.Int, id));
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);

        var mensagens = new List<MensagemOrdem>();
        while (await leitor.ReadAsync(cancelamento))
        {
            mensagens.Add(new MensagemOrdem(leitor.GetString(0), leitor.GetString(1),
                leitor.GetString(2), DataUtc(leitor, 3)));
        }

        await leitor.NextResultAsync(cancelamento);
        var entregas = new List<EntregaOrdem>();
        while (await leitor.ReadAsync(cancelamento))
        {
            entregas.Add(new EntregaOrdem(leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2),
                Texto(leitor, 3), Texto(leitor, 4), leitor.GetString(5), Texto(leitor, 6),
                DataUtc(leitor, 7), leitor.GetString(8), DataUtcOpcional(leitor, 9), Texto(leitor, 10)));
        }

        await leitor.NextResultAsync(cancelamento);
        var historico = new List<EventoOrdem>();
        while (await leitor.ReadAsync(cancelamento))
        {
            historico.Add(new EventoOrdem(leitor.GetString(0), Texto(leitor, 1), DataUtc(leitor, 2)));
        }

        return new DetalheOrdem(ordem, mensagens, entregas, historico);
    }

    public async Task<List<OpcaoOrdem>> ListarOpcoesAsync(Operador operador, CancellationToken cancelamento)
    {
        var sql = operador.Fornecedora
            ? "SELECT Id, Nome FROM dbo.Usuarios WHERE EmpresaId=@EmpresaId AND Ativo=1 ORDER BY Nome;"
            : "SELECT Id, Nome FROM dbo.Sistemas WHERE EmpresaId=@EmpresaId ORDER BY Nome;";

        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand(sql, conexao);
        comando.Parameters.Add(P("@EmpresaId", SqlDbType.Int, operador.EmpresaId));
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);
        var opcoes = new List<OpcaoOrdem>();
        while (await leitor.ReadAsync(cancelamento))
        {
            opcoes.Add(new OpcaoOrdem(leitor.GetInt32(0), leitor.GetString(1)));
        }

        return opcoes;
    }

    public async Task<int?> ExecutarAsync(
        string procedimento, Operador operador, int? ordemId, SqlParameter[] dados, CancellationToken cancelamento)
    {
        await using var conexao = acesso.CriarConexao();
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand(procedimento, conexao)
        {
            CommandType = CommandType.StoredProcedure
        };
        comando.Parameters.Add(P("@EmpresaId", SqlDbType.Int, operador.EmpresaId));
        comando.Parameters.Add(P("@UsuarioId", SqlDbType.Int, operador.UsuarioId));
        if (ordemId is int id)
        {
            comando.Parameters.Add(P("@OrdemId", SqlDbType.Int, id));
        }

        comando.Parameters.AddRange(dados);
        return await comando.ExecuteScalarAsync(cancelamento) as int?;
    }

    public static SqlParameter P(string nome, SqlDbType tipo, object? valor, int tamanho = 0) =>
        new(nome, tipo, tamanho) { Value = valor ?? DBNull.Value };

    private static string? Texto(SqlDataReader leitor, int coluna) =>
        leitor.IsDBNull(coluna) ? null : leitor.GetString(coluna);

    private static DateTime DataUtc(SqlDataReader leitor, int coluna) =>
        DateTime.SpecifyKind(leitor.GetDateTime(coluna), DateTimeKind.Utc);

    private static DateTime? DataUtcOpcional(SqlDataReader leitor, int coluna) =>
        leitor.IsDBNull(coluna) ? null : DataUtc(leitor, coluna);
}
