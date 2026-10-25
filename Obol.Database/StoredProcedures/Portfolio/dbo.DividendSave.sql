/*
    DividendSave
    Adds dividends, as JSON: [{"i":7,"k":"IE…|20260924|28.30","p":"2026-09-24T10:12:00","q":31.5,"ps":0.4512,"a":28.3,"w":null,
    "kd":"ORDINARY","s":1}, …] (i instrument id, k key, p paid at UTC, q quantity, ps per share, a amount, w withheld, kd kind, s source).
    One already kept (same key) is left as it is. Returns how many were new.
*/
CREATE PROCEDURE [dbo].[DividendSave]
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

    INSERT INTO [dbo].[Dividend] ([UserID], [InstrumentID], [DividendKey], [PaidAt], [Quantity], [PerShare], [Amount], [Withheld], [Kind], [Source])
    SELECT @UserID, s.[InstrumentID], s.[DividendKey], s.[PaidAt], s.[Quantity], s.[PerShare], s.[Amount], s.[Withheld], ISNULL(s.[Kind], N''), s.[Source]
    FROM (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY [DividendKey] ORDER BY (SELECT NULL)) AS [Copy]
        FROM OPENJSON(@Rows) WITH (
            [InstrumentID] INT '$.i', [DividendKey] NVARCHAR (100) '$.k', [PaidAt] DATETIME2 (0) '$.p', [Quantity] DECIMAL (28, 10) '$.q',
            [PerShare] DECIMAL (18, 8) '$.ps', [Amount] DECIMAL (18, 2) '$.a', [Withheld] DECIMAL (18, 2) '$.w', [Kind] NVARCHAR (60) '$.kd',
            [Source] TINYINT '$.s')
    ) AS s
    WHERE s.[Copy] = 1
      AND s.[DividendKey] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Dividend] d WITH (UPDLOCK, HOLDLOCK) WHERE d.[UserID] = @UserID AND d.[DividendKey] = s.[DividendKey]);

    SELECT @@ROWCOUNT;
END
