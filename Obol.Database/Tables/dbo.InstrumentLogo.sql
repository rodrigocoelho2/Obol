CREATE TABLE [dbo].[InstrumentLogo]
(
    [InstrumentKey] NVARCHAR (40)   NOT NULL,
    [ContentType]   NVARCHAR (40)   NOT NULL CONSTRAINT [DF_InstrumentLogo_ContentType] DEFAULT (N''),
    [Data]          VARBINARY (MAX) NULL,
    [FetchedAt]     DATETIME2 (0)   NOT NULL,
    CONSTRAINT [PK_InstrumentLogo] PRIMARY KEY CLUSTERED ([InstrumentKey] ASC)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Company and fund logos, fetched once from the logo service and kept so the phone never asks a third party. Data NULL = the service has no logo for it (asked again after a week).',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'InstrumentLogo';
