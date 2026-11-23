using Taktos.Domain;
using Taktos.Mobile.Controllers;

namespace Taktos.Mobile.Views;

internal sealed class DetalhePage : ContentPage
{
    private readonly ConsultaController controlador;
    private readonly int id;
    private readonly VerticalStackLayout dados = new() { Spacing = 14 };
    private readonly Label aviso = TemaPortal.Texto("", 14);
    private readonly Button atualizar = TemaPortal.Botao("Atualizar", secundario: true);
    private bool ocupada;

    public DetalhePage(ConsultaController controlador, int id)
    {
        this.controlador = controlador;
        this.id = id;
        Title = $"OS {id:D4}";
        NavigationPage.SetHasNavigationBar(this, false);
        atualizar.Clicked += async (_, _) => await CarregarAsync();

        var voltar = TemaPortal.Botao("Voltar às ordens", secundario: true);
        voltar.Clicked += async (_, _) =>
        {
            voltar.IsEnabled = false;
            await Navigation.PopAsync();
        };
        aviso.IsVisible = false;
        Content = TemaPortal.Pagina(TemaPortal.Acoes(voltar, atualizar), aviso, dados);
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
        TemaPortal.MostrarAviso(aviso, "Consultando a ordem…");
        try
        {
            var detalhe = await controlador.DetalharAsync(id);
            dados.Children.Clear();
            dados.Children.Add(CriarResumo(detalhe.Ordem));
            dados.Children.Add(TemaPortal.Titulo("Versões entregues", SemanticHeadingLevel.Level2));

            if (detalhe.Entregas.Count == 0)
                dados.Children.Add(TemaPortal.Cartao(TemaPortal.Texto("Nenhuma versão registrada.", discreto: true)));

            foreach (var entrega in detalhe.Entregas)
                dados.Children.Add(CriarEntrega(entrega));

            TemaPortal.MostrarAviso(aviso, null);
        }
        catch (InvalidOperationException falha) { TemaPortal.MostrarAviso(aviso, falha.Message, falha: true); }
        catch (HttpRequestException) { TemaPortal.MostrarAviso(aviso, "Não foi possível consultar a ordem.", falha: true); }
        catch (TaskCanceledException) { TemaPortal.MostrarAviso(aviso, "A consulta demorou a responder.", falha: true); }
        finally
        {
            atualizar.IsEnabled = true;
            ocupada = false;
        }
    }

    private static Border CriarResumo(OrdemServico ordem) => TemaPortal.Cartao(new VerticalStackLayout
    {
        Spacing = 10,
        Children =
        {
            TemaPortal.Texto($"OS {ordem.Id:D4}", 13, discreto: true, negrito: true),
            TemaPortal.Titulo(ordem.Titulo, SemanticHeadingLevel.Level1),
            TemaPortal.Etiqueta(OrdensPage.Situacao(ordem.Situacao), ordem.Situacao),
            CampoResumo("Empresa e sistema", $"{ordem.Empresa} · {ordem.Sistema}"),
            CampoResumo("Previsão de entrega", OrdensPage.Previsao(ordem.Previsao)),
            CampoResumo("Solicitante", ordem.Solicitante),
            CampoResumo("Responsável", ordem.Responsavel ?? "A definir"),
            CampoResumo("Descrição", ordem.Descricao)
        }
    });

    private static Border CriarEntrega(EntregaOrdem entrega)
    {
        var resultado = entrega.Resultado == "Pendente" ? "Em homologação" : entrega.Resultado;
        var cartao = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                TemaPortal.Titulo($"Versão {entrega.Versao}", SemanticHeadingLevel.Level3),
                TemaPortal.Etiqueta(resultado, entrega.Resultado),
                CampoResumo("GMUD", entrega.Gmud)
            }
        };

        if (!string.IsNullOrEmpty(entrega.Motivo))
            cartao.Children.Add(CampoResumo("Motivo da devolução", entrega.Motivo));

        return TemaPortal.Cartao(cartao);
    }

    private static View CampoResumo(string rotulo, string valor) => new VerticalStackLayout
    {
        Spacing = 2,
        Children = { TemaPortal.Texto(rotulo, 13, discreto: true, negrito: true), TemaPortal.Texto(valor) }
    };
}
