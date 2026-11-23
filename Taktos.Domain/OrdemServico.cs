namespace Taktos.Domain;

public sealed record Operador(int EmpresaId, int UsuarioId, bool Fornecedora);
public sealed record OpcaoOrdem(int Id, string Nome);
public sealed record OrdemServico(
    int Id, string Empresa, string Sistema, string Tipo, string Titulo, string Descricao,
    string Situacao, string? Prioridade, int? ResponsavelId, string? Responsavel, string Solicitante,
    string? Previsao, string? PrimeiraPrevisao, DateTime CriadaEm, DateTime AtualizadaEm,
    DateTime? PrimeiraEntrega, int VersoesDecididas, int VersoesDevolvidas, int SolicitanteId);
public sealed record MensagemOrdem(string Tipo, string Texto, string Autor, DateTime CriadaEm);
public sealed record EntregaOrdem(
    int Id, string Versao, string Gmud, string? LinkGmud, string? ResumoTestes, string Resultado,
    string? Motivo, DateTime CriadaEm, string Autor, DateTime? ValidadaEm, string? ValidadaPor);
public sealed record EventoOrdem(string Evento, string? Autor, DateTime CriadoEm);
public sealed record DetalheOrdem(
    OrdemServico Ordem, List<MensagemOrdem> Mensagens, List<EntregaOrdem> Entregas, List<EventoOrdem> Historico);

public sealed record UsuarioAcesso(
    int Id, string Nome, string Email, string SenhaHash, int EmpresaId, string Empresa, string Perfil,
    bool Ativo, bool EmpresaAtiva, int VersaoUsuario, int VersaoEmpresa, string PerfilNome, bool Administrador)
{
    public DadosSessao Sessao => new(Nome, EmpresaId, Empresa, Perfil, VersaoUsuario, VersaoEmpresa, PerfilNome, Administrador);
}

public sealed record DadosSessao(
    string Nome, int EmpresaId, string Empresa, string Perfil, int VersaoUsuario, int VersaoEmpresa,
    string PerfilNome, bool Administrador);

public sealed record EmpresaVisivel(int Id, string Nome);
