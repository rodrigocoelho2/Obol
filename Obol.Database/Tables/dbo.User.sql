CREATE TABLE [dbo].[User]
(
    [UserID]        BIGINT           IDENTITY (1, 1) NOT NULL,
    [UserUniqueID]  UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_User_UserUniqueID] DEFAULT (NEWID()),
    [Username]      NVARCHAR (32)    COLLATE Latin1_General_CI_AS NOT NULL,
    [PasswordHash]  NVARCHAR (200)   NOT NULL,
    [IsAdmin]       BIT              NOT NULL CONSTRAINT [DF_User_IsAdmin] DEFAULT (0),
    [SecurityStamp] NVARCHAR (64)    NOT NULL,
    [CreatedAt]     DATETIME2 (3)    NOT NULL CONSTRAINT [DF_User_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    [LastLoginAt]   DATETIME2 (3)    NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([UserID] ASC),
    CONSTRAINT [UQ_User_UserUniqueID] UNIQUE NONCLUSTERED ([UserUniqueID] ASC),
    CONSTRAINT [UQ_User_Username] UNIQUE NONCLUSTERED ([Username] ASC)
);
GO

EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Accounts. Usernames are unique ignoring case; the first account is the admin. Every row of portfolio data belongs to one account.',
    @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'User';
