using Taktos.Mobile.Controllers;
using Taktos.Mobile.Views;

namespace Taktos.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder().UseMauiApp<App>().Build();
}

public sealed class App : Application
{
    public App()
    {
        UserAppTheme = AppTheme.Light;
        var controlador = new ConsultaController();
        MainPage = new NavigationPage(new LoginPage(controlador));
    }
}
