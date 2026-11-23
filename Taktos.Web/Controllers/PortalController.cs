using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taktos.Web.Models;

namespace Taktos.Web.Controllers;

public sealed class PortalController : Controller
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/")]
    public IActionResult Inicio() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Ordens))
        : View(new PaginaPortal("Taktos", "Início"));

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/login.html")]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Ordens))
        : View(new PaginaPortal("Taktos", "Acesso"));

    [HttpGet("/cadastro.html")]
    public IActionResult Cadastro() => View(new PaginaPortal("Taktos", "Criar conta"));

    [Authorize(Roles = "Fornecedora")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/empresas.html")]
    public IActionResult Empresas() => View(new PaginaPortal("Taktos", "Empresas"));

    [Authorize(Roles = "Fornecedora")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/aprovacoes.html")]
    public IActionResult Aprovacoes() => View(new PaginaPortal("Taktos", "Aprovações de cadastro"));

    [Authorize(Roles = "Fornecedora")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/indicadores.html")]
    public IActionResult Indicadores() => View(new PaginaPortal("Taktos", "Indicadores"));

    [Authorize(Policy = "AdministradorFornecedora")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/equipe.html")]
    public IActionResult Equipe() => View(new PaginaPortal("Taktos", "Equipe fornecedora"));

    [Authorize(Roles = "Cliente,Fornecedora")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/perfil.html")]
    public IActionResult Perfil() => View(new PaginaPortal("Taktos", "Meu perfil"));

    [Authorize(Roles = "Cliente")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/usuarios.html")]
    public IActionResult Usuarios() => View(new PaginaPortal("Taktos", "Usuários da empresa"));

    [Authorize(Roles = "Cliente,Fornecedora")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/ordens.html")]
    public IActionResult Ordens() => View(new PaginaPortal("Taktos", "Ordens de serviço"));

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/ajuda.html")]
    public IActionResult Ajuda() => View(new PaginaPortal("Taktos", "Ajuda e acessibilidade"));

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("/guia.html")]
    public IActionResult Guia() => View(new PaginaPortal("Taktos", "Guia de uso"));
}
