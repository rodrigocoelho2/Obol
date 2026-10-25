CREATE TABLE [dbo].[PriceDay]
(
    [InstrumentID] INT              NOT NULL,
    [Day]          DATE             NOT NULL,
    [Close]        DECIMAL (28, 10) NOT NULL,
    [Source]       TINYINT          NOT NULL,
    CONSTRAINT [PK_PriceDay] PRIMARY KEY CLUSTERED ([InstrumentID] ASC, [Day] ASC),
    CONSTRAINT [FK_PriceDay_Instrument] FOREIGN KEY ([InstrumentID]) REFERENCES [dbo].[Instrument] ([InstrumentID]) ON DELETE CASCADE
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The price of an instrument at the end of each day, in its own currency: from Yahoo Finance (history) and from each Trading 212 sync (today). Shared by every account.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PriceDay';
