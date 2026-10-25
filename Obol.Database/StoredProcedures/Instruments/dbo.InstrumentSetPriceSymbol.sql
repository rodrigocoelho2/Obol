/*
    InstrumentSetPriceSymbol
    Records the Yahoo Finance symbol of an instrument ('' = none found) and the first day its prices go back to.
*/
CREATE PROCEDURE [dbo].[InstrumentSetPriceSymbol]
    @InstrumentID INT,
    @PriceSymbol  NVARCHAR (30),
    @PricesFrom   DATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Instrument]
    SET [PriceSymbol] = @PriceSymbol,
        [PricesFrom] = ISNULL(@PricesFrom, [PricesFrom])
    WHERE [InstrumentID] = @InstrumentID;
END
