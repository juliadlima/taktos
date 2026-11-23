using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Taktos.API.Controllers;

[ApiController]
[Route("health")]
public sealed class SaudeController(IConfiguration configuracao, ILogger<SaudeController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Api() => Ok(new { status = "ok", componente = "api" });

    [HttpGet("db")]
    public async Task<IActionResult> Banco(CancellationToken cancelamento)
    {
        var conexao = configuracao.GetConnectionString("PortalEntregas");
        if (!string.IsNullOrWhiteSpace(conexao))
        {
            try
            {
                await using var banco = new SqlConnection(conexao);
                await banco.OpenAsync(cancelamento);
                await using var comando = new SqlCommand("SELECT 1;", banco) { CommandTimeout = 5 };
                await comando.ExecuteScalarAsync(cancelamento);
                return Ok(new { status = "ok", componente = "sql-server" });
            }
            catch (SqlException erro)
            {
                logger.LogWarning("Falha na conexão com o SQL Server. Código {Codigo}.", erro.Number);
            }
            catch (ArgumentException)
            {
                logger.LogWarning("A string de conexão PortalEntregas está inválida.");
            }
        }

        return StatusCode(503, new { status = "indisponivel", componente = "sql-server" });
    }
}
