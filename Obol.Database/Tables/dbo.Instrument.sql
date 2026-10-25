CREATE TABLE [dbo].[Instrument]
(
    [InstrumentID]  INT            IDENTITY (1, 1) NOT NULL,
    [InstrumentKey] NVARCHAR (40)  NOT NULL,
    [Isin]          CHAR (12)      NULL,
    [Ticker]        NVARCHAR (40)  NOT NULL,
    [Symbol]        NVARCHAR (20)  NOT NULL,
    [Name]          NVARCHAR (200) NOT NULL,
    [Currency]      CHAR (3)       NOT NULL,
    [AssetClass]    TINYINT        NOT NULL CONSTRAINT [DF_Instrument_AssetClass] DEFAULT (0),
    [PriceSymbol]   NVARCHAR (30)  NULL,
    [PricesFrom]    DATE           NULL,
    [UpdatedAt]     DATETIME2 (0)  NOT NULL CONSTRAINT [DF_Instrument_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_Instrument] PRIMARY KEY CLUSTERED ([InstrumentID] ASC),
    CONSTRAINT [UQ_Instrument_InstrumentKey] UNIQUE NONCLUSTERED ([InstrumentKey] ASC)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Shares, ETFs and the rest, shared by every account. InstrumentKey is the ISIN (or "T212:" + the ticker when there is none). Ticker is the Trading 212 ticker (AAPL_US_EQ) or the plain symbol of a CSV export; Symbol is what the screens show. PriceSymbol is the Yahoo Finance symbol daily prices come from ('''' = looked up, none found; NULL = not looked up yet).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Instrument';
