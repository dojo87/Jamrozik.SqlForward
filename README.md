# Jamrozik.SqlForward

The DatabaseMigration class executes SQL scripts in a certain location accordingly to their file name order one by one just like migrations would be executed on a CodeFirst solution. So it is a Database First automatic migrations solution.

The usage of DatabaseMigration is as follows:
- Specify SqlForward.DatabaseScripts App Setting as the folder where scripts will be held. You can use virtual ~ tilda in Web environments.
- Specify SqlForward.Initialization App Setting to the SQL file containing the initial database setup (e.g. Initialization.sql).
- Initialize the migrator with a factory function initializing a IDbConnection instance representing the database. 
- Run the Synchronize method. Try-Catch if you want to react to migration errors. You can also control the process by adding an event handler to Changed event.

# The app.config/web.config App Settings

```XML
    <add key="SqlForward.DatabaseScripts" value="./DatabaseMigration"/> <!-- Also ~/ virtual directories in Web environments are possible -->
    <add key="SqlForward.Initialization" value="Initialization.sql"/>
```

# The Initialization.sql
I plan to make the ScriptLog table a little more flexible, so the initialization SQL is given as an SQL file. It should be in the DatabaseMigration folder

```SQL
IF (NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND  TABLE_NAME = 'ScriptLog'))
CREATE TABLE [dbo].[ScriptLog](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ScriptName] [nvarchar](200) NULL,
	[ScriptDate] [datetime] NULL,
	[Status] [nvarchar](200) NULL,
	[DomainUser] [nvarchar](200) NULL,
 CONSTRAINT [PK_ScriptLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

IF NOT EXISTS (SELECT * FROM dbo.ScriptLog WHERE ScriptName = 'Initialization.sql')
BEGIN
INSERT INTO ScriptLog (ScriptName, ScriptDate, Status, DomainUser) VALUES ('Initialization.sql',GETDATE(),'Done','NONE');
END
```

# Using it

```csharp

DatabaseMigration migration = new DatabaseMigration(() => 
  new System.Data.SqlClient.SqlConnection(
       ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString));
       
migration.Changed += (o, e) => System.Console.WriteLine(e.Message); // logging some progress.

migration.Synchronize(); // migrate all pending migrations.
```
