/*
    UserGet
    One account by its public identifier.
*/
CREATE PROCEDURE [dbo].[UserGet]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT u.[UserID],
           u.[UserUniqueID],
           u.[Username],
           u.[PasswordHash],
           u.[IsAdmin],
           u.[SecurityStamp],
           u.[CreatedAt],
           u.[LastLoginAt]
    FROM [dbo].[User] u
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
