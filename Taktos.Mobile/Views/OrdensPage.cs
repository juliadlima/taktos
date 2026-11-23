using Taktos.Domain;
using Taktos.Mobile.Controllers;

namespace Taktos.Mobile.Views;

internal sealed class OrdensPage : ContentPage
{
    private readonly ConsultaController controlador;
    private readonly VerticalStackLayout lista = new() { Spacing = 12 };
    private readonly Label quantidade = TemaPortal.Texto("", 14, discreto: true, negrito: true);
    private readonly Label aviso = TemaPortal.Texto("", 14);
    private readonly Button atualizar = TemaPortal.Botao("Atualizar", secundario: true);
    private readonly Button sair = TemaPortal.Botao("Sair", secundario: true);
    private bool ocupada;

    public OrdensPage(ConsultaController controlador)
    {
        this.controlador = controlador;
        Title = "Ordens de serviço";
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
        atualizar.Clicked += async (_, _) => await CarregarAsync();
        sair.Clicked += async (_, _) =>
        {
            sair.IsEnabled = false;
            await controlador.SairAsync();
            await Navigation.PopToRootAsync();
        };
        aviso.IsVisible = false;
        quantidade.IsVisible = false;

        var cabecalho = new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                TemaPortal.Titulo("Ordens de serviço", SemanticHeadingLevel.Level1),
                TemaPortal.Texto(controlador.Identidade, 14, discreto: true)
            }
        };
        Content = TemaPortal.Pagina(cabecalho, TemaPortal.Acoes(atualizar, sair), aviso, quantidade, lista);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        if (ocupada) return;
        ocupada = true;
        atualizar.IsEnabled = false;
        TemaPortal.MostrarAviso(aviso, "Consultando ordens…");
        try
        {
            var ordens = await controlador.ListarAsync();
            lista.Children.Clear();
            quantidade.Text = ordens.Count == 1 ? "1 ordem disponível" : $"{ordens.Count} ordens disponíveis";
            quantidade.IsVisible = true;
            TemaPortal.MostrarAviso(aviso, ordens.Count == 0 ? "Nenhuma ordem disponível." : null);

            foreach (var ordem in ordens)
                lista.Children.Add(CriarCartao(ordem));
        }
        catch (InvalidOperationException falha) { TemaPortal.MostrarAviso(aviso, falha.Message, falha: true); }
        catch (HttpRequestException) { TemaPortal.MostrarAviso(aviso, "Não foi possível consultar a API.", falha: true); }
        catch (TaskCanceledException) { TemaPortal.MostrarAviso(aviso, "A consulta demorou a responder.", falha: true); }
        finally
        {
            atualizar.IsEnabled = true;
            ocupada = false;
        }
    }

    private Border CriarCartao(OrdemServico ordem)
    {
        var codigo = $"OS {ordem.Id:D4}";
        var abrir = TemaPortal.Botao("Ver detalhes", secundario: true);
        SemanticProperties.SetDescription(abrir, $"Ver detalhes da {codigo}, {ordem.Titulo}");
        abrir.Clicked += async (_, _) =>
        {
            abrir.IsEnabled = false;
            try { await Navigation.PushAsync(new DetalhePage(controlador, ordem.Id)); }
            finally { abrir.IsEnabled = true; }
        };

        var previsao = string.IsNullOrWhiteSpace(ordem.Previsao)
            ? "Aguardando previsão" : "Previsão " + Previsao(ordem.Previsao);
        return TemaPortal.Cartao(new VerticalStackLayout
        {
            Spacing = 9,
            Children =
            {
                TemaPortal.Texto(codigo, 12, discreto: true, negrito: true),
                TemaPortal.Titulo(ordem.Titulo, SemanticHeadingLevel.Level2),
                TemaPortal.Etiqueta(Situacao(ordem.Situacao), ordem.Situacao),
                TemaPortal.Texto($"{ordem.Empresa} · {ordem.Sistema}", 14, discreto: true),
                TemaPortal.Texto(previsao, 14, negrito: true),
                abrir
            }
        });
    }

    public static string Situacao(string situacao) => situacao switch
    {
        "EmAnalise" => "Em análise", "EmExecucao" => "Em execução",
        "EmHomologacao" => "Em homologação", "Concluida" => "Concluída", _ => situacao
    };

    public static string Previsao(string? valor) => DateOnly.TryParseExact(
        valor, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out var data) ? data.ToString("dd/MM/yyyy") : "Aguardando previsão";
}
