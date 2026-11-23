namespace Taktos.Domain;

public sealed record UsuarioFornecedora(
    int Id, string Nome, string Email, string Empresa, bool Ativo, bool Administrador);
