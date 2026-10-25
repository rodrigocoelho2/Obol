/*
    TradeList
    Every trade of an account, oldest first.
*/
CREATE PROCEDURE [dbo].[TradeList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT t.[TradeID],
           t.[InstrumentID],
           t.[TradeKey],
           t.[Side],
           t.[Quantity],
           t.[Price],
           t.[PriceCurrency],
           t.[FxRate],
           t.[Total],
           t.[RealizedPL],
           t.[Fees],
           t.[ExecutedAt],
           t.[Source],
           t.[ExternalId]
    FROM [dbo].[Trade] t
    INNER JOIN [dbo].[User] u ON u.[UserID] = t.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY t.[ExecutedAt], t.[TradeID];
END
