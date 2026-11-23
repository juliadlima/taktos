USE [master];
GO
IF SUSER_ID(N'taktos_app') IS NULL
    CREATE LOGIN [taktos_app] WITH PASSWORD = '$(SenhaPortal)', DEFAULT_DATABASE = [PortalEntregas];
ELSE
    ALTER LOGIN [taktos_app] WITH PASSWORD = '$(SenhaPortal)';
GO
USE [PortalEntregas];
GO
IF USER_ID(N'taktos_app') IS NULL
    CREATE USER [taktos_app] FOR LOGIN [taktos_app];
GRANT SELECT ON dbo.Empresas TO [taktos_app];
GRANT SELECT, INSERT ON dbo.Usuarios TO [taktos_app];
GRANT UPDATE ON OBJECT::dbo.Usuarios (SenhaHash) TO [taktos_app];
GRANT SELECT ON dbo.Sistemas TO [taktos_app];
GRANT SELECT ON dbo.MensagensOrdem TO [taktos_app];
GRANT SELECT ON dbo.Entregas TO [taktos_app];
GRANT SELECT ON dbo.HistoricoOrdem TO [taktos_app];
GRANT EXECUTE ON dbo.PortalListarOrdens TO [taktos_app];
GRANT EXECUTE ON dbo.PortalAbrirOrdem TO [taktos_app];
GRANT EXECUTE ON dbo.PortalTratarOrdem TO [taktos_app];
GRANT EXECUTE ON dbo.PortalEnviarMensagem TO [taktos_app];
GRANT EXECUTE ON dbo.PortalRegistrarEntrega TO [taktos_app];
GRANT EXECUTE ON dbo.PortalHomologar TO [taktos_app];
GRANT SELECT ON dbo.SolicitacoesCadastro TO [taktos_app];
GRANT EXECUTE ON dbo.PortalSolicitarCadastro TO [taktos_app];
GRANT EXECUTE ON dbo.PortalDecidirCadastro TO [taktos_app];
GRANT EXECUTE ON dbo.PortalAtualizarPerfil TO [taktos_app];
GRANT EXECUTE ON dbo.PortalCadastrarEmpresa TO [taktos_app];
GRANT EXECUTE ON dbo.PortalCadastrarSistema TO [taktos_app];
GRANT EXECUTE ON dbo.PortalAtualizarEmpresa TO [taktos_app];
GRANT EXECUTE ON dbo.PortalCriarContaCliente TO [taktos_app];
GRANT EXECUTE ON dbo.PortalAtualizarContaCliente TO [taktos_app];
GRANT EXECUTE ON dbo.PortalConfigurarAdministradorDemonstracao TO [taktos_app];
GRANT EXECUTE ON dbo.PortalAtualizarIdentidadeFornecedora TO [taktos_app];
GRANT EXECUTE ON dbo.PortalCriarUsuarioFornecedora TO [taktos_app];
GRANT EXECUTE ON dbo.PortalAtualizarUsuarioFornecedora TO [taktos_app];
GRANT SELECT ON dbo.IndicadoresOrdem TO [taktos_app];
GRANT EXECUTE ON dbo.PortalCadastrarIndicador TO [taktos_app];
GRANT SELECT ON dbo.IndicadoresPadraoOrdem TO [taktos_app];
GRANT EXECUTE ON dbo.PortalAtualizarIndicador TO [taktos_app];
GRANT SELECT ON dbo.RelatosInclusao TO [taktos_app];
GRANT EXECUTE ON dbo.PortalRegistrarRelato TO [taktos_app];
GRANT EXECUTE ON dbo.PortalTratarRelato TO [taktos_app];
GO
