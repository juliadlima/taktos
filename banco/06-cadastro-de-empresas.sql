USE [PortalEntregas];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.PortalCadastrarEmpresa
    @FornecedoraId int,
    @Nome nvarchar(120)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    -- A linha da fornecedora serializa cadastros concorrentes da sua carteira.
    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
                   WHERE Id = @FornecedoraId AND Tipo = 'Fornecedora')
        THROW 50002, N'Fornecedora indisponível.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Empresas
               WHERE FornecedoraId = @FornecedoraId AND Tipo = 'Cliente'
                 AND Nome COLLATE Latin1_General_100_CI_AI = @Nome)
        THROW 50003, N'Já existe uma empresa cliente com esse nome.', 1;

    DECLARE @Codigo varchar(40) = 'cli-' + LOWER(LEFT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 16));
    INSERT dbo.Empresas (Codigo, Nome, Tipo, FornecedoraId)
    VALUES (@Codigo, @Nome, 'Cliente', @FornecedoraId);
    DECLARE @Id int = CONVERT(int, SCOPE_IDENTITY());
    COMMIT TRANSACTION;

    SELECT @Id AS Id, @Codigo AS Codigo;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalCadastrarSistema
    @FornecedoraId int,
    @EmpresaId int,
    @Nome nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
                   WHERE Id = @EmpresaId AND Tipo = 'Cliente' AND FornecedoraId = @FornecedoraId)
        THROW 50002, N'Empresa indisponível para esta fornecedora.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Sistemas WHERE EmpresaId = @EmpresaId
               AND Nome COLLATE Latin1_General_100_CI_AI = @Nome)
        THROW 50003, N'Já existe um sistema com esse nome para essa empresa.', 1;

    INSERT dbo.Sistemas (EmpresaId, Nome) VALUES (@EmpresaId, @Nome);
    DECLARE @Id int = CONVERT(int, SCOPE_IDENTITY());
    COMMIT TRANSACTION;

    SELECT @Id AS Id;
END;
GO
