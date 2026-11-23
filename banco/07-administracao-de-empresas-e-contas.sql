USE [PortalEntregas];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.Empresas', N'Ativa') IS NULL
    ALTER TABLE dbo.Empresas ADD Ativa bit NOT NULL CONSTRAINT DF_Empresas_Ativa DEFAULT (1);
IF COL_LENGTH(N'dbo.Empresas', N'VersaoSessao') IS NULL
    ALTER TABLE dbo.Empresas ADD VersaoSessao int NOT NULL CONSTRAINT DF_Empresas_VersaoSessao DEFAULT (0);
IF COL_LENGTH(N'dbo.Usuarios', N'Ativo') IS NULL
    ALTER TABLE dbo.Usuarios ADD Ativo bit NOT NULL CONSTRAINT DF_Usuarios_Ativo DEFAULT (1);
IF COL_LENGTH(N'dbo.Usuarios', N'VersaoSessao') IS NULL
    ALTER TABLE dbo.Usuarios ADD VersaoSessao int NOT NULL CONSTRAINT DF_Usuarios_VersaoSessao DEFAULT (0);
COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.PortalAtualizarEmpresa
    @FornecedoraId int,
    @EmpresaId int,
    @Nome nvarchar(120) = NULL,
    @Ativa bit = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    -- Usa o mesmo bloqueio do cadastro ao validar nomes da carteira.
    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
                   WHERE Id = @FornecedoraId AND Tipo = 'Fornecedora')
        THROW 50002, N'Fornecedora indisponível.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
                   WHERE Id = @EmpresaId AND Tipo = 'Cliente' AND FornecedoraId = @FornecedoraId)
        THROW 50002, N'Empresa indisponível para esta fornecedora.', 1;

    IF @Nome IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Empresas
                   WHERE FornecedoraId = @FornecedoraId AND Tipo = 'Cliente' AND Id <> @EmpresaId
                     AND Nome COLLATE Latin1_General_100_CI_AI = @Nome)
            THROW 50003, N'Já existe uma empresa cliente com esse nome.', 1;

        UPDATE dbo.Empresas SET Nome = @Nome WHERE Id = @EmpresaId;
    END;

    IF @Ativa IS NOT NULL
        UPDATE dbo.Empresas SET Ativa = @Ativa, VersaoSessao = VersaoSessao + 1
        WHERE Id = @EmpresaId AND Ativa <> @Ativa;

    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalCriarContaCliente
    @FornecedoraId int,
    @EmpresaId int,
    @Nome nvarchar(120),
    @Email nvarchar(254),
    @SenhaHash nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @EmpresaAtiva bit;
    SELECT @EmpresaAtiva = Ativa FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = @EmpresaId AND Tipo = 'Cliente' AND FornecedoraId = @FornecedoraId;
    IF @EmpresaAtiva IS NULL THROW 50002, N'Empresa indisponível para esta fornecedora.', 1;
    IF @EmpresaAtiva = 0 THROW 50003, N'Reative a empresa para criar uma conta.', 1;

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

CREATE OR ALTER PROCEDURE dbo.PortalAtualizarContaCliente
    @FornecedoraId int,
    @UsuarioId int,
    @Nome nvarchar(120) = NULL,
    @Email nvarchar(254) = NULL,
    @NovoHash nvarchar(500) = NULL,
    @Ativo bit = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EmpresaId int;
    SELECT @EmpresaId = EmpresaId FROM dbo.Usuarios WHERE Id = @UsuarioId;

    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
                   WHERE Id = @EmpresaId AND Tipo = 'Cliente' AND FornecedoraId = @FornecedoraId)
        THROW 50002, N'Conta indisponível para esta fornecedora.', 1;

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

