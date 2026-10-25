/*
    BrokerConnectionDelete
    Forgets the Trading 212 key and secret of an account. The portfolio already imported stays.
*/
CREATE PROCEDURE [dbo].[BrokerConnectionDelete]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE c
    FROM [dbo].[BrokerConnection] c
    INNER JOIN [dbo].[User] u ON u.[UserID] = c.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
