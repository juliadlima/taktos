using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Taktos.Domain;
using Taktos.Infrastructure;

namespace Taktos.API.Controllers;

[ApiController]
[Route("api/equipe")]
[Authorize(Policy = SessaoPortal.PoliticaAdminFornecedor)]
public sealed class EquipeController(RepositorioEquipe repositorio, IPasswordHasher<string> senhas) : ControllerBase
{
    [HttpGet("usuarios")]
    public async Task<IResult> ListarAsync(CancellationToken token)
    {
        var acesso = Identificar();
        if (acesso is null)
            return Results.Unauthorized();
        var admin = acesso.Value;

        return Results.Ok(await repositorio.ListarAsync(admin.EmpresaId, admin.UsuarioId, token));
    }

    [HttpPost("usuarios")]
    public async Task<IResult> CriarAsync(NovoUsuario entrada, CancellationToken token)
    {
        var acesso = Identificar();
        if (acesso is null)
            return Results.Unauthorized();
        var admin = acesso.Value;

        var nome = RegrasCadastro.NomeValido(entrada.Nome);
        var email = RegrasCadastro.EmailValido(entrada.Email);
        if (nome is null || email is null)
            return Results.BadRequest(new { mensagem = "Informe um nome e um e-mail válido." });
        if (!RegrasCadastro.SenhaValida(entrada.Senha))
            return Results.BadRequest(new { mensagem = "Use de 12 a 128 caracteres na senha inicial." });

        var id = await repositorio.CriarAsync(admin.EmpresaId, admin.UsuarioId, nome, email,
            senhas.HashPassword(email, entrada.Senha!), token);
        return Results.Json(new { id }, statusCode: StatusCodes.Status201Created);
    }

    [HttpPut("usuarios/{id:int}")]
    public async Task<IResult> EditarAsync(int id, AlteracaoUsuario entrada, CancellationToken token)
    {
        var acesso = Identificar();
        if (acesso is null)
            return Results.Unauthorized();
        var admin = acesso.Value;

        var nome = RegrasCadastro.NomeValido(entrada.Nome);
        var email = RegrasCadastro.EmailValido(entrada.Email);
        if (nome is null || email is null)
            return Results.BadRequest(new { mensagem = "Informe um nome e um e-mail válido." });
        if (!string.IsNullOrEmpty(entrada.SenhaNova) && !RegrasCadastro.SenhaValida(entrada.SenhaNova))
            return Results.BadRequest(new { mensagem = "Use de 12 a 128 caracteres na nova senha." });

        var hash = string.IsNullOrEmpty(entrada.SenhaNova) ? null : senhas.HashPassword(email, entrada.SenhaNova);
        await repositorio.AtualizarAsync(admin.EmpresaId, admin.UsuarioId, id, nome, email, hash, null, token);
        return Results.NoContent();
    }

    [HttpPut("usuarios/{id:int}/situacao")]
    public async Task<IResult> AlterarSituacaoAsync(int id, SituacaoUsuario entrada, CancellationToken token)
    {
        var acesso = Identificar();
        if (acesso is null)
            return Results.Unauthorized();
        var admin = acesso.Value;
        if (entrada.Ativo is not bool ativo)
            return Results.BadRequest(new { mensagem = "Informe a situação da conta." });

        await repositorio.AtualizarAsync(admin.EmpresaId, admin.UsuarioId, id, null, null, null, ativo, token);
        return Results.NoContent();
    }

    [HttpPut("identidade")]
    public async Task<IResult> AtualizarIdentidadeAsync(IdentidadeFornecedora entrada, CancellationToken token)
    {
        var acesso = Identificar();
        if (acesso is null)
            return Results.Unauthorized();
        var admin = acesso.Value;

        var empresa = RegrasCadastro.NomeValido(entrada.Empresa);
        var perfil = RegrasCadastro.NomeValido(entrada.Perfil);
        if (empresa is null || perfil is null)
            return Results.BadRequest(new { mensagem = "Informe o nome da fornecedora e o nome do perfil." });

        await repositorio.AtualizarIdentidadeAsync(admin.EmpresaId, admin.UsuarioId, empresa, perfil, token);
        return Results.Ok(new { empresa, perfil });
    }

    private (int EmpresaId, int UsuarioId)? Identificar()
    {
        if (int.TryParse(User.FindFirstValue("empresa_id"), out var empresaId) &&
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            return (empresaId, usuarioId);
        return null;
    }

    public sealed record NovoUsuario(string? Nome, string? Email, string? Senha);
    public sealed record AlteracaoUsuario(string? Nome, string? Email, string? SenhaNova);
    public sealed record SituacaoUsuario(bool? Ativo);
    public sealed record IdentidadeFornecedora(string? Empresa, string? Perfil);
}
