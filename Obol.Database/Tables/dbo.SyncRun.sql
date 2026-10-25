CREATE TABLE [dbo].[SyncRun]
(
    [SyncRunID]  BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserID]     BIGINT         NOT NULL,
    [Source]     TINYINT        NOT NULL,
    [StartedAt]  DATETIME2 (0)  NOT NULL,
    [FinishedAt] DATETIME2 (0)  NOT NULL,
    [Ok]         BIT            NOT NULL,
    [Summary]    NVARCHAR (400) NOT NULL,
    [Lines]      NVARCHAR (MAX) NOT NULL CONSTRAINT [DF_SyncRun_Lines] DEFAULT (N'[]'),
    CONSTRAINT [PK_SyncRun] PRIMARY KEY CLUSTERED ([SyncRunID] ASC),
    CONSTRAINT [FK_SyncRun_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_SyncRun_User] ON [dbo].[SyncRun] ([UserID] ASC, [SyncRunID] DESC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The log of every sync (Trading 212 API) and CSV import: when, whether it worked, a one-line summary and the steps as a JSON array of strings. Only the last 30 per account are kept.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'SyncRun';
