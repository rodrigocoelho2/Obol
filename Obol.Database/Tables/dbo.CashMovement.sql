CREATE TABLE [dbo].[CashMovement]
(
    [CashMovementID] BIGINT          IDENTITY (1, 1) NOT NULL,
    [UserID]         BIGINT          NOT NULL,
    [MovementKey]    NVARCHAR (100)  NOT NULL,
    [Kind]           TINYINT         NOT NULL,
    [Amount]         DECIMAL (18, 2) NOT NULL,
    [At]             DATETIME2 (0)   NOT NULL,
    [Note]           NVARCHAR (100)  NOT NULL CONSTRAINT [DF_CashMovement_Note] DEFAULT (N''),
    [Source]         TINYINT         NOT NULL,
    CONSTRAINT [PK_CashMovement] PRIMARY KEY CLUSTERED ([CashMovementID] ASC),
    CONSTRAINT [UQ_CashMovement_UserKey] UNIQUE NONCLUSTERED ([UserID] ASC, [MovementKey] ASC),
    CONSTRAINT [FK_CashMovement_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([UserID]) ON DELETE CASCADE,
    CONSTRAINT [CK_CashMovement_Kind] CHECK ([Kind] BETWEEN 1 AND 5)
);
GO

CREATE NONCLUSTERED INDEX [IX_CashMovement_UserAt] ON [dbo].[CashMovement] ([UserID] ASC, [At] ASC);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Deposits (1), withdrawals (2), interest (3), fees (4) and other cash (5) of an account, in the account currency: positive in, negative out. At in UTC.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'CashMovement';
