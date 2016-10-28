# Jamrozik.SqlForward

The DatabaseMigration class executes SQL scripts in a certain location accordingly to their file name order one by one just like migrations would be executed on a CodeFirst solution.

The usage of DatabaseMigration is as follows:
- Specify SqlForward.DatabaseScripts App Setting as the folder where scripts will be held. You can use virtual ~ tilda in Web environments.
- Specify SqlForward.Initialization App Setting to the SQL file containing the initial database setup (e.g. Initialization.sql).
- Initialize the migrator with a factory function initializing a IDbConnection instance representing the database. 
- Run the Synchronize method. Try-Catch if you want to react to migration errors. You can also control the process by adding an event handler to Changed event.
