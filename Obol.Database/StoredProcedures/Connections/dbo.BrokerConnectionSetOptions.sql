/*
    BrokerConnectionSetOptions
    What the sync reads: positions and cash, orders, dividends, deposits and withdrawals.
*/
CREATE PROCEDURE [dbo].[BrokerConnectionSetOptions]
    @UserUniqueID     UNIQUEIDENTIFIER,
    @SyncPositions    BIT,
    @SyncOrders       BIT,
    @SyncDividends    BIT,
    @SyncTransactions BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE c
    SET [SyncPositions] = @SyncPositions,
        [SyncOrders] = @SyncOrders,
        [SyncDividends] = @SyncDividends,
        [SyncTransactions] = @SyncTransactions
    FROM [dbo].[BrokerConnection] c
    INNER JOIN [dbo].[User] u ON u.[UserID] = c.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
