/*
    FxDayList
    Every exchange rate into a currency from a day on, oldest first.
*/
CREATE PROCEDURE [dbo].[FxDayList]
    @Quote CHAR (3),
    @From  DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT f.[Currency],
           f.[Day],
           f.[Rate]
    FROM [dbo].[FxDay] f
    WHERE f.[Quote] = @Quote
      AND f.[Day] >= @From
    ORDER BY f.[Currency], f.[Day];
END
