/*
    InstrumentSetProfile
    Records the sector and industry of an instrument ('' = looked up, none found).
*/
CREATE PROCEDURE [dbo].[InstrumentSetProfile]
    @InstrumentID INT,
    @Sector       NVARCHAR (60),
    @Industry     NVARCHAR (80)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Instrument]
    SET [Sector] = @Sector,
        [Industry] = @Industry
    WHERE [InstrumentID] = @InstrumentID;
END
