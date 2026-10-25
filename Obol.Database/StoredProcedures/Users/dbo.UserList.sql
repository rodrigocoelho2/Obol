/*
    UserList
    Every account, oldest first.
*/
CREATE PROCEDURE [dbo].[UserList]
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
    ORDER BY u.[UserID];
END
