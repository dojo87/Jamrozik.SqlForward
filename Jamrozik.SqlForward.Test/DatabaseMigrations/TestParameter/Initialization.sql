IF (NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND  TABLE_NAME = 'ScriptLog'))
CREATE TABLE [dbo].[ScriptLog](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ScriptName] [nvarchar](200) NULL,
	[ScriptDate] [datetime] NULL,
	[Status] [nvarchar](200) NULL,
	[DomainUser] [nvarchar](200) NULL,
	[Application] [nvarchar](200) NULL,
 CONSTRAINT [PK_ScriptLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

IF NOT EXISTS (SELECT * FROM dbo.ScriptLog WHERE ScriptName = 'Initialization.sql')
BEGIN
INSERT INTO ScriptLog (ScriptName, ScriptDate, Status, DomainUser, Application) VALUES ('Initialization.sql',GETDATE(),'Done',@user, @TestApplicationName);
END