/*
    TradeSave
    Adds trades, as JSON: [{"i":12,"k":"US…|20261001080001|1.87|1","sd":1,"q":1.87,"p":106.9,"pc":"EUR","fx":1,"t":200,"r":null,"f":0,
    "x":"2026-10-01T08:00:01","s":1,"e":"39484001"}, …] (i instrument id, k trade key, sd side, q quantity, p price, pc price currency,
    fx rate, t total, r realised P/L, f fees, x executed at UTC, s source, e id at the broker).
    A trade already kept (same key) is left as it is. Returns how many were new.
*/
CREATE PROCEDURE [dbo].[TradeSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL
    BEGIN
        SELECT 0;
        RETURN;
    END

    INSERT INTO [dbo].[Trade] ([UserID], [InstrumentID], [TradeKey], [Side], [Quantity], [Price], [PriceCurrency], [FxRate], [Total], [RealizedPL], [Fees], [ExecutedAt], [Source], [ExternalId])
    SELECT @UserID, s.[InstrumentID], s.[TradeKey], s.[Side], s.[Quantity], s.[Price], s.[PriceCurrency], s.[FxRate], s.[Total], s.[RealizedPL], ISNULL(s.[Fees], 0), s.[ExecutedAt], s.[Source], s.[ExternalId]
    FROM (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY [TradeKey] ORDER BY (SELECT NULL)) AS [Copy]
        FROM OPENJSON(@Rows) WITH (
            [InstrumentID] INT '$.i', [TradeKey] NVARCHAR (100) '$.k', [Side] TINYINT '$.sd', [Quantity] DECIMAL (28, 10) '$.q',
            [Price] DECIMAL (28, 10) '$.p', [PriceCurrency] CHAR (3) '$.pc', [FxRate] DECIMAL (18, 8) '$.fx', [Total] DECIMAL (18, 2) '$.t',
            [RealizedPL] DECIMAL (18, 2) '$.r', [Fees] DECIMAL (18, 2) '$.f', [ExecutedAt] DATETIME2 (0) '$.x', [Source] TINYINT '$.s',
            [ExternalId] NVARCHAR (60) '$.e')
    ) AS s
    WHERE s.[Copy] = 1
      AND s.[TradeKey] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Trade] t WITH (UPDLOCK, HOLDLOCK) WHERE t.[UserID] = @UserID AND t.[TradeKey] = s.[TradeKey]);

    SELECT @@ROWCOUNT;
END
