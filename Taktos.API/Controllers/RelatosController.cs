using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taktos.Infrastructure;

namespace Taktos.API.Controllers;

[ApiController]
[Route("api/relatos")]
[Authorize(Roles = "Cliente,Fornecedora")]
public sealed class RelatosController(RepositorioRelatos repositorio) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListarAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            return Results.Unauthorized();
        return Results.Ok(await repositorio.ListarAsync(usuarioId, token));
    }

    [HttpPost]
    public async Task<IResult> RegistrarAsync(EntradaRelato entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            return Results.Unauthorized();

        var descricao = entrada.Descricao?.Trim();
        if (entrada.Categoria is not ("Discriminacao" or "Acessibilidade"))
            return Results.BadRequest(new { campo = "categoria", mensagem = "Escolha o tipo de relato." });
        if (string.IsNullOrEmpty(descricao) || descricao.Length > 2000)
            return Results.BadRequest(new { campo = "descricao", mensagem = "Descreva o ocorrido em até 2.000 caracteres." });
        if (entrada.OrdemId is <= 0)
            return Results.BadRequest(new { campo = "ordemId", mensagem = "Informe um número de OS válido ou deixe o campo em branco." });

        var id = await repositorio.RegistrarAsync(usuarioId, entrada.Categoria, descricao, entrada.OrdemId, token);
        return Results.Json(new { id }, statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("{id:int}/situacao")]
    [Authorize(Policy = SessaoPortal.PoliticaAdminFornecedor)]
    public async Task<IResult> TratarAsync(int id, TratamentoRelato entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            return Results.Unauthorized();

        var resposta = entrada.Resposta?.Trim();
        if (entrada.Situacao is not ("EmAnalise" or "Respondido"))
            return Results.BadRequest(new { mensagem = "Selecione uma situação válida para o relato." });
        if (entrada.Situacao == "Respondido" && (string.IsNullOrEmpty(resposta) || resposta.Length > 2000))
            return Results.BadRequest(new { campo = "resposta", mensagem = "Informe a resposta em até 2.000 caracteres." });

        await repositorio.TratarAsync(usuarioId, id, entrada.Situacao,
            entrada.Situacao == "Respondido" ? resposta : null, token);
        return Results.NoContent();
    }

    public sealed record EntradaRelato(string? Categoria, string? Descricao, int? OrdemId);
    public sealed record TratamentoRelato(string? Situacao, string? Resposta);
}
