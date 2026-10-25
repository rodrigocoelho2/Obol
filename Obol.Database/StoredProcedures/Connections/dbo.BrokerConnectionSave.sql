/*
    BrokerConnectionSave
    Saves (or replaces) the encrypted Trading 212 key and secret of an account. A new key starts with a fresh state, so
    the next sync reads the whole history again; what to sync is kept.
*/
CREATE PROCEDURE [dbo].[BrokerConnectionSave]
    @UserUniqueID    UNIQUEIDENTIFIER,
    @Environment     NVARCHAR (8),
    @ApiKeyCipher    NVARCHAR (1000),
    @ApiSecretCipher NVARCHAR (1000),
    @KeyHint         NVARCHAR (12),
    @AccountId       BIGINT,
    @Currency        CHAR (3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    MERGE [dbo].[BrokerConnection] WITH (HOLDLOCK) AS target
    USING (SELECT @UserID AS [UserID]) AS source
    ON target.[UserID] = source.[UserID]
    WHEN MATCHED THEN
        UPDATE SET [Environment] = @Environment, [ApiKeyCipher] = @ApiKeyCipher, [ApiSecretCipher] = @ApiSecretCipher,
                   [KeyHint] = @KeyHint, [AccountId] = @AccountId, [Currency] = @Currency, [State] = 0,
                   [LastMessage] = N'', [HistoryDoneAt] = NULL
    WHEN NOT MATCHED THEN
        INSERT ([UserID], [Environment], [ApiKeyCipher], [ApiSecretCipher], [KeyHint], [AccountId], [Currency])
        VALUES (@UserID, @Environment, @ApiKeyCipher, @ApiSecretCipher, @KeyHint, @AccountId, @Currency);
END
