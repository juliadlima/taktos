using Taktos.Mobile.Controllers;

namespace Taktos.Mobile.Views;

internal sealed class LoginPage : ContentPage
{
    private readonly Entry servidor = new() { Keyboard = Keyboard.Url };
    private readonly Entry email = new() { Keyboard = Keyboard.Email };
    private readonly Entry senha = new() { IsPassword = true };
    private readonly Button entrar = TemaPortal.Botao("Entrar");
    private readonly Label aviso = TemaPortal.Texto("", 14);
    private readonly ConsultaController controlador;

    public LoginPage(ConsultaController controlador)
    {
        this.controlador = controlador;
        Title = "Acesso ao Taktos";
        NavigationPage.SetHasNavigationBar(this, false);
#if DEBUG
        servidor.Text = "http://10.0.2.2:5097";
#endif
        entrar.Clicked += Entrar;
        aviso.IsVisible = false;

        var foto = new Image
        {
            Source = ImageSource.FromResource("Taktos.Mobile.terminal_ao_anoitecer.webp", typeof(LoginPage).Assembly),
            Aspect = Aspect.AspectFill, HeightRequest = 116, HorizontalOptions = LayoutOptions.Fill
        };
        AutomationProperties.SetExcludedWithChildren(foto, true);

        var formulario = TemaPortal.Cartao(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        TemaPortal.Titulo("Acesso ao Taktos", SemanticHeadingLevel.Level1),
                        TemaPortal.Texto("Consulte suas ordens de serviço e as versões entregues.", discreto: true)
                    }
                },
                TemaPortal.Campo("Endereço da API", servidor),
                TemaPortal.Campo("E-mail", email),
                TemaPortal.Campo("Senha", senha),
                aviso,
                entrar
            }
        });
        Content = TemaPortal.Pagina(foto, formulario);
    }

    private async void Entrar(object? sender, EventArgs args)
    {
        var endereco = servidor.Text?.Trim();
        var conta = email.Text?.Trim();
        var chave = senha.Text;
        if (string.IsNullOrEmpty(endereco) || string.IsNullOrEmpty(conta) || string.IsNullOrEmpty(chave))
        {
            TemaPortal.MostrarAviso(aviso, "Preencha o endereço, o e-mail e a senha.", falha: true);
            return;
        }

        entrar.IsEnabled = false;
        TemaPortal.MostrarAviso(aviso, "Entrando…");
        try
        {
            await controlador.EntrarAsync(endereco, conta, chave);
            TemaPortal.MostrarAviso(aviso, null);
            await Navigation.PushAsync(new OrdensPage(controlador));
        }
        catch (InvalidOperationException falha) { TemaPortal.MostrarAviso(aviso, falha.Message, falha: true); }
        catch (HttpRequestException) { TemaPortal.MostrarAviso(aviso, "Não foi possível conectar à API. Confira o endereço informado.", falha: true); }
        catch (TaskCanceledException) { TemaPortal.MostrarAviso(aviso, "A API demorou a responder. Tente novamente.", falha: true); }
        finally
        {
            senha.Text = "";
            entrar.IsEnabled = true;
        }
    }
}
