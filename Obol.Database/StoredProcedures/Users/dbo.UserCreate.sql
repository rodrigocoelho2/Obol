/*
    UserCreate
    Creates an account and returns it; returns no row when the user name is taken. The first account becomes the admin.
    (Whether new accounts are allowed at all is decided by the Website before it gets here.)
*/
CREATE PROCEDURE [dbo].[UserCreate]
    @Username      NVARCHAR (32),
    @PasswordHash  NVARCHAR (200),
    @SecurityStamp NVARCHAR (64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

    DECLARE @UserID BIGINT = NULL;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WITH (UPDLOCK, HOLDLOCK) WHERE [Username] = @Username)
    BEGIN
        INSERT INTO [dbo].[User] ([Username], [PasswordHash], [IsAdmin], [SecurityStamp])
        SELECT @Username, @PasswordHash, CASE WHEN EXISTS (SELECT 1 FROM [dbo].[User] WITH (UPDLOCK, HOLDLOCK)) THEN 0 ELSE 1 END, @SecurityStamp;

        SET @UserID = SCOPE_IDENTITY();
    END

    COMMIT TRANSACTION;

    SELECT u.[UserID],
           u.[UserUniqueID],
           u.[Username],
           u.[PasswordHash],
           u.[IsAdmin],
           u.[SecurityStamp],
           u.[CreatedAt],
           u.[LastLoginAt]
    FROM [dbo].[User] u
    WHERE u.[UserID] = @UserID;
END
