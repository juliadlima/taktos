using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taktos.Domain;
using Taktos.Infrastructure;

namespace Taktos.API.Controllers;

[ApiController]
[Route("api/indicadores")]
[Authorize(Roles = "Cliente,Fornecedora")]
public sealed class IndicadoresController(RepositorioIndicadores repositorio) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListarAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var empresaId))
            return Results.Unauthorized();

        return Results.Ok(await repositorio.ListarAsync(empresaId, token));
    }

    [HttpPost]
    [Authorize(Roles = "Fornecedora")]
    public Task<IResult> CadastrarAsync(EntradaIndicador entrada, CancellationToken token) =>
        SalvarAsync(null, entrada, token);

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Fornecedora")]
    public Task<IResult> AtualizarAsync(int id, EntradaIndicador entrada, CancellationToken token) =>
        SalvarAsync(id, entrada, token);

    private async Task<IResult> SalvarAsync(int? id, EntradaIndicador entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var empresaId))
            return Results.Unauthorized();

        var nome = RegrasCadastro.NomeValido(entrada.Nome);
        var criterio = entrada.Criterio;
        if (nome is null || criterio is null || !IndicadorOrdem.CriterioAceito(criterio))
            return Results.BadRequest(new { mensagem = "Informe o nome do cartão e o que ele deve contar." });

        if (id is int existente)
            return Results.Ok(await repositorio.AtualizarAsync(empresaId, existente, nome, criterio, token));

        var indicador = await repositorio.CadastrarAsync(empresaId, nome, criterio, token);
        return Results.Json(indicador, statusCode: StatusCodes.Status201Created);
    }

    public sealed record EntradaIndicador(string? Nome, string? Criterio);
}
