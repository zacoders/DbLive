
-- ****************************************
-- Create Error Log objects
-- ****************************************

-- Create table to store error information
CREATE TABLE [dbo].[ErrorLog](
    [ErrorLogID] [int] IDENTITY (1, 1) NOT NULL,
    [ErrorTime] [datetime] NOT NULL CONSTRAINT [DF_ErrorLog_ErrorTime] DEFAULT (GETDATE()),
    [UserName] [sysname] NOT NULL,
    [ErrorNumber] [int] NOT NULL,
    [ErrorSeverity] [int] NULL,
    [ErrorState] [int] NULL,
    [ErrorProcedure] [nvarchar](126) NULL,
    [ErrorLine] [int] NULL,
    [ErrorMessage] [nvarchar](4000) NOT NULL
) ON [PRIMARY];
GO

ALTER TABLE [dbo].[ErrorLog] WITH CHECK ADD
    CONSTRAINT [PK_ErrorLog_ErrorLogID] PRIMARY KEY CLUSTERED
    (
        [ErrorLogID]
    )  ON [PRIMARY];
GO
