CREATE TABLE [dbo].[BrokerConnection]
(
    [UserID]           BIGINT          NOT NULL,
    [Environment]      NVARCHAR (8)    NOT NULL CONSTRAINT [DF_BrokerConnection_Environment] DEFAULT (N'live'),
    [ApiKeyCipher]     NVARCHAR (1000) NOT NULL,
    [ApiSecretCipher]  NVARCHAR (1000) NOT NULL,
    [KeyHint]          NVARCHAR (12)   NOT NULL,
    [AccountId]        BIGINT          NULL,
    [Currency]         CHAR (3)        NULL,
    [SyncPositions]    BIT             NOT NULL CONSTRAINT [DF_BrokerConnection_SyncPositions] DEFAULT (1),
    [SyncOrders]       BIT             NOT NULL CONSTRAINT [DF_BrokerConnection_SyncOrders] DEFAULT (1),
    [SyncDividends]    BIT             NOT NULL CONSTRAINT [DF_BrokerConnection_SyncDividends] DEFAULT (1),
    [SyncTransactions] BIT             NOT NULL CONSTRAINT [DF_BrokerConnection_SyncTransactions] DEFAULT (1),
    [State]            TINYINT         NOT NULL CONSTRAINT [DF_BrokerConnection_State] DEFAULT (0),
    [LastMessage]      NVARCHAR (400)  NOT NULL CONSTRAINT [DF_BrokerConnection_LastMessage] DEFAULT (N''),
    [LastSyncAt]       DATETIME2 (0)   NULL,
    [HistoryDoneAt]    DATETIME2 (0)   NULL,
    [CreatedAt]        DATETIME2 (0)   NOT NULL CONSTRAINT [DF_BrokerConnection_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_BrokerConnection] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [FK_BrokerConnection_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_BrokerConnection_Environment] CHECK ([Environment] IN (N'live', N'demo'))
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The Trading 212 API key and secret of an account, encrypted with AES-GCM by the WebApi (OBOL_ENCRYPTION_KEY), what to sync and how the last sync went. Times in UTC. HistoryDoneAt: when the whole order, dividend and transaction history was first read (later syncs only read what is new).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'BrokerConnection';
