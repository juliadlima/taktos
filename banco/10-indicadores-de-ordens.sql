USE [PortalEntregas];
GO

IF OBJECT_ID(N'dbo.IndicadoresOrdem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IndicadoresOrdem (
        Id int IDENTITY PRIMARY KEY,
        FornecedoraId int NOT NULL REFERENCES dbo.Empresas(Id),
        Nome nvarchar(120) COLLATE Latin1_General_100_CI_AI NOT NULL,
        Criterio varchar(20) NOT NULL,
        CONSTRAINT UQ_IndicadoresOrdem_FornecedoraNome UNIQUE (FornecedoraId, Nome),
        CONSTRAINT CK_IndicadoresOrdem_Criterio CHECK (Criterio IN (
            'Total', 'Aberta', 'EmAnalise', 'EmExecucao', 'EmHomologacao', 'Concluida',
            'SemResponsavel', 'Correcao', 'Melhoria'
        ))
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalCadastrarIndicador
    @FornecedoraId int,
    @Nome nvarchar(120),
    @Criterio varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WHERE Id = @FornecedoraId AND Tipo = 'Fornecedora' AND Ativa = 1)
        THROW 50002, N'Fornecedora indisponível.', 1;

    IF EXISTS (SELECT 1 FROM dbo.IndicadoresOrdem WITH (UPDLOCK, HOLDLOCK)
               WHERE FornecedoraId = @FornecedoraId AND Nome = @Nome)
        THROW 50003, N'Já existe um indicador com esse nome.', 1;

    INSERT dbo.IndicadoresOrdem (FornecedoraId, Nome, Criterio)
    VALUES (@FornecedoraId, @Nome, @Criterio);
    DECLARE @Id int = CONVERT(int, SCOPE_IDENTITY());
    COMMIT TRANSACTION;

    SELECT @Id AS Id;
END;
GO
