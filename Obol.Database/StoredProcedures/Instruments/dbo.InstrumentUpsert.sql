/*
    InstrumentUpsert
    Adds or updates instruments, as JSON: [{"k":"US0378331005","i":"US0378331005","t":"AAPL_US_EQ","s":"AAPL","n":"Apple","c":"USD","a":1}, …]
    (k key, i ISIN, t ticker, s symbol, n name, c currency, a asset class). What the Trading 212 API says (a ticker with
    an underscore) wins over a CSV export; an asset class of 0 (unknown) never replaces a known one.
    Returns the instruments sent, with their ids.
*/
CREATE PROCEDURE [dbo].[InstrumentUpsert]
    @Rows NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Source TABLE (
        [InstrumentKey] NVARCHAR (40) NOT NULL PRIMARY KEY, [Isin] CHAR (12) NULL, [Ticker] NVARCHAR (40) NOT NULL,
        [Symbol] NVARCHAR (20) NOT NULL, [Name] NVARCHAR (200) NOT NULL, [Currency] CHAR (3) NOT NULL, [AssetClass] TINYINT NOT NULL);

    INSERT INTO @Source
    SELECT [InstrumentKey], MAX([Isin]), MAX([Ticker]), MAX([Symbol]), MAX([Name]), MAX([Currency]), MAX([AssetClass])
    FROM OPENJSON(@Rows) WITH (
        [InstrumentKey] NVARCHAR (40) '$.k', [Isin] CHAR (12) '$.i', [Ticker] NVARCHAR (40) '$.t', [Symbol] NVARCHAR (20) '$.s',
        [Name] NVARCHAR (200) '$.n', [Currency] CHAR (3) '$.c', [AssetClass] TINYINT '$.a')
    WHERE [InstrumentKey] IS NOT NULL
    GROUP BY [InstrumentKey];

    MERGE [dbo].[Instrument] WITH (HOLDLOCK) AS target
    USING @Source AS source
    ON target.[InstrumentKey] = source.[InstrumentKey]
    WHEN MATCHED THEN
        UPDATE SET [Isin] = ISNULL(source.[Isin], target.[Isin]),
                   [Ticker] = CASE WHEN CHARINDEX(N'_', source.[Ticker]) > 0 OR CHARINDEX(N'_', target.[Ticker]) = 0 THEN source.[Ticker] ELSE target.[Ticker] END,
                   [Symbol] = CASE WHEN CHARINDEX(N'_', source.[Ticker]) > 0 OR CHARINDEX(N'_', target.[Ticker]) = 0 THEN source.[Symbol] ELSE target.[Symbol] END,
                   [Name] = CASE WHEN CHARINDEX(N'_', source.[Ticker]) > 0 OR CHARINDEX(N'_', target.[Ticker]) = 0 THEN source.[Name] ELSE target.[Name] END,
                   [Currency] = source.[Currency],
                   [AssetClass] = CASE WHEN source.[AssetClass] > 0 THEN source.[AssetClass] ELSE target.[AssetClass] END,
                   [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([InstrumentKey], [Isin], [Ticker], [Symbol], [Name], [Currency], [AssetClass])
        VALUES (source.[InstrumentKey], source.[Isin], source.[Ticker], source.[Symbol], source.[Name], source.[Currency], source.[AssetClass]);

    SELECT i.[InstrumentID],
           i.[InstrumentKey],
           i.[Isin],
           i.[Ticker],
           i.[Symbol],
           i.[Name],
           i.[Currency],
           i.[AssetClass],
           i.[PriceSymbol],
           i.[PricesFrom],
           i.[Sector],
           i.[Industry]
    FROM [dbo].[Instrument] i
    INNER JOIN @Source s ON s.[InstrumentKey] = i.[InstrumentKey];
END
