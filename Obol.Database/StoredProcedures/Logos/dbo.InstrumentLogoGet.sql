/*
    InstrumentLogoGet
    The logo kept for an instrument (no row when it was never fetched).
*/
CREATE PROCEDURE [dbo].[InstrumentLogoGet]
    @InstrumentKey NVARCHAR (40)
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    SELECT l.[InstrumentKey],
           l.[ContentType],
           l.[Data],
           l.[FetchedAt]
    FROM [dbo].[InstrumentLogo] l
    WHERE l.[InstrumentKey] = @InstrumentKey;
END
