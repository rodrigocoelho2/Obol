/*
    AccountSnapshotList
    The daily account snapshots of an account, oldest first.
*/
CREATE PROCEDURE [dbo].[AccountSnapshotList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT s.[Day],
           s.[TotalValue],
           s.[Cash],
           s.[Cost],
           s.[UnrealizedPL],
           s.[RealizedPL]
    FROM [dbo].[AccountSnapshot] s
    INNER JOIN [dbo].[User] u ON u.[UserID] = s.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY s.[Day];
END
