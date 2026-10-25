CREATE TABLE [dbo].[Trade]
(
    [TradeID]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [UserID]        BIGINT           NOT NULL,
    [InstrumentID]  INT              NOT NULL,
    [TradeKey]      NVARCHAR (100)   NOT NULL,
    [Side]          TINYINT          NOT NULL,
    [Quantity]      DECIMAL (28, 10) NOT NULL,
    [Price]         DECIMAL (28, 10) NOT NULL,
    [PriceCurrency] CHAR (3)         NOT NULL,
    [FxRate]        DECIMAL (18, 8)  NULL,
    [Total]         DECIMAL (18, 2)  NOT NULL,
    [RealizedPL]    DECIMAL (18, 2)  NULL,
    [Fees]          DECIMAL (18, 2)  NOT NULL CONSTRAINT [DF_Trade_Fees] DEFAULT (0),
    [ExecutedAt]    DATETIME2 (0)    NOT NULL,
    [Source]        TINYINT          NOT NULL,
    [ExternalId]    NVARCHAR (60)    NULL,
    CONSTRAINT [PK_Trade] PRIMARY KEY CLUSTERED ([TradeID] ASC),
    CONSTRAINT [UQ_Trade_UserKey] UNIQUE NONCLUSTERED ([UserID] ASC, [TradeKey] ASC),
    CONSTRAINT [FK_Trade_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [FK_Trade_Instrument] FOREIGN KEY ([InstrumentID]) REFERENCES [dbo].[Instrument] ([InstrumentID]),
    CONSTRAINT [CK_Trade_Side] CHECK ([Side] IN (1, 2))
);
GO

CREATE NONCLUSTERED INDEX [IX_Trade_UserExecutedAt] ON [dbo].[Trade] ([UserID] ASC, [ExecutedAt] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Every buy (Side 1) and sell (Side 2) of an account, from the API or a CSV export. TradeKey (instrument, second and quantity) makes the same fill from both sources one row. Price is in PriceCurrency; Total, RealizedPL and Fees in the account currency. ExecutedAt in UTC.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Trade';
