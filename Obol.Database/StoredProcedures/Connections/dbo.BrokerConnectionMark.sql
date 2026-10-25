/*
    BrokerConnectionMark
    Records how a sync went. NULL leaves LastSyncAt, HistoryDoneAt, AccountId and Currency as they are.
*/
CREATE PROCEDURE [dbo].[BrokerConnectionMark]
    @UserUniqueID  UNIQUEIDENTIFIER,
    @State         TINYINT,
    @LastMessage   NVARCHAR (400),
    @LastSyncAt    DATETIME2 (0),
    @HistoryDoneAt DATETIME2 (0),
    @AccountId     BIGINT,
    @Currency      CHAR (3)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE c
    SET [State] = @State,
        [LastMessage] = LEFT(ISNULL(@LastMessage, N''), 400),
        [LastSyncAt] = ISNULL(@LastSyncAt, c.[LastSyncAt]),
        [HistoryDoneAt] = ISNULL(@HistoryDoneAt, c.[HistoryDoneAt]),
        [AccountId] = ISNULL(@AccountId, c.[AccountId]),
        [Currency] = ISNULL(@Currency, c.[Currency])
    FROM [dbo].[BrokerConnection] c
    INNER JOIN [dbo].[User] u ON u.[UserID] = c.[UserID]
    WHERE u.[UserUniqueID] = @UserUniqueID;
END
