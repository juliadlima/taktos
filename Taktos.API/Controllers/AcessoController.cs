using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Taktos.Domain;
using Taktos.Infrastructure;

namespace Taktos.API.Controllers;

[ApiController]
[Route("api/acesso")]
[Authorize(Roles = "Cliente,Fornecedora")]
public sealed class AcessoController(RepositorioAcesso repositorio, RepositorioCadastro cadastros,
    IPasswordHasher<string> senhas) : ControllerBase
{
    private const string Esquema = CookieAuthenticationDefaults.AuthenticationScheme;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IResult> EntrarAsync(Credenciais credenciais, CancellationToken token)
    {
        var email = credenciais.Email?.Trim().ToLowerInvariant();
        var senha = credenciais.Senha;
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 ||
            string.IsNullOrEmpty(senha) || senha.Length > 128)
        {
            return Results.BadRequest(new { mensagem = "Informe o e-mail e a senha." });
        }

        var usuario = await repositorio.BuscarUsuarioAsync(email, token);
        if (usuario is null)
        {
            var pendente = await cadastros.BuscarSenhaPendenteAsync(email, token);
            if (pendente is not null && senhas.VerifyHashedPassword(email, pendente, senha) != PasswordVerificationResult.Failed)
            {
                return Results.Json(new { mensagem = "Seu cadastro está aguardando aprovação da fornecedora." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Unauthorized();
        }

        if (senhas.VerifyHashedPassword(usuario.Email, usuario.SenhaHash, senha) == PasswordVerificationResult.Failed)
            return Results.Unauthorized();

        if (!usuario.Ativo || !usuario.EmpresaAtiva)
        {
            var mensagem = usuario.EmpresaAtiva ? "Sua conta está desativada. Entre em contato com a fornecedora." :
                "Sua empresa está desativada. Entre em contato com a fornecedora.";
            return Results.Json(new { mensagem }, statusCode: StatusCodes.Status403Forbidden);
        }

        await AbrirSessaoAsync(usuario);
        return Results.NoContent();
    }

    [HttpGet]
    public async Task<IResult> ConsultarAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var empresaId))
            return Results.Unauthorized();

        var fornecedora = User.IsInRole("Fornecedora");
        var empresas = await repositorio.ListarClientesAsync(empresaId, fornecedora, token);
        return Results.Ok(new
        {
            usuario = User.Identity?.Name, empresa = User.FindFirstValue("empresa_nome"),
            empresaId, fornecedora, perfil = User.FindFirstValue(SessaoPortal.PerfilNome),
            administrador = User.HasClaim(SessaoPortal.Administrador, "true"), empresasVisiveis = empresas
        });
    }

    [HttpGet("perfil")]
    public async Task<IResult> PerfilAsync(CancellationToken token)
    {
        var usuario = await ConsultarUsuarioDaSessaoAsync(token);
        return usuario is null ? Results.Unauthorized() : Results.Ok(ProjetarPerfil(usuario));
    }

    [HttpPut("perfil")]
    public async Task<IResult> AtualizarAsync(AlteracaoPerfil alteracao, CancellationToken token)
    {
        var usuario = await ConsultarUsuarioDaSessaoAsync(token);
        if (usuario is null)
            return Results.Unauthorized();

        var nome = RegrasCadastro.NomeValido(alteracao.Nome);
        var email = RegrasCadastro.EmailValido(alteracao.Email);
        if (nome is null || email is null)
            return Results.BadRequest(new { mensagem = "Informe um nome e um e-mail válido." });

        var mudaSenha = !string.IsNullOrEmpty(alteracao.SenhaNova);
        if (mudaSenha && !RegrasCadastro.SenhaValida(alteracao.SenhaNova))
            return Results.BadRequest(new { mensagem = "Use de 12 a 128 caracteres na nova senha." });

        if (mudaSenha || !string.Equals(email, usuario.Email, StringComparison.OrdinalIgnoreCase))
        {
            var senhaAtual = alteracao.SenhaAtual;
            if (string.IsNullOrEmpty(senhaAtual) || senhaAtual.Length > 128 ||
                senhas.VerifyHashedPassword(usuario.Email, usuario.SenhaHash, senhaAtual) == PasswordVerificationResult.Failed)
            {
                return Results.BadRequest(new { mensagem = "Confirme sua senha atual para alterar o e-mail ou a senha." });
            }
        }

        var novoHash = mudaSenha ? senhas.HashPassword(email, alteracao.SenhaNova!) : null;
        var versao = await repositorio.AtualizarPerfilAsync(usuario, nome, email, novoHash, token);
        var atualizado = usuario with { Nome = nome, Email = email, VersaoUsuario = versao };
        await AbrirSessaoAsync(atualizado);
        return Results.Ok(ProjetarPerfil(atualizado));
    }

    [HttpGet("usuarios")]
    [Authorize(Roles = "Cliente")]
    public async Task<IResult> UsuariosAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue("empresa_id"), out var empresaId))
            return Results.Unauthorized();

        var usuarios = await repositorio.ListarUsuariosEmpresaAsync(empresaId, token);
        return Results.Ok(new { empresa = User.FindFirstValue("empresa_nome"), usuarios });
    }

    [HttpPost("logout")]
    public async Task<IResult> SairAsync()
    {
        if (!Request.HasJsonContentType())
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        await HttpContext.SignOutAsync(Esquema);
        return Results.NoContent();
    }

    private Task AbrirSessaoAsync(UsuarioAcesso usuario) =>
        HttpContext.SignInAsync(Esquema, SessaoPortal.CriarPrincipal(usuario.Id, usuario.Sessao));

    private async Task<UsuarioAcesso?> ConsultarUsuarioDaSessaoAsync(CancellationToken token)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !int.TryParse(User.FindFirstValue("empresa_id"), out var empresaId))
            return null;

        var usuario = await repositorio.BuscarUsuarioPorIdAsync(id, token);
        return usuario is { Ativo: true, EmpresaAtiva: true } && usuario.EmpresaId == empresaId ? usuario : null;
    }

    private static object ProjetarPerfil(UsuarioAcesso usuario) => new
    {
        usuario.Nome, usuario.Email, usuario.Empresa, perfil = usuario.PerfilNome
    };

    public sealed record Credenciais(string? Email, string? Senha);
    public sealed record AlteracaoPerfil(string? Nome, string? Email, string? SenhaAtual, string? SenhaNova);
}
