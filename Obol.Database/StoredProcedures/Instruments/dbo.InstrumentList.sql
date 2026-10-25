/*
    InstrumentList
    Every instrument (a few hundred at most: only what someone holds or has held).
*/
CREATE PROCEDURE [dbo].[InstrumentList]
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT i.[InstrumentID],
           i.[InstrumentKey],
           i.[Isin],
           i.[Ticker],
           i.[Symbol],
           i.[Name],
           i.[Currency],
           i.[AssetClass],
           i.[PriceSymbol],
           i.[PricesFrom]
    FROM [dbo].[Instrument] i
    ORDER BY i.[InstrumentID];
END
