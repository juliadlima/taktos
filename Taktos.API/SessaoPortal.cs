using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Taktos.Domain;
using Taktos.Infrastructure;

namespace Taktos.API;

internal static class SessaoPortal
{
    public const string PoliticaAdminFornecedor = "AdministradorFornecedora";
    public const string Administrador = "administrador_fornecedora";
    public const string PerfilNome = "perfil_nome";
    private const string VersaoUsuario = "versao_usuario";
    private const string VersaoEmpresa = "versao_empresa";

    public static ClaimsPrincipal CriarPrincipal(int usuarioId, DadosSessao dados)
    {
        Claim[] atributos =
        [
            new(ClaimTypes.NameIdentifier, usuarioId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, dados.Nome),
            new(ClaimTypes.Role, dados.Perfil),
            new("empresa_id", dados.EmpresaId.ToString(CultureInfo.InvariantCulture)),
            new("empresa_nome", dados.Empresa),
            new(PerfilNome, dados.PerfilNome),
            new(Administrador, dados.Administrador ? "true" : "false"),
            new(VersaoUsuario, dados.VersaoUsuario.ToString(CultureInfo.InvariantCulture)),
            new(VersaoEmpresa, dados.VersaoEmpresa.ToString(CultureInfo.InvariantCulture))
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(atributos, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static async Task ValidarAsync(CookieValidatePrincipalContext contexto)
    {
        var principal = contexto.Principal;
        if (principal is not null &&
            int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) &&
            int.TryParse(principal.FindFirstValue("empresa_id"), out var empresaId))
        {
            var repositorio = contexto.HttpContext.RequestServices.GetRequiredService<RepositorioAcesso>();
            var sessao = await repositorio.BuscarSessaoAtivaAsync(id, contexto.HttpContext.RequestAborted);
            // Cookies emitidos antes da migração pertencem à versão inicial.
            int.TryParse(principal.FindFirstValue(VersaoUsuario), out var versaoUsuario);
            int.TryParse(principal.FindFirstValue(VersaoEmpresa), out var versaoEmpresa);

            if (sessao is not null && sessao.EmpresaId == empresaId && principal.IsInRole(sessao.Perfil) &&
                versaoUsuario == sessao.VersaoUsuario && versaoEmpresa == sessao.VersaoEmpresa)
            {
                if (principal.Identity?.Name != sessao.Nome || principal.FindFirstValue("empresa_nome") != sessao.Empresa ||
                    principal.FindFirstValue(PerfilNome) != sessao.PerfilNome ||
                    principal.HasClaim(Administrador, "true") != sessao.Administrador)
                {
                    contexto.ReplacePrincipal(CriarPrincipal(id, sessao));
                    contexto.ShouldRenew = true;
                }

                return;
            }
        }

        contexto.RejectPrincipal();
        await contexto.HttpContext.SignOutAsync(contexto.Scheme.Name);
    }
}
