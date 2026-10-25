CREATE TABLE [dbo].[Holding]
(
    [UserID]       BIGINT          NOT NULL,
    [InstrumentID] INT             NOT NULL,
    [Quantity]     DECIMAL (28, 10) NOT NULL,
    [AveragePrice] DECIMAL (28, 10) NOT NULL,
    [CurrentPrice] DECIMAL (28, 10) NOT NULL,
    [Value]        DECIMAL (18, 2)  NOT NULL,
    [Cost]         DECIMAL (18, 2)  NOT NULL,
    [UnrealizedPL] DECIMAL (18, 2)  NOT NULL,
    [FxImpact]     DECIMAL (18, 2)  NULL,
    [OpenedAt]     DATETIME2 (0)    NULL,
    [UpdatedAt]    DATETIME2 (0)    NOT NULL,
    CONSTRAINT [PK_Holding] PRIMARY KEY CLUSTERED ([UserID] ASC, [InstrumentID] ASC),
    CONSTRAINT [FK_Holding_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [FK_Holding_Instrument] FOREIGN KEY ([InstrumentID]) REFERENCES [dbo].[Instrument] ([InstrumentID])
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The open positions of an account as Trading 212 last reported them (replaced on every sync). Prices are in the instrument currency; Value, Cost, UnrealizedPL and FxImpact in the account currency.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Holding';
