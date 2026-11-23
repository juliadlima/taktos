
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

IF DB_NAME() <> N'PortalEntregas'
    THROW 50001, N'Selecione o banco PortalEntregas.', 1;

SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;


IF OBJECT_ID(N'dbo.Sistemas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sistemas (
        Id int IDENTITY PRIMARY KEY,
        EmpresaId int NOT NULL REFERENCES dbo.Empresas(Id),
        Nome nvarchar(100) NOT NULL,
        CONSTRAINT UQ_Sistemas_EmpresaNome UNIQUE (EmpresaId, Nome),
        CONSTRAINT UQ_Sistemas_EmpresaId UNIQUE (EmpresaId, Id)
    );
END;

IF OBJECT_ID(N'dbo.OrdensServico', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrdensServico (
        Id int IDENTITY PRIMARY KEY,
        EmpresaId int NOT NULL REFERENCES dbo.Empresas(Id),
        SistemaId int NOT NULL,
        SolicitanteId int NOT NULL REFERENCES dbo.Usuarios(Id),
        Tipo varchar(10) NOT NULL CHECK (Tipo IN ('Correcao', 'Melhoria')),
        Titulo nvarchar(150) NOT NULL,
        Descricao nvarchar(4000) NOT NULL,
        Situacao varchar(16) NOT NULL DEFAULT 'Aberta'
            CHECK (Situacao IN ('Aberta','EmAnalise','EmExecucao','EmHomologacao','Concluida')),
        Prioridade varchar(5) NULL CHECK (Prioridade IN ('Baixa','Media','Alta')),
        ResponsavelId int NULL REFERENCES dbo.Usuarios(Id),
        PrevisaoEntrega date NULL,
        PrimeiraPrevisao date NULL,
        CriadaEm datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        AtualizadaEm datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Ordens_SistemaEmpresa FOREIGN KEY (EmpresaId, SistemaId)
            REFERENCES dbo.Sistemas(EmpresaId, Id)
    );
    CREATE INDEX IX_Ordens_EmpresaSituacao ON dbo.OrdensServico(EmpresaId, Situacao, AtualizadaEm DESC);
END;

IF OBJECT_ID(N'dbo.Entregas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Entregas (
        Id int IDENTITY PRIMARY KEY,
        OrdemId int NOT NULL REFERENCES dbo.OrdensServico(Id),
        Versao nvarchar(60) NOT NULL,
        Gmud nvarchar(150) NOT NULL,
        LinkGmud nvarchar(600) NULL,
        ResumoTestes nvarchar(2000) NULL,
        Resultado varchar(9) NOT NULL DEFAULT 'Pendente' CHECK (Resultado IN ('Pendente','Aprovada','Devolvida')),
        Motivo nvarchar(2000) NULL,
        CriadaPor int NOT NULL REFERENCES dbo.Usuarios(Id),
        CriadaEm datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ValidadaPor int NULL REFERENCES dbo.Usuarios(Id),
        ValidadaEm datetime2(3) NULL,
        CONSTRAINT UQ_Entregas_Versao UNIQUE (OrdemId, Versao),
        CONSTRAINT CK_Entregas_Validacao CHECK (
            (Resultado = 'Pendente' AND ValidadaPor IS NULL AND ValidadaEm IS NULL)
            OR (Resultado = 'Aprovada' AND ValidadaPor IS NOT NULL AND ValidadaEm IS NOT NULL)
            OR (Resultado = 'Devolvida' AND ValidadaPor IS NOT NULL AND ValidadaEm IS NOT NULL AND Motivo IS NOT NULL)
        )
    );
    CREATE UNIQUE INDEX UX_Entregas_UmaPendente ON dbo.Entregas(OrdemId) WHERE Resultado = 'Pendente';
END;

IF OBJECT_ID(N'dbo.MensagensOrdem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MensagensOrdem (
        Id int IDENTITY PRIMARY KEY,
        OrdemId int NOT NULL REFERENCES dbo.OrdensServico(Id),
        AutorId int NOT NULL REFERENCES dbo.Usuarios(Id),
        Tipo varchar(16) NOT NULL CHECK (Tipo IN ('Mensagem','SolicitacaoVpn')),
        Texto nvarchar(2000) NOT NULL,
        CriadaEm datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Mensagens_Ordem ON dbo.MensagensOrdem(OrdemId, Id);
END;

IF OBJECT_ID(N'dbo.HistoricoOrdem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.HistoricoOrdem (
        Id int IDENTITY PRIMARY KEY,
        OrdemId int NOT NULL REFERENCES dbo.OrdensServico(Id),
        AutorId int NULL REFERENCES dbo.Usuarios(Id),
        Evento nvarchar(250) NOT NULL,
        CriadoEm datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_Historico_Ordem ON dbo.HistoricoOrdem(OrdemId, Id);
END;

INSERT INTO dbo.Sistemas (EmpresaId, Nome)
SELECT e.Id, demo.Nome
FROM dbo.Empresas AS e JOIN (VALUES
    ('demo-alfa', N'REDEX'), ('demo-alfa', N'GIPA'), ('demo-beta', N'GIPA')
) AS demo(Codigo, Nome) ON demo.Codigo = e.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Sistemas AS s WHERE s.EmpresaId = e.Id AND s.Nome = demo.Nome
);

COMMIT TRANSACTION;
GO
CREATE OR ALTER TRIGGER dbo.PortalRegistrarMudancas
ON dbo.OrdensServico
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.HistoricoOrdem(OrdemId, AutorId, Evento)
    SELECT i.Id, TRY_CONVERT(int, SESSION_CONTEXT(N'portal_usuario')),
        CONCAT(N'Situação: ', i.Situacao, N' | prioridade: ', COALESCE(i.Prioridade, 'pendente'),
            N' | responsável: ', COALESCE(u.Nome, N'a definir'), N' | previsão: ',
            COALESCE(CONVERT(char(10), i.PrevisaoEntrega, 103), 'pendente'))
    FROM inserted AS i
    JOIN deleted AS d ON d.Id = i.Id
    LEFT JOIN dbo.Usuarios AS u ON u.Id = i.ResponsavelId
    WHERE i.Situacao <> d.Situacao OR ISNULL(i.Prioridade, '') <> ISNULL(d.Prioridade, '')
        OR ISNULL(i.ResponsavelId, 0) <> ISNULL(d.ResponsavelId, 0)
        OR EXISTS (SELECT i.PrevisaoEntrega EXCEPT SELECT d.PrevisaoEntrega);
END;
GO
CREATE OR ALTER PROCEDURE dbo.PortalListarOrdens
    @EmpresaId int, @Fornecedora bit, @OrdemId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.Id, e.Nome AS Empresa, s.Nome AS Sistema, o.Tipo, o.Titulo, o.Descricao, o.Situacao,
        o.Prioridade, o.ResponsavelId, r.Nome AS Responsavel, u.Nome AS Solicitante,
        CONVERT(char(10), o.PrevisaoEntrega, 23) AS Previsao,
        CONVERT(char(10), o.PrimeiraPrevisao, 23) AS PrimeiraPrevisao,
        o.CriadaEm, o.AtualizadaEm, dados.PrimeiraEntrega, dados.VersoesDecididas, dados.VersoesDevolvidas
    FROM dbo.OrdensServico o
    JOIN dbo.Empresas e ON e.Id = o.EmpresaId
    JOIN dbo.Sistemas s ON s.Id = o.SistemaId
    JOIN dbo.Usuarios u ON u.Id = o.SolicitanteId
    LEFT JOIN dbo.Usuarios r ON r.Id = o.ResponsavelId
    OUTER APPLY (
        SELECT MIN(x.CriadaEm) AS PrimeiraEntrega,
            COUNT(CASE WHEN x.Resultado <> 'Pendente' THEN 1 END) AS VersoesDecididas,
            COUNT(CASE WHEN x.Resultado = 'Devolvida' THEN 1 END) AS VersoesDevolvidas
        FROM dbo.Entregas x WHERE x.OrdemId = o.Id
    ) AS dados
    WHERE ((@Fornecedora = 1 AND e.FornecedoraId = @EmpresaId)
        OR (@Fornecedora = 0 AND o.EmpresaId = @EmpresaId))
        AND (@OrdemId IS NULL OR o.Id = @OrdemId)
    ORDER BY o.AtualizadaEm DESC, o.Id DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PortalAbrirOrdem
    @EmpresaId int, @UsuarioId int, @SistemaId int, @Tipo varchar(10),
    @Titulo nvarchar(150), @Descricao nvarchar(4000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Sistemas s JOIN dbo.Empresas e ON e.Id=s.EmpresaId
        WHERE s.Id=@SistemaId AND s.EmpresaId=@EmpresaId AND e.Tipo='Cliente')
        THROW 50004, N'Selecione um sistema da sua empresa.', 1;
    BEGIN TRANSACTION;
    INSERT dbo.OrdensServico(EmpresaId,SistemaId,SolicitanteId,Tipo,Titulo,Descricao)
        VALUES(@EmpresaId,@SistemaId,@UsuarioId,@Tipo,@Titulo,@Descricao);
    DECLARE @Id int = CONVERT(int, SCOPE_IDENTITY());
    INSERT dbo.HistoricoOrdem(OrdemId,AutorId,Evento) VALUES(@Id,@UsuarioId,N'OS aberta');
    COMMIT TRANSACTION;
    SELECT @Id;
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
    SELECT @Atual=o.Situacao FROM dbo.OrdensServico o WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.Empresas e ON e.Id=o.EmpresaId
        WHERE o.Id=@OrdemId AND e.FornecedoraId=@EmpresaId;
    IF @Atual IS NULL THROW 50002, N'Ordem de serviço indisponível.', 1;
    IF @Atual NOT IN ('Aberta','EmAnalise','EmExecucao') OR NOT (
        @Atual=@Situacao OR (@Atual='Aberta' AND @Situacao='EmAnalise')
        OR (@Atual='EmAnalise' AND @Situacao='EmExecucao'))
        THROW 50003, N'A situação selecionada não está disponível nesta etapa.', 1;
    IF @ResponsavelId IS NOT NULL AND NOT EXISTS
        (SELECT 1 FROM dbo.Usuarios WHERE Id=@ResponsavelId AND EmpresaId=@EmpresaId)
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
CREATE OR ALTER PROCEDURE dbo.PortalEnviarMensagem
    @OrdemId int, @EmpresaId int, @UsuarioId int, @Fornecedora bit, @Tipo varchar(16), @Texto nvarchar(2000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @Tipo='SolicitacaoVpn' AND @Fornecedora=0 THROW 50004, N'A solicitação de VPN é registrada pela fornecedora.', 1;
    BEGIN TRANSACTION;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.OrdensServico o WITH (UPDLOCK,HOLDLOCK) JOIN dbo.Empresas e ON e.Id=o.EmpresaId
        WHERE o.Id=@OrdemId AND ((@Fornecedora=1 AND e.FornecedoraId=@EmpresaId)
            OR (@Fornecedora=0 AND e.Id=@EmpresaId))
    ) THROW 50002, N'Ordem de serviço indisponível.', 1;
    INSERT dbo.MensagensOrdem(OrdemId,AutorId,Tipo,Texto) VALUES(@OrdemId,@UsuarioId,@Tipo,@Texto);
    UPDATE dbo.OrdensServico SET AtualizadaEm=SYSUTCDATETIME() WHERE Id=@OrdemId;
    COMMIT TRANSACTION;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PortalRegistrarEntrega
    @OrdemId int, @EmpresaId int, @UsuarioId int, @Versao nvarchar(60), @Gmud nvarchar(150),
    @LinkGmud nvarchar(600), @ResumoTestes nvarchar(2000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @Situacao varchar(16);
    SELECT @Situacao=o.Situacao FROM dbo.OrdensServico o WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.Empresas e ON e.Id=o.EmpresaId
        WHERE o.Id=@OrdemId AND e.FornecedoraId=@EmpresaId;
    IF @Situacao IS NULL THROW 50002, N'Ordem de serviço indisponível.', 1;
    IF @Situacao <> 'EmExecucao' THROW 50003, N'Para registrar uma entrega, a OS deve estar em execução.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Entregas WHERE OrdemId=@OrdemId AND Versao=@Versao)
        THROW 50003, N'Essa versão já foi registrada na OS.', 1;
    INSERT dbo.Entregas(OrdemId,Versao,Gmud,LinkGmud,ResumoTestes,CriadaPor)
        VALUES(@OrdemId,@Versao,@Gmud,@LinkGmud,@ResumoTestes,@UsuarioId);
    EXEC sys.sp_set_session_context @key=N'portal_usuario', @value=@UsuarioId;
    UPDATE dbo.OrdensServico SET Situacao='EmHomologacao', AtualizadaEm=SYSUTCDATETIME() WHERE Id=@OrdemId;
    INSERT dbo.HistoricoOrdem(OrdemId,AutorId,Evento) VALUES(@OrdemId,@UsuarioId,CONCAT(N'Versão enviada: ',@Versao));
    COMMIT TRANSACTION;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PortalHomologar
    @OrdemId int, @EntregaId int, @EmpresaId int, @UsuarioId int, @Resultado varchar(9), @Motivo nvarchar(2000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @Situacao varchar(16);
    SELECT @Situacao=Situacao FROM dbo.OrdensServico WITH (UPDLOCK,HOLDLOCK)
        WHERE Id=@OrdemId AND EmpresaId=@EmpresaId;
    IF @Situacao IS NULL THROW 50002, N'Ordem de serviço indisponível.', 1;
    IF @Situacao <> 'EmHomologacao' THROW 50003, N'Esta OS não está em homologação.', 1;
    IF @Resultado='Devolvida' AND @Motivo IS NULL THROW 50004, N'Informe o motivo da devolução.', 1;
    UPDATE dbo.Entregas SET Resultado=@Resultado, Motivo=@Motivo, ValidadaEm=SYSUTCDATETIME(), ValidadaPor=@UsuarioId
        WHERE Id=@EntregaId AND OrdemId=@OrdemId AND Resultado='Pendente';
    IF @@ROWCOUNT=0 THROW 50003, N'A entrega selecionada não aguarda validação.', 1;
    EXEC sys.sp_set_session_context @key=N'portal_usuario', @value=@UsuarioId;
    UPDATE dbo.OrdensServico SET Situacao=CASE WHEN @Resultado='Aprovada' THEN 'Concluida' ELSE 'EmExecucao' END,
        AtualizadaEm=SYSUTCDATETIME() WHERE Id=@OrdemId;
    INSERT dbo.HistoricoOrdem(OrdemId,AutorId,Evento)
        SELECT @OrdemId,@UsuarioId,CONCAT(N'Versão ', Versao, N' ', LOWER(@Resultado)) FROM dbo.Entregas WHERE Id=@EntregaId;
    COMMIT TRANSACTION;
END;
GO
