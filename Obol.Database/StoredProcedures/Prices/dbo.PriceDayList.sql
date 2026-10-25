/*
    PriceDayList
    The closing prices of some instruments (@InstrumentIDs: a JSON array of ids) from a day on, oldest first.
*/
CREATE PROCEDURE [dbo].[PriceDayList]
    @InstrumentIDs NVARCHAR (MAX),
    @From          DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT p.[InstrumentID],
           p.[Day],
           p.[Close]
    FROM [dbo].[PriceDay] p
    WHERE p.[InstrumentID] IN (SELECT CAST([value] AS INT) FROM OPENJSON(@InstrumentIDs))
      AND p.[Day] >= @From
    ORDER BY p.[InstrumentID], p.[Day];
END
