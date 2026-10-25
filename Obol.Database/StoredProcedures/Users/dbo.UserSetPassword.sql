/*
    UserSetPassword
    Saves a new password hash and security stamp (the new stamp signs out every other device).
*/
CREATE PROCEDURE [dbo].[UserSetPassword]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @PasswordHash  NVARCHAR (200),
    @SecurityStamp NVARCHAR (64)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[User]
    SET [PasswordHash] = @PasswordHash,
        [SecurityStamp] = @SecurityStamp
    WHERE [UserUniqueID] = @UserUniqueID;
END
