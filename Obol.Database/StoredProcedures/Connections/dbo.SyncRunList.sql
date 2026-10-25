/*
    SyncRunList
    The last syncs and imports of an account, newest first.
*/
CREATE PROCEDURE [dbo].[SyncRunList]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Top          INT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT TOP (@Top)
           r.[SyncRunID],
           r.[Source],
           r.[StartedAt],
           r.[FinishedAt],
           r.[Ok],
           r.[Summary],
           r.[Lines]
    FROM [dbo].[SyncRun] r
    INNER JOIN [dbo].[User] u ON u.[UserID] = r.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY r.[SyncRunID] DESC;
END
