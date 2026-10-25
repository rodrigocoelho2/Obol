/*
    UserCount
    How many accounts there are (none: the first account to be created becomes the admin).
*/
CREATE PROCEDURE [dbo].[UserCount]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) FROM [dbo].[User];
END
