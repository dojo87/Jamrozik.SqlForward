﻿IF (NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND  TABLE_NAME = 'Mytable'))
CREATE TABLE [dbo].Mytable(
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[GoodTable] [nvarchar](200) NULL
	
 CONSTRAINT [PK_Mytable] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

IF NOT EXISTS (SELECT * FROM dbo.Mytable WHERE GoodTable = 'TEST')
BEGIN
INSERT INTO Mytable (GoodTable) VALUES ('TEST');
INSERT INTO Mytable (GoodTable) VALUES (@user);
END