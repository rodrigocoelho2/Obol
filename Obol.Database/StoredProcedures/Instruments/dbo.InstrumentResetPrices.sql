/*
    InstrumentResetPrices
    Forgets the Yahoo Finance listing of an instrument (its prices didn't match Trading 212's): clears its symbol and the
    day its prices go back to, and deletes the closes that came from Yahoo. The closes from the syncs stay.
*/
CREATE PROCEDURE [dbo].[InstrumentResetPrices]
    @InstrumentID INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DELETE FROM [dbo].[PriceDay] WHERE [InstrumentID] = @InstrumentID AND [Source] = 4;

    UPDATE [dbo].[Instrument]
    SET [PriceSymbol] = NULL,
        [PricesFrom] = NULL
    WHERE [InstrumentID] = @InstrumentID;

    COMMIT TRANSACTION;
END
