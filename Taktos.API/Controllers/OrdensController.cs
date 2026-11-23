using System.Data;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taktos.Domain;
using Taktos.Infrastructure;
using static Taktos.Infrastructure.RepositorioOrdens;

namespace Taktos.API.Controllers;

[ApiController]
[Route("api/ordens")]
[Authorize(Roles = "Cliente,Fornecedora")]
public sealed class OrdensController(RepositorioOrdens repo) : ControllerBase
{
    [HttpGet]
    public async Task<IResult> ListarAsync(CancellationToken token) =>
        Results.Ok(await repo.ListarAsync(Identificar(), null, token));

    [HttpGet("opcoes")]
    public async Task<IResult> OpcoesAsync(CancellationToken token) =>
        Results.Ok(await repo.ListarOpcoesAsync(Identificar(), token));

    [HttpGet("{id:int}")]
    public async Task<IResult> ConsultarAsync(
        int id, CancellationToken token)
    {
        var detalhe = await repo.DetalharAsync(Identificar(), id, token);
        return detalhe is null
            ? Results.NotFound(new { mensagem = "Ordem de serviço indisponível." })
            : Results.Ok(detalhe);
    }

    [HttpPost]
    [Authorize(Roles = "Cliente")]
    public async Task<IResult> AbrirAsync(
        NovaOrdem entrada, CancellationToken token)
    {
        var titulo = entrada.Titulo?.Trim();
        var descricao = entrada.Descricao?.Trim();
        if (entrada.SistemaId <= 0)
            return Invalido("Selecione o sistema da ordem.", "sistemaId");
        if (!RegrasOrdem.TipoAceito(entrada.Tipo))
            return Invalido("Selecione o tipo de atendimento.", "tipo");
        if (string.IsNullOrEmpty(titulo) || titulo.Length > 150)
            return Invalido("Informe um título com até 150 caracteres.", "titulo");
        if (string.IsNullOrEmpty(descricao) || descricao.Length > 4000)
            return Invalido("Descreva a solicitação em até 4.000 caracteres.", "descricao");

        var id = await repo.ExecutarAsync("dbo.PortalAbrirOrdem", Identificar(), null,
            [P("@SistemaId", SqlDbType.Int, entrada.SistemaId), P("@Tipo", SqlDbType.VarChar, entrada.Tipo, 10),
             P("@Titulo", SqlDbType.NVarChar, titulo, 150), P("@Descricao", SqlDbType.NVarChar, descricao, 4000)], token);
        return Results.Created($"/api/ordens/{id}", new { id });
    }

    [HttpPut("{id:int}/triagem")]
    [Authorize(Roles = "Fornecedora")]
    public async Task<IResult> TriarAsync(
        int id, EntradaTriagem entrada, CancellationToken token)
    {
        if (!RegrasOrdem.SituacaoDeTriagem(entrada.Situacao) ||
            !RegrasOrdem.PrioridadeAceita(entrada.Prioridade) || entrada.ResponsavelId is <= 0)
        {
            return Invalido("Confira a situação, a prioridade e o responsável.");
        }

        DateTime? previsao = null;
        if (!string.IsNullOrWhiteSpace(entrada.Previsao))
        {
            if (!DateOnly.TryParseExact(entrada.Previsao, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var data))
            {
                return Invalido("Informe uma data válida para a previsão.");
            }

            previsao = data.ToDateTime(TimeOnly.MinValue);
        }

        await repo.ExecutarAsync("dbo.PortalTratarOrdem", Identificar(), id,
            [P("@Situacao", SqlDbType.VarChar, entrada.Situacao, 16),
             P("@Prioridade", SqlDbType.VarChar, entrada.Prioridade, 5),
             P("@ResponsavelId", SqlDbType.Int, entrada.ResponsavelId), P("@Previsao", SqlDbType.Date, previsao)], token);
        return Results.NoContent();
    }

