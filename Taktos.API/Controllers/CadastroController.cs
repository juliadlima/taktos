using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Taktos.Domain;
using Taktos.Infrastructure;

namespace Taktos.API.Controllers;

[ApiController]
[Route("api/cadastro")]
[Authorize(Roles = "Fornecedora")]
public sealed class CadastroController(RepositorioCadastro repositorio, IPasswordHasher<string> senhas) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IResult> CadastrarAsync(NovaSolicitacao conta, CancellationToken token)
    {
        var nome = RegrasCadastro.NomeValido(conta.Nome);
        var email = RegrasCadastro.EmailValido(conta.Email);
        var empresa = RegrasCadastro.NomeValido(conta.Empresa);

        if (nome is null || email is null || empresa is null)
            return Results.BadRequest(new { mensagem = "Informe seu nome, e-mail e a empresa cadastrada no Taktos." });
        if (!RegrasCadastro.SenhaValida(conta.Senha))
            return Results.BadRequest(new { mensagem = "Use uma senha de 12 a 128 caracteres." });

        await repositorio.SolicitarAsync(empresa, nome, email, senhas.HashPassword(email, conta.Senha!), token);
        return Results.NoContent();
    }

    [HttpGet("solicitacoes")]
    public async Task<IResult> ListarAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        return Results.Ok(await repositorio.ListarPendentesAsync(fornecedoraId, token));
    }

    [HttpPost("solicitacoes/{id:int}/decisao")]
    public async Task<IResult> DecidirAsync(int id, DecisaoCadastro decisao, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId) ||
            !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            return Results.Unauthorized();
        if (decisao.Aprovar is not bool aprovar)
            return Results.BadRequest(new { mensagem = "Informe se a solicitação será aprovada ou recusada." });

        await repositorio.DecidirAsync(id, fornecedoraId, usuarioId, aprovar, token);
        return Results.NoContent();
    }

    [HttpGet("contas")]
    public async Task<IResult> ListarContasAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        return Results.Ok(await repositorio.ListarContasAsync(fornecedoraId, token));
    }

    [HttpPost("contas")]
    public async Task<IResult> CriarContaAsync(NovaConta entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        var nome = RegrasCadastro.NomeValido(entrada.Nome);
        var email = RegrasCadastro.EmailValido(entrada.Email);
        if (entrada.EmpresaId <= 0 || nome is null || email is null)
            return Results.BadRequest(new { mensagem = "Informe a empresa cliente, o nome e um e-mail válido." });
        if (!RegrasCadastro.SenhaValida(entrada.Senha))
            return Results.BadRequest(new { mensagem = "Use de 12 a 128 caracteres na senha inicial." });

        var id = await repositorio.CriarContaAsync(fornecedoraId, entrada.EmpresaId, nome, email,
            senhas.HashPassword(email, entrada.Senha!), token);
        return Results.Json(new { id }, statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("contas/{id:int}")]
    public async Task<IResult> EditarContaAsync(int id, AlteracaoConta entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();

        var nome = RegrasCadastro.NomeValido(entrada.Nome);
        var email = RegrasCadastro.EmailValido(entrada.Email);
        if (nome is null || email is null)
            return Results.BadRequest(new { mensagem = "Informe um nome e um e-mail válido." });
        if (!string.IsNullOrEmpty(entrada.SenhaNova) && !RegrasCadastro.SenhaValida(entrada.SenhaNova))
            return Results.BadRequest(new { mensagem = "Use de 12 a 128 caracteres na nova senha." });

        var novoHash = string.IsNullOrEmpty(entrada.SenhaNova) ? null : senhas.HashPassword(email, entrada.SenhaNova);
        await repositorio.AtualizarContaAsync(fornecedoraId, id, nome, email, novoHash, null, token);
        return Results.NoContent();
    }

    [HttpPut("contas/{id:int}/situacao")]
    public async Task<IResult> AlterarSituacaoContaAsync(int id, SituacaoConta entrada, CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var fornecedoraId))
            return Results.Unauthorized();
        if (entrada.Ativo is not bool ativo)
            return Results.BadRequest(new { mensagem = "Informe a situação da conta." });

        await repositorio.AtualizarContaAsync(fornecedoraId, id, null, null, null, ativo, token);
        return Results.NoContent();
    }

    public sealed record NovaSolicitacao(string? Nome, string? Email, string? Empresa, string? Senha);
    public sealed record DecisaoCadastro(bool? Aprovar);
    public sealed record NovaConta(int EmpresaId, string? Nome, string? Email, string? Senha);
    public sealed record AlteracaoConta(string? Nome, string? Email, string? SenhaNova);
    public sealed record SituacaoConta(bool? Ativo);
}
