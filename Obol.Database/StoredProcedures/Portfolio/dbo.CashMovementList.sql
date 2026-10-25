/*
    CashMovementList
    Every deposit, withdrawal, interest payment and fee of an account, oldest first.
*/
CREATE PROCEDURE [dbo].[CashMovementList]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT c.[CashMovementID],
           c.[MovementKey],
           c.[Kind],
           c.[Amount],
           c.[At],
           c.[Note],
           c.[Source]
    FROM [dbo].[CashMovement] c
    INNER JOIN [dbo].[User] u ON u.[UserID] = c.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID
    ORDER BY c.[At], c.[CashMovementID];
END
