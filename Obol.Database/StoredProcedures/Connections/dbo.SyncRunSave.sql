/*
    SyncRunSave
    Logs a sync or a CSV import (Lines: a JSON array of strings) and keeps only the last 30 of the account.
*/
CREATE PROCEDURE [dbo].[SyncRunSave]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Source       TINYINT,
    @StartedAt    DATETIME2 (0),
    @FinishedAt   DATETIME2 (0),
    @Ok           BIT,
    @Summary      NVARCHAR (400),
    @Lines        NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    BEGIN TRANSACTION;

    INSERT INTO [dbo].[SyncRun] ([UserID], [Source], [StartedAt], [FinishedAt], [Ok], [Summary], [Lines])
    VALUES (@UserID, @Source, @StartedAt, @FinishedAt, @Ok, LEFT(ISNULL(@Summary, N''), 400), ISNULL(@Lines, N'[]'));

    DELETE FROM [dbo].[SyncRun]
    WHERE [UserID] = @UserID
      AND [SyncRunID] NOT IN (SELECT TOP (30) [SyncRunID] FROM [dbo].[SyncRun] WHERE [UserID] = @UserID ORDER BY [SyncRunID] DESC);

    COMMIT TRANSACTION;
END
