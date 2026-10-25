/*
    HoldingReplace
    Replaces the open positions of an account with what Trading 212 just reported, as JSON:
    [{"i":12,"q":42.18,"ap":88.4,"cp":104.62,"v":4412.87,"c":3728.71,"u":684.16,"fx":-2.1,"o":"2024-01-02T10:00:00"}, …]
    (i instrument id, q quantity, ap average price, cp current price, v value, c cost, u unrealised P/L, fx FX impact,
    o opened at UTC). An empty array closes every position.
*/
CREATE PROCEDURE [dbo].[HoldingReplace]
    @UserUniqueID UNIQUEIDENTIFIER,
    @Rows         NVARCHAR (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserID BIGINT = (SELECT [UserID] FROM [dbo].[User] WHERE [UserUniqueID] = @UserUniqueID);
    IF @UserID IS NULL RETURN;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[Holding] WHERE [UserID] = @UserID;

    INSERT INTO [dbo].[Holding] ([UserID], [InstrumentID], [Quantity], [AveragePrice], [CurrentPrice], [Value], [Cost], [UnrealizedPL], [FxImpact], [OpenedAt], [UpdatedAt])
    SELECT @UserID, [InstrumentID], SUM([Quantity]), MAX([AveragePrice]), MAX([CurrentPrice]), SUM([Value]), SUM([Cost]), SUM([UnrealizedPL]), SUM([FxImpact]), MIN([OpenedAt]), SYSUTCDATETIME()
    FROM OPENJSON(@Rows) WITH (
        [InstrumentID] INT '$.i', [Quantity] DECIMAL (28, 10) '$.q', [AveragePrice] DECIMAL (28, 10) '$.ap', [CurrentPrice] DECIMAL (28, 10) '$.cp',
        [Value] DECIMAL (18, 2) '$.v', [Cost] DECIMAL (18, 2) '$.c', [UnrealizedPL] DECIMAL (18, 2) '$.u', [FxImpact] DECIMAL (18, 2) '$.fx',
        [OpenedAt] DATETIME2 (0) '$.o')
    WHERE [InstrumentID] IS NOT NULL AND [Quantity] > 0
    GROUP BY [InstrumentID];

    COMMIT TRANSACTION;
END
