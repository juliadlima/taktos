using System.Net;
using System.Net.Http.Json;
using Taktos.Domain;

namespace Taktos.Mobile.Controllers;

internal sealed class ConsultaController
{
    private HttpClient? http;

    public string Identidade { get; private set; } = "";

    public async Task EntrarAsync(string endereco, string email, string senha)
    {
        if (!Uri.TryCreate(endereco.TrimEnd('/') + "/", UriKind.Absolute, out var servidor) ||
            !string.IsNullOrEmpty(servidor.UserInfo) || !string.IsNullOrEmpty(servidor.Query) ||
            !string.IsNullOrEmpty(servidor.Fragment) || !EnderecoPermitido(servidor))
        {
            throw new InvalidOperationException("Informe o endereço HTTPS da API ou use o endereço local do emulador.");
        }

        var novo = new HttpClient(new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = false
        }) { BaseAddress = servidor, Timeout = TimeSpan.FromSeconds(15) };

        try
        {
            using var resposta = await novo.PostAsJsonAsync("api/acesso/login", new { email, senha });
            if (resposta.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("E-mail ou senha inválidos.");
            if (resposta.StatusCode == HttpStatusCode.Forbidden)
            {
                var erro = resposta.Content.Headers.ContentType?.MediaType == "application/json"
                    ? await resposta.Content.ReadFromJsonAsync<ErroAcesso>()
                    : null;
                throw new InvalidOperationException(erro?.Mensagem ?? "Acesso indisponível. Entre em contato com a fornecedora.");
            }

            resposta.EnsureSuccessStatusCode();
            using var respostaConta = await novo.GetAsync("api/acesso/");
            respostaConta.EnsureSuccessStatusCode();
            var conta = await respostaConta.Content.ReadFromJsonAsync<Conta>() ??
                throw new InvalidOperationException("A API não retornou a conta.");

            http?.Dispose();
            http = novo;
            Identidade = conta.Empresa + " · " + conta.Perfil;
        }
        catch
        {
            novo.Dispose();
            throw;
        }
    }

    public Task<List<OrdemServico>> ListarAsync() => ConsultarAsync<List<OrdemServico>>("api/ordens");
    public Task<DetalheOrdem> DetalharAsync(int id) => ConsultarAsync<DetalheOrdem>($"api/ordens/{id}");

    public async Task SairAsync()
    {
        var atual = http;
        http = null;
        Identidade = "";
        if (atual is null) return;
        using (atual)
        {
            try
            {
                using var resposta = await atual.PostAsJsonAsync("api/acesso/logout", new { });
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
        }
    }

    private async Task<T> ConsultarAsync<T>(string rota) where T : class
    {
        if (http is null) throw new InvalidOperationException("Entre novamente para consultar as ordens.");
        using var resposta = await http.GetAsync(rota);
        if (resposta.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Sua sessão expirou. Saia e entre novamente.");
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<T>() ??
            throw new InvalidOperationException("A API retornou uma consulta vazia.");
    }

    private static bool EnderecoPermitido(Uri endereco)
    {
#if DEBUG
        if (endereco.Scheme == Uri.UriSchemeHttp &&
            endereco.Host is "10.0.2.2" or "127.0.0.1" or "localhost")
            return true;
#endif
        return endereco.Scheme == Uri.UriSchemeHttps;
    }

    private sealed record Conta(string Empresa, string Perfil);
    private sealed record ErroAcesso(string? Mensagem);
}