CREATE OR ALTER PROCEDURE dbo.PortalSolicitarCadastro
    @Empresa nvarchar(120),
    @Nome nvarchar(120),
    @Email nvarchar(254),
    @SenhaHash nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @EmpresaId int;
    SELECT @EmpresaId = Id FROM dbo.Empresas
    WHERE Tipo = 'Cliente' AND Codigo = @Empresa AND FornecedoraId IS NOT NULL;

    IF @EmpresaId IS NULL
        SELECT @EmpresaId = CASE WHEN COUNT(*) = 1 THEN MIN(Id) END
        FROM dbo.Empresas WHERE Tipo = 'Cliente' AND FornecedoraId IS NOT NULL
            AND Nome COLLATE Latin1_General_100_CI_AI = @Empresa;

    IF @EmpresaId IS NULL
        THROW 50004, N'Confira o nome da empresa cadastrada no Taktos. Se houver empresas com o mesmo nome, informe o código da sua empresa.', 1;

    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
                   WHERE Id = @EmpresaId AND Tipo = 'Cliente' AND Ativa = 1)
        THROW 50004, N'Esta empresa está desativada. Entre em contato com a fornecedora.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email)
        THROW 50003, N'O e-mail informado já está em uso.', 1;
    IF EXISTS (SELECT 1 FROM dbo.SolicitacoesCadastro WITH (UPDLOCK, HOLDLOCK)
               WHERE Email = @Email AND Situacao = 'Pendente')
        THROW 50003, N'Já existe uma solicitação aguardando aprovação para esse e-mail.', 1;

    INSERT dbo.SolicitacoesCadastro (EmpresaId, Nome, Email, SenhaHash)
    VALUES (@EmpresaId, @Nome, @Email, @SenhaHash);
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalDecidirCadastro
    @SolicitacaoId int,
    @FornecedoraId int,
    @DecididaPor int,
    @Aprovar bit
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Email nvarchar(254);
    DECLARE @EmpresaId int;
    SELECT @Email = s.Email, @EmpresaId = s.EmpresaId FROM dbo.SolicitacoesCadastro s JOIN dbo.Empresas e ON e.Id = s.EmpresaId
    JOIN dbo.Usuarios u ON u.Id = @DecididaPor AND u.EmpresaId = @FornecedoraId
    JOIN dbo.Empresas f ON f.Id = u.EmpresaId AND f.Tipo = 'Fornecedora'
    WHERE s.Id = @SolicitacaoId AND e.Tipo = 'Cliente' AND e.FornecedoraId = f.Id;

    IF @Email IS NULL THROW 50002, N'Solicitação indisponível para esta fornecedora.', 1;

    BEGIN TRANSACTION;
    DECLARE @EmpresaAtiva bit;
    SELECT @EmpresaAtiva = Ativa FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = @EmpresaId AND Tipo = 'Cliente' AND FornecedoraId = @FornecedoraId;
    IF @EmpresaAtiva IS NULL THROW 50002, N'Solicitação indisponível para esta fornecedora.', 1;
    IF @EmpresaAtiva = 0 AND @Aprovar = 1
        THROW 50003, N'Reative a empresa para aprovar a solicitação.', 1;

    DECLARE @ContaExistente int;
    SELECT @ContaExistente = Id FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email;

    DECLARE @Nome nvarchar(120);
    DECLARE @SenhaHash nvarchar(500);
    DECLARE @Situacao varchar(10);
    SELECT @Nome = Nome, @SenhaHash = SenhaHash, @Situacao = Situacao
    FROM dbo.SolicitacoesCadastro WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = @SolicitacaoId AND EmpresaId = @EmpresaId;

    IF @Situacao IS NULL THROW 50002, N'Solicitação indisponível para esta fornecedora.', 1;
    IF @Situacao <> 'Pendente' THROW 50003, N'Esta solicitação já foi analisada.', 1;

    DECLARE @UsuarioId int = NULL;
    IF @Aprovar = 1
    BEGIN
        IF @ContaExistente IS NOT NULL THROW 50003, N'O e-mail já está em uso. Confira a solicitação.', 1;
        INSERT dbo.Usuarios (EmpresaId, Nome, Email, SenhaHash)
        VALUES (@EmpresaId, @Nome, @Email, @SenhaHash);
        SET @UsuarioId = CONVERT(int, SCOPE_IDENTITY());
    END;

    UPDATE dbo.SolicitacoesCadastro
    SET Situacao = CASE WHEN @Aprovar = 1 THEN 'Aprovada' ELSE 'Recusada' END,
        SenhaHash = NULL, DecididaEm = SYSUTCDATETIME(),
        DecididaPor = @DecididaPor, UsuarioCriadoId = @UsuarioId
    WHERE Id = @SolicitacaoId;
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalAtualizarPerfil
    @UsuarioId int,
    @EmpresaId int,
    @Nome nvarchar(120),
    @Email nvarchar(254),
    @EmailAnterior nvarchar(254),
    @SenhaHashAnterior nvarchar(500),
    @NovoHash nvarchar(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
                   WHERE Id = @EmpresaId AND Ativa = 1)
        THROW 50003, N'A empresa da conta está desativada.', 1;

    DECLARE @OutroUsuario int;
    SELECT @OutroUsuario = Id FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email;
    IF @OutroUsuario IS NOT NULL AND @OutroUsuario <> @UsuarioId
        THROW 50003, N'O e-mail informado já está em uso.', 1;

    IF @Email <> @EmailAnterior AND EXISTS (SELECT 1 FROM dbo.SolicitacoesCadastro WITH (UPDLOCK, HOLDLOCK)
        WHERE Email = @Email AND Situacao = 'Pendente')
        THROW 50003, N'O e-mail informado já está em uso.', 1;

    DECLARE @Sessao TABLE (Versao int NOT NULL);
    UPDATE dbo.Usuarios
    SET Nome = @Nome, Email = @Email, SenhaHash = COALESCE(@NovoHash, SenhaHash),
        VersaoSessao = VersaoSessao + CASE WHEN Email <> @Email OR @NovoHash IS NOT NULL THEN 1 ELSE 0 END
    OUTPUT inserted.VersaoSessao INTO @Sessao (Versao)
    WHERE Id = @UsuarioId AND EmpresaId = @EmpresaId AND Ativo = 1 AND Email = @EmailAnterior
        AND SenhaHash COLLATE Latin1_General_100_BIN2 = @SenhaHashAnterior;

    IF @@ROWCOUNT = 0
        THROW 50003, N'O perfil foi atualizado em outra sessão. Recarregue a página e tente novamente.', 1;

    COMMIT TRANSACTION;
    SELECT Versao FROM @Sessao;
END;
GO
