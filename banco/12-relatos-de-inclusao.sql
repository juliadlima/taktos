USE [PortalEntregas];
GO

IF OBJECT_ID(N'dbo.RelatosInclusao', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RelatosInclusao (
        Id int IDENTITY PRIMARY KEY,
        FornecedoraId int NOT NULL REFERENCES dbo.Empresas(Id),
        AutorId int NOT NULL REFERENCES dbo.Usuarios(Id),
        OrdemId int NULL REFERENCES dbo.OrdensServico(Id),
        Categoria varchar(20) NOT NULL CHECK (Categoria IN ('Discriminacao', 'Acessibilidade')),
        Descricao nvarchar(2000) NOT NULL,
        Situacao varchar(12) NOT NULL DEFAULT 'Recebido',
        Resposta nvarchar(2000) NULL,
        RevisorId int NULL REFERENCES dbo.Usuarios(Id),
        CriadoEm datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        RevisadoEm datetime2(3) NULL,
        CONSTRAINT CK_RelatosInclusao_Situacao CHECK (
            (Situacao = 'Recebido' AND Resposta IS NULL AND RevisorId IS NULL AND RevisadoEm IS NULL)
            OR (Situacao = 'EmAnalise' AND Resposta IS NULL AND RevisorId IS NOT NULL AND RevisadoEm IS NOT NULL)
            OR (Situacao = 'Respondido' AND Resposta IS NOT NULL AND RevisorId IS NOT NULL AND RevisadoEm IS NOT NULL)
        )
    );
    CREATE INDEX IX_RelatosInclusao_Autor ON dbo.RelatosInclusao(AutorId, Id);
    CREATE INDEX IX_RelatosInclusao_Fornecedora ON dbo.RelatosInclusao(FornecedoraId, Id);
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalRegistrarRelato
    @UsuarioId int,
    @Categoria varchar(20),
    @Descricao nvarchar(2000),
    @OrdemId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @EmpresaId int, @Tipo varchar(12), @FornecedoraId int;

    SELECT @EmpresaId = e.Id, @Tipo = e.Tipo,
        @FornecedoraId = CASE WHEN e.Tipo = 'Fornecedora' THEN e.Id ELSE e.FornecedoraId END
    FROM dbo.Usuarios u JOIN dbo.Empresas e ON e.Id = u.EmpresaId
    WHERE u.Id = @UsuarioId AND u.Ativo = 1 AND e.Ativa = 1;

    IF @FornecedoraId IS NULL
        THROW 50002, N'Canal de relatos indisponível para esta conta.', 1;

    IF @OrdemId IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM dbo.OrdensServico o JOIN dbo.Empresas c ON c.Id = o.EmpresaId
        WHERE o.Id = @OrdemId AND (
            (@Tipo = 'Cliente' AND c.Id = @EmpresaId)
            OR (@Tipo = 'Fornecedora' AND c.FornecedoraId = @EmpresaId)
        )
    )
        THROW 50002, N'Confira o número da OS ou deixe o campo em branco.', 1;

    INSERT dbo.RelatosInclusao (FornecedoraId, AutorId, OrdemId, Categoria, Descricao)
    VALUES (@FornecedoraId, @UsuarioId, @OrdemId, @Categoria, @Descricao);
    SELECT CONVERT(int, SCOPE_IDENTITY()) AS Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.PortalTratarRelato
    @UsuarioId int,
    @RelatoId int,
    @Situacao varchar(12),
    @Resposta nvarchar(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.RelatosInclusao r
        JOIN dbo.Empresas e ON e.Id = r.FornecedoraId
        JOIN dbo.Usuarios u ON u.Id = e.AdministradorId AND u.EmpresaId = e.Id
        WHERE r.Id = @RelatoId AND u.Id = @UsuarioId AND u.Ativo = 1 AND e.Ativa = 1
    )
        THROW 50002, N'Relato indisponível para esta conta.', 1;

    UPDATE r SET Situacao = @Situacao, Resposta = @Resposta, RevisadoEm = SYSUTCDATETIME(), RevisorId = @UsuarioId
    FROM dbo.RelatosInclusao r
    JOIN dbo.Empresas e ON e.Id = r.FornecedoraId
    JOIN dbo.Usuarios u ON u.Id = e.AdministradorId AND u.EmpresaId = e.Id
    WHERE r.Id = @RelatoId AND u.Id = @UsuarioId AND u.Ativo = 1 AND e.Ativa = 1
        AND (r.Situacao = 'Recebido' OR (r.Situacao = 'EmAnalise' AND @Situacao = 'Respondido'));

    IF @@ROWCOUNT = 0
        THROW 50003, N'A situação deste relato já mudou. Atualize a lista.', 1;

    COMMIT TRANSACTION;
END;
GO
