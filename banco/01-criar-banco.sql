USE [master];
GO

IF DB_ID(N'PortalEntregas') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [PortalEntregas]');
END;
GO

USE [PortalEntregas];
GO

SELECT DB_NAME() AS BancoAtual;
