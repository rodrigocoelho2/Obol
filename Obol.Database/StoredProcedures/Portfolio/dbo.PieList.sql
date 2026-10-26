/*
    PieList
    The Trading 212 pies of an account.
*/
CREATE PROCEDURE [dbo].[PieList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT p.[PieID],
           p.[Name],
           p.[Icon],
           p.[Cash],
           p.[Invested],
           p.[Value],
           p.[Result],
           p.[DividendsGained],
           p.[Goal],
           p.[CreatedAt],
           p.[UpdatedAt]
    FROM [dbo].[Pie] p
    INNER JOIN [dbo].[User] u ON u.[UserID] = p.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
