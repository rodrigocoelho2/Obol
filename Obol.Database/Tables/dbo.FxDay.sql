CREATE TABLE [dbo].[FxDay]
(
    [Currency] CHAR (3)        NOT NULL,
    [Quote]    CHAR (3)        NOT NULL,
    [Day]      DATE            NOT NULL,
    [Rate]     DECIMAL (18, 8) NOT NULL,
    CONSTRAINT [PK_FxDay] PRIMARY KEY CLUSTERED ([Currency] ASC, [Quote] ASC, [Day] ASC)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Exchange rates at the end of each day: one unit of Currency is Rate units of Quote (the account currency). Shared by every account.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'FxDay';