    [HttpPost("{id:int}/mensagens")]
    public async Task<IResult> MensagemAsync(
        int id, EntradaMensagem entrada, CancellationToken token)
    {
        var texto = entrada.Texto?.Trim();
        if (entrada.Tipo is not ("Mensagem" or "SolicitacaoVpn") ||
            string.IsNullOrWhiteSpace(texto) || texto.Length > 2000)
        {
            return Invalido("Informe o tipo e o texto da mensagem.");
        }

        var operador = Identificar();
        await repo.ExecutarAsync("dbo.PortalEnviarMensagem", operador, id,
            [P("@Fornecedora", SqlDbType.Bit, operador.Fornecedora),
             P("@Tipo", SqlDbType.VarChar, entrada.Tipo, 16), P("@Texto", SqlDbType.NVarChar, texto, 2000)], token);
        return Results.NoContent();
    }

    [HttpPost("{id:int}/entregas")]
    [Authorize(Roles = "Fornecedora")]
    public async Task<IResult> EntregarAsync(
        int id, EntradaEntrega entrada, CancellationToken token)
    {
        var versao = entrada.Versao?.Trim();
        var gmud = entrada.Gmud?.Trim();
        var link = Opcional(entrada.LinkGmud);
        var testes = Opcional(entrada.ResumoTestes);

        if (string.IsNullOrWhiteSpace(versao) || versao.Length > 60 ||
            string.IsNullOrWhiteSpace(gmud) || gmud.Length > 150 || testes?.Length > 2000)
        {
            return Invalido("Informe a versão e a referência da GMUD dentro dos limites do formulário.");
        }

        if (link is not null && (link.Length > 600 ||
            !Uri.TryCreate(link, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps))
        {
            return Invalido("Use um link HTTPS para a GMUD ou deixe o campo em branco.");
        }

        await repo.ExecutarAsync("dbo.PortalRegistrarEntrega", Identificar(), id,
            [P("@Versao", SqlDbType.NVarChar, versao, 60), P("@Gmud", SqlDbType.NVarChar, gmud, 150),
             P("@LinkGmud", SqlDbType.NVarChar, link, 600), P("@ResumoTestes", SqlDbType.NVarChar, testes, 2000)], token);
        return Results.NoContent();
    }

    [HttpPost("{id:int}/entregas/{entregaId:int}/homologacao")]
    [Authorize(Roles = "Cliente")]
    public async Task<IResult> HomologarAsync(
        int id, int entregaId, EntradaHomologacao entrada, CancellationToken token)
    {
        var motivo = Opcional(entrada.Motivo);
        if (!RegrasOrdem.ResultadoAceito(entrada.Resultado))
            return Invalido("Selecione o resultado da homologação.", "resultado");
        if (motivo?.Length > 2000 || (entrada.Resultado == "Devolvida" && motivo is null))
            return Invalido("Descreva o motivo da devolução em até 2.000 caracteres.", "motivo");

        await repo.ExecutarAsync("dbo.PortalHomologar", Identificar(), id,
            [P("@EntregaId", SqlDbType.Int, entregaId), P("@Resultado", SqlDbType.VarChar, entrada.Resultado, 9),
             P("@Motivo", SqlDbType.NVarChar, motivo, 2000)], token);
        return Results.NoContent();
    }

    private Operador Identificar() =>
        new(int.Parse(User.FindFirstValue("empresa_id")!, CultureInfo.InvariantCulture),
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture),
            User.IsInRole("Fornecedora"));

    private static IResult Invalido(string mensagem, string? campo = null) => Results.BadRequest(new { mensagem, campo });

    private static string? Opcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    public sealed record NovaOrdem(int SistemaId, string? Tipo, string? Titulo, string? Descricao);
    public sealed record EntradaTriagem(string? Situacao, string? Prioridade, int? ResponsavelId, string? Previsao);
    public sealed record EntradaMensagem(string? Tipo, string? Texto);
    public sealed record EntradaEntrega(string? Versao, string? Gmud, string? LinkGmud, string? ResumoTestes);
    public sealed record EntradaHomologacao(string? Resultado, string? Motivo);
}
