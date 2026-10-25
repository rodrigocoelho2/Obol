/*
    AccountSnapshotSave
    Keeps the account as Trading 212 reported it today (a later sync on the same day replaces it).
*/
CREATE PROCEDURE [dbo].[AccountSnapshotSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Day          DATE,
    @TotalValue   DECIMAL (18, 2),
    @Cash         DECIMAL (18, 2),
    @Cost         DECIMAL (18, 2),
    @UnrealizedPL DECIMAL (18, 2),
    @RealizedPL   DECIMAL (18, 2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[AccountSnapshot] WITH (HOLDLOCK) AS target
    USING (SELECT @UserID AS [UserID], @Day AS [Day]) AS source
    ON target.[UserID] = source.[UserID] AND target.[Day] = source.[Day]
    WHEN MATCHED THEN
        UPDATE SET [TotalValue] = @TotalValue, [Cash] = @Cash, [Cost] = @Cost, [UnrealizedPL] = @UnrealizedPL,
                   [RealizedPL] = @RealizedPL, [UpdatedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [Day], [TotalValue], [Cash], [Cost], [UnrealizedPL], [RealizedPL])
        VALUES (@UserID, @Day, @TotalValue, @Cash, @Cost, @UnrealizedPL, @RealizedPL);
END
