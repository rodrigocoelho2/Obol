/*
    PriceDaySave
    Saves closing prices, as JSON: [{"i":12,"d":"2026-10-23","c":104.62,"s":4}, …] (i instrument id, d day, c close in the
    instrument currency, s source). A day already kept is updated.
*/
CREATE PROCEDURE [dbo].[PriceDaySave]
    @Rows NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    MERGE [dbo].[PriceDay] WITH (HOLDLOCK) AS target
    USING (
        SELECT [InstrumentID], [Day], MAX([Close]) AS [Close], MAX([Source]) AS [Source]
        FROM OPENJSON(@Rows) WITH ([InstrumentID] INT '$.i', [Day] DATE '$.d', [Close] DECIMAL (28, 10) '$.c', [Source] TINYINT '$.s')
        WHERE [InstrumentID] IS NOT NULL AND [Day] IS NOT NULL AND [Close] > 0
        GROUP BY [InstrumentID], [Day]
    ) AS source
    ON target.[InstrumentID] = source.[InstrumentID] AND target.[Day] = source.[Day]
    WHEN MATCHED THEN
        UPDATE SET [Close] = source.[Close], [Source] = source.[Source]
    WHEN NOT MATCHED THEN
        INSERT ([InstrumentID], [Day], [Close], [Source])
        VALUES (source.[InstrumentID], source.[Day], source.[Close], source.[Source]);
END
