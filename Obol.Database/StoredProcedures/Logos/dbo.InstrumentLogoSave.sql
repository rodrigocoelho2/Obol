/*
    InstrumentLogoSave
    Keeps a logo (or, with @Data NULL, that the logo service has none).
*/
CREATE PROCEDURE [dbo].[InstrumentLogoSave]
    @InstrumentKey NVARCHAR (40),
    @ContentType   NVARCHAR (40),
    @Data          VARBINARY (MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    MERGE [dbo].[InstrumentLogo] WITH (HOLDLOCK) AS target
    USING (SELECT @InstrumentKey AS [InstrumentKey]) AS source
    ON target.[InstrumentKey] = source.[InstrumentKey]
    WHEN MATCHED THEN
        UPDATE SET [ContentType] = ISNULL(@ContentType, N''), [Data] = @Data, [FetchedAt] = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT ([InstrumentKey], [ContentType], [Data], [FetchedAt])
        VALUES (@InstrumentKey, ISNULL(@ContentType, N''), @Data, SYSUTCDATETIME());
END
