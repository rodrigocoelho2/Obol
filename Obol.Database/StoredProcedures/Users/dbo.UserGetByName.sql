/*
    UserGetByName
    One account by its user name (ignoring case).
*/
CREATE PROCEDURE [dbo].[UserGetByName]
    @Username NVARCHAR (32)
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
    WHERE u.[Username] = @Username;
END
