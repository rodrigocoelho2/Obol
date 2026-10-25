/*
    FxDaySave
    Saves exchange rates, as JSON: [{"c":"USD","q":"EUR","d":"2026-10-23","r":0.92145}, …] (one c is r q). A day already kept is updated.
*/
CREATE PROCEDURE [dbo].[FxDaySave]
    @Rows NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    MERGE [dbo].[FxDay] WITH (HOLDLOCK) AS target
    USING (
        SELECT [Currency], [Quote], [Day], MAX([Rate]) AS [Rate]
        FROM OPENJSON(@Rows) WITH ([Currency] CHAR (3) '$.c', [Quote] CHAR (3) '$.q', [Day] DATE '$.d', [Rate] DECIMAL (18, 8) '$.r')
        WHERE [Currency] IS NOT NULL AND [Quote] IS NOT NULL AND [Day] IS NOT NULL AND [Rate] > 0
        GROUP BY [Currency], [Quote], [Day]
    ) AS source
    ON target.[Currency] = source.[Currency] AND target.[Quote] = source.[Quote] AND target.[Day] = source.[Day]
    WHEN MATCHED THEN
        UPDATE SET [Rate] = source.[Rate]
    WHEN NOT MATCHED THEN
        INSERT ([Currency], [Quote], [Day], [Rate])
        VALUES (source.[Currency], source.[Quote], source.[Day], source.[Rate]);
END
