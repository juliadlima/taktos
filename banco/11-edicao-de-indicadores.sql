USE [PortalEntregas];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

ALTER TABLE dbo.IndicadoresOrdem DROP CONSTRAINT IF EXISTS CK_IndicadoresOrdem_Criterio;
ALTER TABLE dbo.IndicadoresOrdem WITH CHECK ADD CONSTRAINT CK_IndicadoresOrdem_Criterio CHECK (Criterio IN (
    'Total', 'Aberta', 'EmAnalise', 'EmExecucao', 'EmHomologacao', 'Concluida',
    'SemResponsavel', 'Correcao', 'Melhoria', 'SemPrevisao', 'PrimeirasNoPrazo', 'VersoesDevolvidas'
));

IF OBJECT_ID(N'dbo.IndicadoresPadraoOrdem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IndicadoresPadraoOrdem (
        FornecedoraId int NOT NULL REFERENCES dbo.Empresas(Id),
        IndicadorId int NOT NULL,
        Nome nvarchar(120) NOT NULL,
        Criterio varchar(20) NOT NULL,
        CONSTRAINT PK_IndicadoresPadraoOrdem PRIMARY KEY (FornecedoraId, IndicadorId),
        CONSTRAINT CK_IndicadoresPadraoOrdem_Id CHECK (IndicadorId IN (-3, -2, -1)),
        CONSTRAINT CK_IndicadoresPadraoOrdem_Criterio CHECK (Criterio IN (
            'Total', 'Aberta', 'EmAnalise', 'EmExecucao', 'EmHomologacao', 'Concluida',
            'SemResponsavel', 'Correcao', 'Melhoria', 'SemPrevisao', 'PrimeirasNoPrazo', 'VersoesDevolvidas'
        ))
    );
END;

COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.PortalAtualizarIndicador
    @FornecedoraId int,
    @Id int,
    @Nome nvarchar(120),
    @Criterio varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WHERE Id = @FornecedoraId AND Tipo = 'Fornecedora' AND Ativa = 1)
        THROW 50002, N'Fornecedora indisponível.', 1;

    IF @Id IN (-3, -2, -1)
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.IndicadoresPadraoOrdem WITH (UPDLOCK, HOLDLOCK)
                   WHERE FornecedoraId = @FornecedoraId AND IndicadorId = @Id)
        BEGIN
            UPDATE dbo.IndicadoresPadraoOrdem SET Nome = @Nome, Criterio = @Criterio
            WHERE FornecedoraId = @FornecedoraId AND IndicadorId = @Id;
        END
        ELSE
        BEGIN
            INSERT dbo.IndicadoresPadraoOrdem (FornecedoraId, IndicadorId, Nome, Criterio)
            VALUES (@FornecedoraId, @Id, @Nome, @Criterio);
        END;
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.IndicadoresOrdem WHERE FornecedoraId = @FornecedoraId AND Id = @Id)
            THROW 50002, N'Indicador indisponível para esta fornecedora.', 1;

        IF EXISTS (SELECT 1 FROM dbo.IndicadoresOrdem WITH (UPDLOCK, HOLDLOCK)
                   WHERE FornecedoraId = @FornecedoraId AND Nome = @Nome AND Id <> @Id)
            THROW 50003, N'Já existe um indicador com esse nome.', 1;

        UPDATE dbo.IndicadoresOrdem SET Nome = @Nome, Criterio = @Criterio
        WHERE FornecedoraId = @FornecedoraId AND Id = @Id;
    END;

    COMMIT TRANSACTION;
END;
GO
