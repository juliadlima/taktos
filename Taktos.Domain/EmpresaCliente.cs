namespace Taktos.Domain;

public sealed record EmpresaCliente(int Id, string Nome, string Codigo, bool Ativa, List<OpcaoOrdem> Sistemas);
