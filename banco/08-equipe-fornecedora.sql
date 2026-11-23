USE [PortalEntregas];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.Empresas', N'AdministradorId') IS NULL
BEGIN
    ALTER TABLE dbo.Empresas ADD AdministradorId int NULL,
        CONSTRAINT FK_Empresas_Administrador FOREIGN KEY (AdministradorId) REFERENCES dbo.Usuarios(Id),
        CONSTRAINT CK_Empresas_Administrador CHECK (AdministradorId IS NULL OR Tipo = 'Fornecedora');
END;
IF COL_LENGTH(N'dbo.Empresas', N'NomePerfil') IS NULL
    ALTER TABLE dbo.Empresas ADD NomePerfil nvarchar(120) NULL;
COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.PortalConfigurarAdministradorDemonstracao
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE e SET AdministradorId = u.Id
    FROM dbo.Empresas e JOIN dbo.Usuarios u ON u.EmpresaId = e.Id
    WHERE e.Codigo = 'demo-fornecedora' AND e.Tipo = 'Fornecedora' AND e.AdministradorId IS NULL
        AND u.Email = N'fornecedora@portal.test';
END;
GO

EXEC dbo.PortalConfigurarAdministradorDemonstracao;
GO

CREATE OR ALTER PROCEDURE dbo.PortalAtualizarIdentidadeFornecedora
    @EmpresaId int,
    @AdministradorId int,
    @NomeEmpresa nvarchar(120),
    @NomePerfil nvarchar(120)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE e SET Nome = @NomeEmpresa, NomePerfil = @NomePerfil
    FROM dbo.Empresas e JOIN dbo.Usuarios u ON u.Id = e.AdministradorId AND u.EmpresaId = e.Id
    WHERE e.Id = @EmpresaId AND e.Tipo = 'Fornecedora' AND e.Ativa = 1
        AND u.Id = @AdministradorId AND u.Ativo = 1;

    IF @@ROWCOUNT = 0
        THROW 50002, N'Administração da fornecedora indisponível.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalCriarUsuarioFornecedora
    @EmpresaId int,
    @AdministradorId int,
    @Nome nvarchar(120),
    @Email nvarchar(254),
    @SenhaHash nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas e WITH (UPDLOCK, HOLDLOCK)
                   JOIN dbo.Usuarios u ON u.Id = e.AdministradorId AND u.EmpresaId = e.Id
                   WHERE e.Id = @EmpresaId AND e.Tipo = 'Fornecedora' AND e.Ativa = 1
                     AND u.Id = @AdministradorId AND u.Ativo = 1)
        THROW 50002, N'Administração da fornecedora indisponível.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email)
        THROW 50003, N'O e-mail informado já está em uso.', 1;
    IF EXISTS (SELECT 1 FROM dbo.SolicitacoesCadastro WITH (UPDLOCK, HOLDLOCK)
               WHERE Email = @Email AND Situacao = 'Pendente')
        THROW 50003, N'Já existe uma solicitação aguardando aprovação para esse e-mail.', 1;

    INSERT dbo.Usuarios (EmpresaId, Nome, Email, SenhaHash)
    VALUES (@EmpresaId, @Nome, @Email, @SenhaHash);
    DECLARE @Id int = CONVERT(int, SCOPE_IDENTITY());
    COMMIT TRANSACTION;
    SELECT @Id AS Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalAtualizarUsuarioFornecedora
    @EmpresaId int,
    @AdministradorId int,
    @UsuarioId int,
    @Nome nvarchar(120) = NULL,
    @Email nvarchar(254) = NULL,
    @NovoHash nvarchar(500) = NULL,
    @Ativo bit = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas e WITH (UPDLOCK, HOLDLOCK)
                   JOIN dbo.Usuarios u ON u.Id = e.AdministradorId AND u.EmpresaId = e.Id
                   WHERE e.Id = @EmpresaId AND e.Tipo = 'Fornecedora' AND e.Ativa = 1
                     AND u.Id = @AdministradorId AND u.Ativo = 1)
        THROW 50002, N'Administração da fornecedora indisponível.', 1;

    IF @UsuarioId = @AdministradorId
        THROW 50003, N'Use Meu perfil para editar os dados da conta administradora.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Id = @UsuarioId AND EmpresaId = @EmpresaId)
        THROW 50002, N'Usuário indisponível para esta fornecedora.', 1;

    IF @Nome IS NOT NULL AND @Email IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
                   WHERE Email = @Email AND Id <> @UsuarioId)
            THROW 50003, N'O e-mail informado já está em uso.', 1;
        IF EXISTS (SELECT 1 FROM dbo.SolicitacoesCadastro WITH (UPDLOCK, HOLDLOCK)
                   WHERE Email = @Email AND Situacao = 'Pendente')
            THROW 50003, N'Já existe uma solicitação aguardando aprovação para esse e-mail.', 1;

        UPDATE dbo.Usuarios
        SET Nome = @Nome, Email = @Email, SenhaHash = COALESCE(@NovoHash, SenhaHash),
            VersaoSessao = VersaoSessao + CASE WHEN Email <> @Email OR @NovoHash IS NOT NULL THEN 1 ELSE 0 END
        WHERE Id = @UsuarioId AND EmpresaId = @EmpresaId;
    END;

    IF @Ativo IS NOT NULL
        UPDATE dbo.Usuarios SET Ativo = @Ativo, VersaoSessao = VersaoSessao + 1
        WHERE Id = @UsuarioId AND EmpresaId = @EmpresaId AND Ativo <> @Ativo;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalTratarOrdem
    @OrdemId int, @EmpresaId int, @UsuarioId int, @Situacao varchar(16),
    @Prioridade varchar(5), @ResponsavelId int, @Previsao date
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @Atual varchar(16);
    DECLARE @ResponsavelAtual int;
    SELECT @Atual=o.Situacao, @ResponsavelAtual=o.ResponsavelId FROM dbo.OrdensServico o WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.Empresas e ON e.Id=o.EmpresaId
        WHERE o.Id=@OrdemId AND e.FornecedoraId=@EmpresaId;
    IF @Atual IS NULL THROW 50002, N'Ordem de serviço indisponível.', 1;
    IF @Atual NOT IN ('Aberta','EmAnalise','EmExecucao') OR NOT (
        @Atual=@Situacao OR (@Atual='Aberta' AND @Situacao='EmAnalise')
        OR (@Atual='EmAnalise' AND @Situacao='EmExecucao'))
        THROW 50003, N'A situação selecionada não está disponível nesta etapa.', 1;
    IF @ResponsavelId IS NOT NULL AND NOT EXISTS
        (SELECT 1 FROM dbo.Usuarios WHERE Id=@ResponsavelId AND EmpresaId=@EmpresaId
            AND (Ativo = 1 OR Id = @ResponsavelAtual))
        THROW 50004, N'Selecione um responsável da fornecedora.', 1;
    IF @Situacao='EmExecucao' AND (@ResponsavelId IS NULL OR @Prioridade IS NULL)
        THROW 50004, N'Defina o responsável e a prioridade antes de iniciar a execução.', 1;
    EXEC sys.sp_set_session_context @key=N'portal_usuario', @value=@UsuarioId;
    UPDATE dbo.OrdensServico SET Situacao=@Situacao, Prioridade=@Prioridade,
        ResponsavelId=@ResponsavelId, PrevisaoEntrega=@Previsao,
        PrimeiraPrevisao=COALESCE(PrimeiraPrevisao,@Previsao), AtualizadaEm=SYSUTCDATETIME() WHERE Id=@OrdemId;
    COMMIT TRANSACTION;
END;
GO
