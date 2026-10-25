/*
    BrokerConnectionGet
    The Trading 212 connection of an account (no row when it has none).
*/
CREATE PROCEDURE [dbo].[BrokerConnectionGet]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT u.[UserUniqueID],
           c.[Environment],
           c.[ApiKeyCipher],
           c.[ApiSecretCipher],
           c.[KeyHint],
           c.[AccountId],
           c.[Currency],
           c.[SyncPositions],
           c.[SyncOrders],
           c.[SyncDividends],
           c.[SyncTransactions],
           c.[State],
           c.[LastMessage],
           c.[LastSyncAt],
           c.[HistoryDoneAt],
           c.[CreatedAt]
    FROM [dbo].[BrokerConnection] c
    INNER JOIN [dbo].[User] u ON u.[UserID] = c.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
