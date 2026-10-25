/*
    BrokerConnectionList
    Every Trading 212 connection (the background sync goes through them).
*/
CREATE PROCEDURE [dbo].[BrokerConnectionList]
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
    ORDER BY c.[UserID];
END
