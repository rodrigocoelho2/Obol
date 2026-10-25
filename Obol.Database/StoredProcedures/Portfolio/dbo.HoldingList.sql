/*
    HoldingList
    The open positions of an account.
*/
CREATE PROCEDURE [dbo].[HoldingList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT h.[InstrumentID],
           h.[Quantity],
           h.[AveragePrice],
           h.[CurrentPrice],
           h.[Value],
           h.[Cost],
           h.[UnrealizedPL],
           h.[FxImpact],
           h.[OpenedAt],
           h.[UpdatedAt]
    FROM [dbo].[Holding] h
    INNER JOIN [dbo].[User] u ON u.[UserID] = h.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
