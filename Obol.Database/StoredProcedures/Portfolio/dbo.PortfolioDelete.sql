/*
    PortfolioDelete
    Deletes everything imported for an account (positions, trades, dividends, cash, snapshots and the sync log), so it can
    be imported again from scratch. The account and its Trading 212 connection stay; the next sync reads the whole history.
*/
CREATE PROCEDURE [dbo].[PortfolioDelete]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[Holding] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[Trade] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[Dividend] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[CashMovement] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[AccountSnapshot] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[SyncRun] WHERE [UserID] = @UserID;
    UPDATE [dbo].[BrokerConnection] SET [HistoryDoneAt] = NULL, [State] = 0, [LastMessage] = N'' WHERE [UserID] = @UserID;

    COMMIT TRANSACTION;
END
