using Microsoft.Maui.Controls.Shapes;

namespace Taktos.Mobile.Views;

internal static class TemaPortal
{
    private static readonly Color Tinta = Color.FromArgb("#17263A");
    private static readonly Color Suave = Color.FromArgb("#53647B");
    private static readonly Color Barra = Color.FromArgb("#142135");
    private static readonly Color Marca = Color.FromArgb("#A4EFDB");
    private static readonly Color Fundo = Color.FromArgb("#F2F5F9");
    private static readonly Color Linha = Color.FromArgb("#DBE3ED");
    private static readonly Color BordaCampo = Color.FromArgb("#66768C");
    private static readonly Color Principal = Color.FromArgb("#115370");
    private static readonly Color Foco = Color.FromArgb("#007D99");
    private static readonly Color Erro = Color.FromArgb("#833231");

    public static View Pagina(params View[] secoes)
    {
        var area = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(20, 20, 20, 28) };
        foreach (var secao in secoes)
            area.Children.Add(secao);

        var simbolo = new Border
        {
            BackgroundColor = Color.FromArgb("#24394D"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(9) },
            WidthRequest = 36,
            HeightRequest = 36,
            Content = new Label
            {
                Text = "T", TextColor = Marca, FontSize = 20, FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            }
        };
        AutomationProperties.SetExcludedWithChildren(simbolo, true);
        var topo = new HorizontalStackLayout
        {
            BackgroundColor = Barra, Padding = new Thickness(20, 10), Spacing = 10,
            Children = { simbolo, new Label
            {
                Text = "Taktos", TextColor = Colors.White, FontSize = 21, FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            } }
        };

        var rolagem = new ScrollView { Content = area };
        Grid.SetRow(rolagem, 1);
        return new Grid
        {
            BackgroundColor = Fundo,
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Star } },
            Children = { topo, rolagem }
        };
    }

    public static Label Texto(string texto, double tamanho = 15, bool discreto = false, bool negrito = false) => new()
    {
        Text = texto,
        TextColor = discreto ? Suave : Tinta,
        FontSize = tamanho,
        FontAttributes = negrito ? FontAttributes.Bold : FontAttributes.None,
        LineBreakMode = LineBreakMode.WordWrap
    };

    public static Label Titulo(string texto, SemanticHeadingLevel nivel)
    {
        var titulo = Texto(texto, nivel == SemanticHeadingLevel.Level1 ? 27 : 18, negrito: true);
        SemanticProperties.SetHeadingLevel(titulo, nivel);
        return titulo;
    }

    public static Border Cartao(View conteudo) => new()
    {
        Content = conteudo,
        BackgroundColor = Colors.White,
        Stroke = new SolidColorBrush(Linha),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(15) },
        Padding = 16
    };

    public static Border Etiqueta(string texto, string estado)
    {
        var cores = estado switch
        {
            "EmAnalise" => ("#E4F0FF", "#245480"),
            "EmExecucao" => ("#EAE8FB", "#51448B"),
            "EmHomologacao" or "Pendente" => ("#F8EDD9", "#724E21"),
            "Concluida" or "Aprovada" => ("#E3F1E9", "#225641"),
            "Devolvida" => ("#FFF1EF", "#833231"),
            _ => ("#E9EDF4", "#3B4D63")
        };

        var rotulo = Texto(texto, 13, negrito: true);
        rotulo.TextColor = Color.FromArgb(cores.Item2);
        return new Border
        {
            Content = rotulo,
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Color.FromArgb(cores.Item1),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(7) },
            Padding = new Thickness(8, 4)
        };
    }

    public static View Campo(string rotulo, Entry entrada)
    {
        SemanticProperties.SetHint(entrada, rotulo);
        entrada.FontSize = 16;
        entrada.TextColor = Tinta;
        entrada.BackgroundColor = Colors.Transparent;
        entrada.MinimumHeightRequest = 48;

        var controle = new Border
        {
            Content = entrada,
            Stroke = new SolidColorBrush(BordaCampo),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(9) },
            Padding = new Thickness(10, 0),
            BackgroundColor = Colors.White
        };

        entrada.Focused += (_, _) => { controle.Stroke = new SolidColorBrush(Foco); controle.StrokeThickness = 2; };
        entrada.Unfocused += (_, _) => { controle.Stroke = new SolidColorBrush(BordaCampo); controle.StrokeThickness = 1; };

        return new VerticalStackLayout { Spacing = 4, Children = { Texto(rotulo, 14, negrito: true), controle } };
    }

    public static Button Botao(string texto, bool secundario = false) => new()
    {
        Text = texto,
        TextColor = secundario ? Tinta : Colors.White,
        BackgroundColor = secundario ? Colors.White : Principal,
        BorderColor = secundario ? BordaCampo : Principal,
        BorderWidth = 1,
        CornerRadius = 10,
        FontAttributes = FontAttributes.Bold,
        FontSize = 14,
        LineBreakMode = LineBreakMode.WordWrap,
        MinimumHeightRequest = 48,
        Padding = new Thickness(12, 8)
    };

    public static Grid Acoes(View primeira, View segunda)
    {
        Grid.SetColumn(segunda, 1);
        return new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Star } },
            Children = { primeira, segunda }
        };
    }

    public static void MostrarAviso(Label aviso, string? texto, bool falha = false)
    {
        aviso.Text = texto ?? "";
        aviso.TextColor = falha ? Erro : Suave;
        aviso.IsVisible = !string.IsNullOrEmpty(texto);
    }
}
