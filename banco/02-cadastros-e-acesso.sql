USE [PortalEntregas];
GO

IF DB_NAME() <> N'PortalEntregas'
    THROW 50001, N'Conecte-se ao banco PortalEntregas antes de executar este script.', 1;

SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Empresas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Empresas
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Empresas PRIMARY KEY,
        Codigo varchar(40) NOT NULL CONSTRAINT UQ_Empresas_Codigo UNIQUE,
        Nome nvarchar(120) NOT NULL,
        Tipo varchar(12) NOT NULL,
        FornecedoraId int NULL CONSTRAINT FK_Empresas_Fornecedora REFERENCES dbo.Empresas (Id),
        CONSTRAINT CK_Empresas_Tipo CHECK (
            (Tipo = 'Fornecedora' AND FornecedoraId IS NULL)
            OR (Tipo = 'Cliente' AND FornecedoraId IS NOT NULL AND FornecedoraId <> Id)
        )
    );
END;

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios
    (
        Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        EmpresaId int NOT NULL CONSTRAINT FK_Usuarios_Empresa REFERENCES dbo.Empresas (Id),
        Nome nvarchar(120) NOT NULL,
        Email nvarchar(254) NOT NULL CONSTRAINT UQ_Usuarios_Email UNIQUE,
        SenhaHash nvarchar(500) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK) WHERE Codigo = 'demo-fornecedora')
BEGIN
    INSERT INTO dbo.Empresas (Codigo, Nome, Tipo)
    VALUES ('demo-fornecedora', N'Fornecedora de demonstração', 'Fornecedora');
END;

DECLARE @FornecedoraId int = (SELECT Id FROM dbo.Empresas WHERE Codigo = 'demo-fornecedora');

INSERT INTO dbo.Empresas (Codigo, Nome, Tipo, FornecedoraId)
SELECT demo.Codigo, demo.Nome, 'Cliente', @FornecedoraId
FROM (VALUES
    ('demo-alfa', N'Cliente Alfa'),
    ('demo-beta', N'Cliente Beta')
) AS demo (Codigo, Nome)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Empresas AS e WITH (UPDLOCK, HOLDLOCK) WHERE e.Codigo = demo.Codigo);

COMMIT TRANSACTION;

SELECT Codigo, Nome, Tipo
FROM dbo.Empresas
WHERE Codigo IN ('demo-fornecedora', 'demo-alfa', 'demo-beta')
ORDER BY Id;
