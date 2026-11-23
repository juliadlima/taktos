USE [PortalEntregas];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF DB_NAME() <> N'PortalEntregas' THROW 50001, N'Selecione o banco PortalEntregas.', 1;

IF OBJECT_ID(N'dbo.SolicitacoesCadastro', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SolicitacoesCadastro (
        Id int IDENTITY PRIMARY KEY,
        EmpresaId int NOT NULL REFERENCES dbo.Empresas(Id),
        Nome nvarchar(120) NOT NULL,
        Email nvarchar(254) NOT NULL,
        SenhaHash nvarchar(500) NULL,
        Situacao varchar(10) NOT NULL DEFAULT 'Pendente',
        SolicitadaEm datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        DecididaEm datetime2(3) NULL,
        DecididaPor int NULL REFERENCES dbo.Usuarios(Id),
        UsuarioCriadoId int NULL REFERENCES dbo.Usuarios(Id),
        CONSTRAINT CK_SolicitacoesCadastro_Estado CHECK (
            (Situacao = 'Pendente' AND SenhaHash IS NOT NULL AND DecididaEm IS NULL
                AND DecididaPor IS NULL AND UsuarioCriadoId IS NULL)
            OR (Situacao = 'Aprovada' AND SenhaHash IS NULL AND DecididaEm IS NOT NULL
                AND DecididaPor IS NOT NULL AND UsuarioCriadoId IS NOT NULL)
            OR (Situacao = 'Recusada' AND SenhaHash IS NULL AND DecididaEm IS NOT NULL
                AND DecididaPor IS NOT NULL AND UsuarioCriadoId IS NULL)
        )
    );
    CREATE UNIQUE INDEX UX_SolicitacoesCadastro_PendenteEmail
        ON dbo.SolicitacoesCadastro (Email) WHERE Situacao = 'Pendente';
    CREATE INDEX IX_SolicitacoesCadastro_EmpresaSituacao
        ON dbo.SolicitacoesCadastro (EmpresaId, Situacao, SolicitadaEm);
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
    SELECT @Email = s.Email FROM dbo.SolicitacoesCadastro s JOIN dbo.Empresas e ON e.Id = s.EmpresaId
    JOIN dbo.Usuarios u ON u.Id = @DecididaPor AND u.EmpresaId = @FornecedoraId
    JOIN dbo.Empresas f ON f.Id = u.EmpresaId AND f.Tipo = 'Fornecedora'
    WHERE s.Id = @SolicitacaoId AND e.Tipo = 'Cliente' AND e.FornecedoraId = f.Id;

    IF @Email IS NULL THROW 50002, N'Solicitação indisponível para esta fornecedora.', 1;

    BEGIN TRANSACTION;
    DECLARE @ContaExistente int;
    SELECT @ContaExistente = Id FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email;

    DECLARE @EmpresaId int;
    DECLARE @Nome nvarchar(120);
    DECLARE @SenhaHash nvarchar(500);
    DECLARE @Situacao varchar(10);
    SELECT @EmpresaId = s.EmpresaId, @Nome = s.Nome, @SenhaHash = s.SenhaHash, @Situacao = s.Situacao
    FROM dbo.SolicitacoesCadastro s WITH (UPDLOCK, HOLDLOCK) JOIN dbo.Empresas e ON e.Id = s.EmpresaId
    WHERE s.Id = @SolicitacaoId AND e.Tipo = 'Cliente' AND e.FornecedoraId = @FornecedoraId;

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

    DECLARE @OutroUsuario int;
    SELECT @OutroUsuario = Id FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Email = @Email;
    IF @OutroUsuario IS NOT NULL AND @OutroUsuario <> @UsuarioId
        THROW 50003, N'O e-mail informado já está em uso.', 1;

    IF @Email <> @EmailAnterior AND EXISTS (SELECT 1 FROM dbo.SolicitacoesCadastro WITH (UPDLOCK, HOLDLOCK)
        WHERE Email = @Email AND Situacao = 'Pendente')
        THROW 50003, N'O e-mail informado já está em uso.', 1;

    UPDATE dbo.Usuarios SET Nome = @Nome, Email = @Email, SenhaHash = COALESCE(@NovoHash, SenhaHash)
    WHERE Id = @UsuarioId AND EmpresaId = @EmpresaId AND Email = @EmailAnterior
        AND SenhaHash COLLATE Latin1_General_100_BIN2 = @SenhaHashAnterior;

    IF @@ROWCOUNT = 0
        THROW 50003, N'O perfil foi atualizado em outra sessão. Recarregue a página e tente novamente.', 1;

    COMMIT TRANSACTION;
END;
GO
