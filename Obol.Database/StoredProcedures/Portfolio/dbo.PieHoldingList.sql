/*
    PieHoldingList
    The instruments of every Trading 212 pie of an account.
*/
CREATE PROCEDURE [dbo].[PieHoldingList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT h.[PieID],
           h.[InstrumentID],
           h.[Quantity],
           h.[Invested],
           h.[Value],
           h.[Result],
           h.[CurrentShare],
           h.[ExpectedShare]
    FROM [dbo].[PieHolding] h
    INNER JOIN [dbo].[User] u ON u.[UserID] = h.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
