CREATE TABLE [dbo].[AccountSnapshot]
(
    [UserID]       BIGINT          NOT NULL,
    [Day]          DATE            NOT NULL,
    [TotalValue]   DECIMAL (18, 2) NOT NULL,
    [Cash]         DECIMAL (18, 2) NOT NULL,
    [Cost]         DECIMAL (18, 2) NOT NULL,
    [UnrealizedPL] DECIMAL (18, 2) NOT NULL,
    [RealizedPL]   DECIMAL (18, 2) NOT NULL,
    [UpdatedAt]    DATETIME2 (0)   NOT NULL CONSTRAINT [DF_AccountSnapshot_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_AccountSnapshot] PRIMARY KEY CLUSTERED ([UserID] ASC, [Day] ASC),
    CONSTRAINT [FK_AccountSnapshot_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The account as Trading 212 reported it, one row per day (the last sync of the day wins), in the account currency. Day is a date in the app time zone.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AccountSnapshot';
