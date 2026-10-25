/*
    UserTouchLogin
    Records a successful sign-in.
*/
CREATE PROCEDURE [dbo].[UserTouchLogin]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[User]
    SET [LastLoginAt] = SYSUTCDATETIME()
    WHERE [UserUniqueID] = @UserUniqueID;
END
