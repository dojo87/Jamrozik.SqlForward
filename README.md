# Jamrozik.SqlForward
Licensed under MIT:
https://opensource.org/licenses/MIT

The tool for Database First Migrations

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

# Passing Parameters
The library is capable of using parametrized scripts.
There are two properties on the DatabaseMigratio class:
- ScriptLogParameters
- ScriptParameters
Both properties are pre-initialized Dictionary types with overloaded .Add methods.
You have to defined the parameter name, the DbType and the factory function that returns the value.

The ScriptLogParameters define parameters which you may pass down to the INSERT script that writes the executed migrations.
Adding those parameters would probably mean, that you also want to edit the DatabaseMigration.ScriptLogInsert property ;)

The ScriptLog Parameters are applied to every migration script that is run.

Lets consider this example:
```csharp
DatabaseMigration migration = Initialize(); // Magic initialization ;) see first examples to see that.

// For parameters in the migration scripts.
migration.ScriptParameters
	.Add("TestApplicationName", DbType.String, 
	(script, parameter) => ConfigurationManager.AppSettings["TestApplicationName"]); 
	
// It will work for an example SQL migration script:
// INSERT INTO SomeTable (MyApplication) VALUES (@TestApplicationName)

// For parameters in the ScriptLog - e.g. you want to write the user that executed the script. 
            
migration.ScriptLogParameters
	.Add("user", DbType.String, (script, parameter) => "SuperUser");

migration.ScriptLogInsert = "INSERT INTO ScriptLog (ScriptName, ScriptDate, Status, DomainUser) VALUES 		   (@name,GETDATE(),'DONE',@user);"; // Note, that @name is built in - it's the migration name. 
            
migration.Synchronize(); // Fire up

```
