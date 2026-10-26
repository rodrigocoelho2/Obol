/*
    PieReplace
    Replaces the Trading 212 pies of an account with what the sync just read, as JSON:
    @Pies     [{"id":1234,"n":"Dividends","ic":"Coins","c":0.42,"iv":1000,"v":1080.5,"r":80.5,"d":12.3,"g":5000,"o":"2024-01-02T10:00:00"}, …]
    @Holdings [{"p":1234,"i":12,"q":3.2,"iv":300,"v":320.4,"r":20.4,"cs":0.3,"es":0.3}, …]
    (id / p the Trading 212 pie id, n name, ic icon, c cash, iv invested, v value, r result, d dividends gained, g goal,
    o created at UTC; i instrument id, q shares, cs and es the share of the pie now and as set). Empty arrays remove every pie.
*/
CREATE PROCEDURE [dbo].[PieReplace]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Pies         NVARCHAR (MAX),
    @Holdings     NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[PieHolding] WHERE [UserID] = @UserID;
    DELETE FROM [dbo].[Pie] WHERE [UserID] = @UserID;

    INSERT INTO [dbo].[Pie] ([UserID], [PieID], [Name], [Icon], [Cash], [Invested], [Value], [Result], [DividendsGained], [Goal], [CreatedAt], [UpdatedAt])
    SELECT @UserID, [PieID], LEFT(ISNULL([Name], N''), 100), LEFT(ISNULL([Icon], N''), 40), ISNULL([Cash], 0), ISNULL([Invested], 0), ISNULL([Value], 0),
           ISNULL([Result], 0), ISNULL([DividendsGained], 0), [Goal], [CreatedAt], SYSUTCDATETIME()
    FROM (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY [PieID] ORDER BY (SELECT NULL)) AS [N]
        FROM OPENJSON(@Pies) WITH (
            [PieID] BIGINT '$.id', [Name] NVARCHAR (200) '$.n', [Icon] NVARCHAR (80) '$.ic', [Cash] DECIMAL (18, 2) '$.c',
            [Invested] DECIMAL (18, 2) '$.iv', [Value] DECIMAL (18, 2) '$.v', [Result] DECIMAL (18, 2) '$.r',
            [DividendsGained] DECIMAL (18, 2) '$.d', [Goal] DECIMAL (18, 2) '$.g', [CreatedAt] DATETIME2 (0) '$.o')
        WHERE [PieID] IS NOT NULL
    ) p
    WHERE p.[N] = 1;

    INSERT INTO [dbo].[PieHolding] ([UserID], [PieID], [InstrumentID], [Quantity], [Invested], [Value], [Result], [CurrentShare], [ExpectedShare])
    SELECT @UserID, h.[PieID], h.[InstrumentID], SUM(h.[Quantity]), SUM(ISNULL(h.[Invested], 0)), SUM(ISNULL(h.[Value], 0)), SUM(ISNULL(h.[Result], 0)),
           MAX(h.[CurrentShare]), MAX(h.[ExpectedShare])
    FROM OPENJSON(@Holdings) WITH (
        [PieID] BIGINT '$.p', [InstrumentID] INT '$.i', [Quantity] DECIMAL (28, 10) '$.q', [Invested] DECIMAL (18, 2) '$.iv',
        [Value] DECIMAL (18, 2) '$.v', [Result] DECIMAL (18, 2) '$.r', [CurrentShare] DECIMAL (9, 6) '$.cs', [ExpectedShare] DECIMAL (9, 6) '$.es') h
    WHERE h.[InstrumentID] IS NOT NULL
      AND EXISTS (SELECT 1 FROM [dbo].[Pie] p WHERE p.[UserID] = @UserID AND p.[PieID] = h.[PieID])
    GROUP BY h.[PieID], h.[InstrumentID];

    COMMIT TRANSACTION;
END
