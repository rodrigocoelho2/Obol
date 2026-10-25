/*
    CashMovementSave
    Adds deposits, withdrawals, interest and fees, as JSON: [{"k":"1|20260901090000|200.00","kd":1,"a":200,"at":"2026-09-01T09:00:00",
    "n":"Bank transfer","s":1}, …] (k key, kd kind, a signed amount, at UTC, n note, s source).
    One already kept (same key) is left as it is. Returns how many were new.
*/
CREATE PROCEDURE [dbo].[CashMovementSave]
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

    INSERT INTO [dbo].[CashMovement] ([UserID], [MovementKey], [Kind], [Amount], [At], [Note], [Source])
    SELECT @UserID, s.[MovementKey], s.[Kind], s.[Amount], s.[At], LEFT(ISNULL(s.[Note], N''), 100), s.[Source]
    FROM (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY [MovementKey] ORDER BY (SELECT NULL)) AS [Copy]
        FROM OPENJSON(@Rows) WITH (
            [MovementKey] NVARCHAR (100) '$.k', [Kind] TINYINT '$.kd', [Amount] DECIMAL (18, 2) '$.a', [At] DATETIME2 (0) '$.at',
            [Note] NVARCHAR (200) '$.n', [Source] TINYINT '$.s')
    ) AS s
    WHERE s.[Copy] = 1
      AND s.[MovementKey] IS NOT NULL
      AND s.[Kind] BETWEEN 1 AND 5
      AND NOT EXISTS (SELECT 1 FROM [dbo].[CashMovement] c WITH (UPDLOCK, HOLDLOCK) WHERE c.[UserID] = @UserID AND c.[MovementKey] = s.[MovementKey]);

    SELECT @@ROWCOUNT;
END
