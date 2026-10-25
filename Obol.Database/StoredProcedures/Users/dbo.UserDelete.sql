/*
    UserDelete
    Deletes an account and, through the cascading foreign keys, all of its portfolio data.
*/
CREATE PROCEDURE [dbo].[UserDelete]
    @UserUniqueID UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM [dbo].[User]
    WHERE [UserUniqueID] = @UserUniqueID;
END
