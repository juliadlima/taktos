using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Taktos.API;
using Taktos.API.Controllers;
using Taktos.Infrastructure;
using Taktos.Web.Controllers;

var redefinirContas = args is ["preparar-contas", "--redefinir"];
var prepararContas = redefinirContas || args is ["preparar-contas"];
var builder = WebApplication.CreateBuilder(prepararContas ? Array.Empty<string>() : args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddSingleton(new RepositorioAcesso(builder.Configuration.GetConnectionString("PortalEntregas")));
builder.Services.AddSingleton<RepositorioOrdens>();
builder.Services.AddSingleton<RepositorioCadastro>();
builder.Services.AddSingleton<RepositorioEmpresas>();
builder.Services.AddSingleton<RepositorioEquipe>();
builder.Services.AddSingleton<RepositorioIndicadores>();
builder.Services.AddSingleton<RepositorioRelatos>();
builder.Services.AddSingleton<IPasswordHasher<string>, PasswordHasher<string>>();
builder.Services.AddProblemDetails();
builder.Services.AddControllersWithViews(opcoes => opcoes.Filters.Add<FalhasSqlFilter>())
    .AddApplicationPart(typeof(PortalController).Assembly);

var protecao = builder.Services.AddDataProtection().SetApplicationName("Taktos");
var pastaChaves = builder.Configuration["Taktos:Chaves"];
if (!string.IsNullOrWhiteSpace(pastaChaves))
    protecao.PersistKeysToFileSystem(new DirectoryInfo(pastaChaves));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.Cookie.Name = "Taktos.Acesso";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Strict;
        opcoes.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(4);
        opcoes.Events.OnValidatePrincipal = SessaoPortal.ValidarAsync;
        opcoes.Events.OnRedirectToLogin = contexto =>
        {
            if (contexto.Request.Path.StartsWithSegments("/api"))
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else
                contexto.Response.Redirect("/login.html");

            return Task.CompletedTask;
        };
        opcoes.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(opcoes => opcoes.AddPolicy(SessaoPortal.PoliticaAdminFornecedor,
    politica => politica.RequireRole("Fornecedora").RequireClaim(SessaoPortal.Administrador, "true")));

var app = builder.Build();

if (prepararContas)
{
    if (!app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("O preparo das contas fictícias usa o ambiente Development.");
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        var repositorio = app.Services.GetRequiredService<RepositorioAcesso>();
        var senhas = app.Services.GetRequiredService<IPasswordHasher<string>>();
        await CadastroDemonstracao.PrepararAsync(repositorio, senhas.HashPassword, redefinirContas);
    }
    catch (SqlException excecao)
    {
        Console.Error.WriteLine($"Falha no SQL Server (código {excecao.Number}). Confira a conexão e os scripts do banco.");
        Environment.ExitCode = 1;
    }
    catch (InvalidOperationException excecao)
    {
        Console.Error.WriteLine(excecao.Message);
        Environment.ExitCode = 1;
    }

    return;
}

app.UseExceptionHandler();
app.UseStaticFiles();
app.Use(async (contexto, proximo) =>
{
    if (contexto.Request.Path.StartsWithSegments("/api"))
        contexto.Response.Headers.CacheControl = "no-store";

    await proximo(contexto);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
