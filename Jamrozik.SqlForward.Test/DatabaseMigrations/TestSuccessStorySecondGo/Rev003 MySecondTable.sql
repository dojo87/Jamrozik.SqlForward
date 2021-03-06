﻿IF (NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND  TABLE_NAME = 'MySecondTable'))
CREATE TABLE [dbo].MySecondTable(
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[GoodTable] [nvarchar](200) NULL
	
 CONSTRAINT [PK_MySecondTable] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

IF NOT EXISTS (SELECT * FROM dbo.MySecondTable WHERE GoodTable = 'GOOD')
BEGIN
INSERT INTO MySecondTable (GoodTable) VALUES ('GOOD');
END