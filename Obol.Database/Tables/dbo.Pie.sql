CREATE TABLE [dbo].[Pie]
(
    [UserID]          BIGINT          NOT NULL,
    [PieID]           BIGINT          NOT NULL,
    [Name]            NVARCHAR (100)  NOT NULL,
    [Icon]            NVARCHAR (40)   NOT NULL,
    [Cash]            DECIMAL (18, 2) NOT NULL,
    [Invested]        DECIMAL (18, 2) NOT NULL,
    [Value]           DECIMAL (18, 2) NOT NULL,
    [Result]          DECIMAL (18, 2) NOT NULL,
    [DividendsGained] DECIMAL (18, 2) NOT NULL,
    [Goal]            DECIMAL (18, 2) NULL,
    [CreatedAt]       DATETIME2 (0)   NULL,
    [UpdatedAt]       DATETIME2 (0)   NOT NULL,
    CONSTRAINT [PK_Pie] PRIMARY KEY CLUSTERED ([UserID] ASC, [PieID] ASC),
    CONSTRAINT [FK_Pie_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The Trading 212 pies of an account as the last sync read them (replaced on every sync). PieID is the Trading 212 id; amounts are in the account currency.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Pie';
