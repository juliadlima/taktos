using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taktos.Domain;
using Taktos.Infrastructure;

namespace Taktos.API.Controllers;

[ApiController]
[Route("api/empresas")]
[Authorize(Roles = "Fornecedora")]
public sealed class EmpresasController(RepositorioEmpresas repositorio) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListarAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        return Results.Ok(await repositorio.ListarAsync(fornecedoraId, token));
    }

    [HttpPost]
    public async Task<IResult> CadastrarAsync(EntradaNome entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        var nome = RegrasCadastro.NomeValido(entrada.Nome);
        if (nome is null)
            return Results.BadRequest(new { mensagem = "Informe o nome da empresa, com até 120 caracteres." });

        var empresa = await repositorio.CadastrarAsync(fornecedoraId, nome, token);
        return Results.Json(empresa, statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("{id:int}")]
    public async Task<IResult> EditarAsync(int id, EntradaNome entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        var nome = RegrasCadastro.NomeValido(entrada.Nome);
        if (nome is null)
            return Results.BadRequest(new { mensagem = "Informe o nome da empresa, com até 120 caracteres." });

        await repositorio.AtualizarAsync(fornecedoraId, id, nome, null, token);
        return Results.NoContent();
    }

    [HttpPut("{id:int}/situacao")]
    public async Task<IResult> AlterarSituacaoAsync(int id, AlteracaoSituacao entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();
        if (entrada.Ativa is not bool ativa)
            return Results.BadRequest(new { mensagem = "Informe a situação da empresa." });

        await repositorio.AtualizarAsync(fornecedoraId, id, null, ativa, token);
        return Results.NoContent();
    }

    [HttpPost("{id:int}/sistemas")]
    public async Task<IResult> CadastrarSistemaAsync(int id, EntradaNome entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        var nome = entrada.Nome?.Trim();
        if (string.IsNullOrEmpty(nome) || nome.Length > 100)
            return Results.BadRequest(new { mensagem = "Informe o nome do sistema, com até 100 caracteres." });

        var sistema = await repositorio.CadastrarSistemaAsync(fornecedoraId, id, nome, token);
        return Results.Json(sistema, statusCode: StatusCodes.Status201Created);
    }

    public sealed record EntradaNome(string? Nome);
    public sealed record AlteracaoSituacao(bool? Ativa);
}
