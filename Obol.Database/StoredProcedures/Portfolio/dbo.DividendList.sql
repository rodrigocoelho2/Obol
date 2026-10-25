/*
    DividendList
    Every dividend of an account, oldest first.
*/
CREATE PROCEDURE [dbo].[DividendList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT d.[DividendID],
           d.[InstrumentID],
           d.[DividendKey],
           d.[PaidAt],
           d.[Quantity],
           d.[PerShare],
           d.[Amount],
           d.[Withheld],
           d.[Kind],
           d.[Source]
    FROM [dbo].[Dividend] d
    INNER JOIN [dbo].[User] u ON u.[UserID] = d.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY d.[PaidAt], d.[DividendID];
END
