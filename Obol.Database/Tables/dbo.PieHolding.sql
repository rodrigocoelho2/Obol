CREATE TABLE [dbo].[PieHolding]
(
    [UserID]        BIGINT           NOT NULL,
    [PieID]         BIGINT           NOT NULL,
    [InstrumentID]  INT              NOT NULL,
    [Quantity]      DECIMAL (28, 10) NOT NULL,
    [Invested]      DECIMAL (18, 2)  NOT NULL,
    [Value]         DECIMAL (18, 2)  NOT NULL,
    [Result]        DECIMAL (18, 2)  NOT NULL,
    [CurrentShare]  DECIMAL (9, 6)   NULL,
    [ExpectedShare] DECIMAL (9, 6)   NULL,
    CONSTRAINT [PK_PieHolding] PRIMARY KEY CLUSTERED ([UserID] ASC, [PieID] ASC, [InstrumentID] ASC),
    CONSTRAINT [FK_PieHolding_Pie] FOREIGN KEY ([UserID], [PieID]) REFERENCES [dbo].[Pie] ([UserID], [PieID]) ON DELETE CASCADE,
    CONSTRAINT [FK_PieHolding_Instrument] FOREIGN KEY ([InstrumentID]) REFERENCES [dbo].[Instrument] ([InstrumentID])
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The instruments of a Trading 212 pie: the shares the pie owns, what they cost and are worth (account currency), and their share of the pie now and as set.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PieHolding';
