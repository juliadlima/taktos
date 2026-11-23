using System.ComponentModel.DataAnnotations;

namespace Taktos.Domain;

public static class RegrasCadastro
{
    public static string? NomeValido(string? nome)
    {
        var valor = nome?.Trim();
        return valor is { Length: > 0 and <= 120 } ? valor : null;
    }

    public static string? EmailValido(string? email)
    {
        var valor = email?.Trim().ToLowerInvariant();
        return valor is { Length: > 0 and <= 254 } && new EmailAddressAttribute().IsValid(valor)
            ? valor : null;
    }

    public static bool SenhaValida(string? senha) =>
        senha is { Length: >= 12 and <= 128 } && !string.IsNullOrWhiteSpace(senha);
}

public sealed record SolicitacaoCadastro(int Id, string Empresa, string Nome, string Email, DateTime SolicitadaEm, bool EmpresaAtiva);
public sealed record UsuarioEquipe(string Nome, string Email);

public sealed record ContaCliente(
    int Id, string Nome, string Email, int EmpresaId, string Empresa, bool Ativo, bool EmpresaAtiva);
