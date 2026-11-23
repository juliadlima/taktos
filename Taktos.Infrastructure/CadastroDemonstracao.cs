using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace Taktos.Infrastructure;

public static class CadastroDemonstracao
{
    public static async Task PrepararAsync(RepositorioAcesso repositorio, Func<string, string, string> gerarHash, bool redefinir)
    {
        const string sql = """
            SET NOCOUNT ON;
            DECLARE @UsuarioId int;
            SELECT @UsuarioId = AdministradorId FROM dbo.Empresas
            WHERE Codigo = @CodigoEmpresa AND Tipo = 'Fornecedora';

            IF @UsuarioId IS NULL
                SELECT @UsuarioId = Id FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email;

            IF @UsuarioId IS NULL
            BEGIN
                INSERT dbo.Usuarios (EmpresaId, Nome, Email, SenhaHash)
                SELECT Id, @Nome, @Email, @SenhaHash FROM dbo.Empresas WHERE Codigo = @CodigoEmpresa;
                SELECT CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE -1 END, @Email;
                RETURN;
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios u JOIN dbo.Empresas e ON e.Id = u.EmpresaId
                WHERE u.Id = @UsuarioId AND e.Codigo = @CodigoEmpresa)
            BEGIN
                SELECT -1, @Email;
                RETURN;
            END;

            IF @Redefinir = 1
                UPDATE dbo.Usuarios SET SenhaHash = @SenhaHash WHERE Id = @UsuarioId;
            SELECT CONVERT(int, @Redefinir), Email FROM dbo.Usuarios WHERE Id = @UsuarioId;
            """;

        var contas = new (string Empresa, string Nome, string Email)[]
        {
            ("demo-fornecedora", "Consultor ADM", "fornecedora@portal.test"),
            ("demo-alfa", "TI de testes do Cliente Alfa", "ti.alfa@portal.test"),
            ("demo-beta", "TI de testes do Cliente Beta", "ti.beta@portal.test")
        };

        await using var conexao = repositorio.CriarConexao();
        await conexao.OpenAsync();
        await using var transacao = (SqlTransaction)await conexao.BeginTransactionAsync();
        var criadas = new List<(string Email, string Senha)>();

        foreach (var conta in contas)
        {
            var senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
            await using var comando = new SqlCommand(sql, conexao, transacao);
            comando.Parameters.Add("@Redefinir", SqlDbType.Bit).Value = redefinir;
            comando.Parameters.Add("@CodigoEmpresa", SqlDbType.VarChar, 40).Value = conta.Empresa;
            comando.Parameters.Add("@Nome", SqlDbType.NVarChar, 120).Value = conta.Nome;
            comando.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = conta.Email;
            comando.Parameters.Add("@SenhaHash", SqlDbType.NVarChar, 500).Value =
                gerarHash(conta.Email, senha);

            await using var leitor = await comando.ExecuteReaderAsync();
            var resultado = await leitor.ReadAsync() ? leitor.GetInt32(0) : -1;
            switch (resultado)
            {
                case 1:
                    criadas.Add((leitor.GetString(1), senha));
                    break;
                case 0:
                    break;
                default:
                    throw new InvalidOperationException("Confira os cadastros criados pelo script 02-cadastros-e-acesso.sql.");
            }
        }

        await using (var configurarAdmin = new SqlCommand("dbo.PortalConfigurarAdministradorDemonstracao", conexao, transacao)
        {
            CommandType = CommandType.StoredProcedure
        })
        {
            await configurarAdmin.ExecuteNonQueryAsync();
        }

        await transacao.CommitAsync();

        if (criadas.Count == 0)
        {
            Console.WriteLine("As três contas de demonstração já estão cadastradas.");
            return;
        }

        Console.WriteLine("Contas de demonstração preparadas. Guarde as senhas para os testes.");
        foreach (var conta in criadas)
        {
            Console.WriteLine($"{conta.Email}  {conta.Senha}");
        }
    }
}
