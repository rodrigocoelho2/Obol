CREATE TABLE [dbo].[Dividend]
(
    [DividendID]   BIGINT           IDENTITY (1, 1) NOT NULL,
    [UserID]       BIGINT           NOT NULL,
    [InstrumentID] INT              NOT NULL,
    [DividendKey]  NVARCHAR (100)   NOT NULL,
    [PaidAt]       DATETIME2 (0)    NOT NULL,
    [Quantity]     DECIMAL (28, 10) NULL,
    [PerShare]     DECIMAL (18, 8)  NULL,
    [Amount]       DECIMAL (18, 2)  NOT NULL,
    [Withheld]     DECIMAL (18, 2)  NULL,
    [Kind]         NVARCHAR (60)    NOT NULL,
    [Source]       TINYINT          NOT NULL,
    CONSTRAINT [PK_Dividend] PRIMARY KEY CLUSTERED ([DividendID] ASC),
    CONSTRAINT [UQ_Dividend_UserKey] UNIQUE NONCLUSTERED ([UserID] ASC, [DividendKey] ASC),
    CONSTRAINT [FK_Dividend_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [FK_Dividend_Instrument] FOREIGN KEY ([InstrumentID]) REFERENCES [dbo].[Instrument] ([InstrumentID])
);
GO

CREATE NONCLUSTERED INDEX [IX_Dividend_UserPaidAt] ON [dbo].[Dividend] ([UserID] ASC, [PaidAt] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Dividends paid into an account. Amount is net, in the account currency; PerShare is gross, in the instrument currency. PaidAt in UTC.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Dividend';
