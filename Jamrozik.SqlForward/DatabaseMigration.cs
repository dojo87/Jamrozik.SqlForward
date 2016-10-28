using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Jamrozik.SqlForward
{
    /// <summary>
    /// The DatabaseMigration class executes SQL scripts in a certain location accordingly to their file name order one by one
    /// just like migrations would be executed on a CodeFirst solution. 
    /// </summary>
    /// <remarks>
    /// The usage of DatabaseMigration is as follows:
    /// <list type="bullet">
    /// <item>Specify SqlForward.DatabaseScripts App Setting as the folder where scripts will be held. You can use virtual ~ tilda in Web environments.</item>
    /// <item>Specify SqlForward.Initialization App Setting to the SQL file containing the initial database setup (e.g. Initialization.sql). </item>
    /// <item>Initialize the migrator with a factory function initializing a IDbConnection instance representing the database. </item>
    /// <item>Run the Synchronize method. Try-Catch if you want to react to migration errors. You can also control the process by adding an event handler to Changed event.</item>
    /// </list>
    /// </remarks>
    public class DatabaseMigration
    {
        /// <summary>
        /// Initializes a new instance of the DatabaseMigration class specifying a factory function for the database connection.
        /// </summary>
        /// <param name="connectionFactory">The connection factory is a function that returns a constructed object 
        /// of interface IDbConnection. It ensures, that the connection is used only by the migrator
        /// and no other function, because the connection returned by the connectionFactory is
        /// used and disposed after the migration process. The connection isn't reusable.</param>
        public DatabaseMigration(Func<IDbConnection> connectionFactory)
        {
            this.ConnectionFactory = connectionFactory;
            if (this.ConnectionFactory == null){
                throw new ArgumentNullException("No connection factory provided");
            }

            this.User = "none";
        }

        #region Properties
        
        /// <summary>
        /// Gets or privetly sets the ConnectionFactory creating the IDbConnection instance.
        /// </summary>
        protected Func<IDbConnection> ConnectionFactory
        {
            get;
            private set;
        }

        /// <summary>
        /// Holds the Connection created by the Connection factory.
        /// </summary>
        protected IDbConnection Connection
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the folder containing migration scripts.
        /// Default: SqlForward.DatabaseScripts from AppConfig AppSettings.
        /// </summary>
        public string DatabaseScripts
        {
            get;
            set;
        } = ConfigurationManager.AppSettings["SqlForward.DatabaseScripts"];

        /// <summary>
        /// Gets or sets the initialization script file name (is assumed to be in the DatabaseScripts folder).
        /// Default: SqlForward.Initialization from AppConfig AppSettings.
        /// </summary>
        public string InitializationScript
        {
            get;
            set;
        } = ConfigurationManager.AppSettings["SqlForward.Initialization"];
        
        /// <summary>
        /// Gets the current stage of the migration for information purposes.
        /// </summary>
        public DatabaseMigrationStage CurrentDatabaseMigrationStage { get; protected set; } = DatabaseMigrationStage.None;

        /// <summary>
        /// Gets or sets the user that can be setup for a migration, so that it will be logged in the ScriptLog migration table.
        /// </summary>
        public string User
        {
            get;set;
        }
        /// <summary>
        /// Specifies an event on any change in stage or progress in migrations.
        /// </summary>
        public event EventHandler<DatabaseMigrationEventArgs> Changed;
        

        #endregion Properties

        #region Public Methods

        /// <summary>
        /// Migrates the database through all pending migration
        /// </summary>
        public void Synchronize()
        {
            Synchronize(null);
        }

        /// <summary>
        /// Migrates the database through all pending migrations up to specificMigration.
        /// </summary>
        /// <param name="specificMigration"></param>
        public void Synchronize(string specificMigration)
        {
            Log(DatabaseMigrationStage.Started, "Started Migration at " + DateTime.Now + ". "
                + (specificMigration == null ? "" : " Migrating to " + specificMigration));

            using (this.Connection = this.ConnectionFactory())
            {
                if (this.Connection.State == ConnectionState.Closed)
                {
                    this.Connection.Open();
                }

                string scriptsFolder = GetScriptsFolder();
                InitializeScriptLogTable(scriptsFolder);

                IterateMigrations(specificMigration,
                    GetExecutedScripts(),
                    GetScriptsToExecute(scriptsFolder));

                Log(DatabaseMigrationStage.Finished, "Finished Migration");
            }
        }

        /// <summary>
        /// Gets the list of executed scripts/migrations from the ScriptLog table tracking the migrations done on a particular database.
        /// </summary>
        /// <returns>A list of filenames representing migrations already executed.</returns>
        public List<string> GetExecutedScripts()
        {
            IDbCommand executedScriptsCommand = this.Connection.CreateCommand();
            executedScriptsCommand.CommandText = @"SELECT [ScriptName]
              ,[ScriptDate]
              ,[Status]
              ,[DomainUser] FROM ScriptLog";

            List<string> executedScriptNames = new List<string>();
            IDataReader reader = executedScriptsCommand.ExecuteReader();
            while (reader.Read())
            {
                executedScriptNames.Add(reader.GetString(0));
            }
            reader.Close();
            return executedScriptNames;
        }

        #endregion Public Methods

        #region Private/Protected members

        /// <summary>
        /// Creates the table, which will hold information about scripts already executed/migrated.
        /// </summary>
        /// <param name="scriptsFolder">The path to the folder containing the scripts/migrations</param>
        private void InitializeScriptLogTable(string scriptsFolder)
        {
            string scriptLogCreation = Path.Combine(scriptsFolder, InitializationScript);
            this.CurrentDatabaseMigrationStage = DatabaseMigrationStage.Initializing;
            ExecuteScriptInTransaction(scriptLogCreation);
        }

        /// <summary>
        /// Helper method for logging progress.
        /// </summary>
        /// <param name="stage">The stage of the migration.</param>
        /// <param name="message">Message describing the progress.</param>
        /// <param name="migration">Currently run migration, if in stage Migrating.</param>
        /// <param name="ex">Exception, if available.</param>
        protected void Log(DatabaseMigrationStage stage, string message, string migration = null, Exception ex = null)
        {
            this.CurrentDatabaseMigrationStage = stage;
            Changed?.Invoke(this, new DatabaseMigrationEventArgs(this.CurrentDatabaseMigrationStage, migration, message, ex));
            System.Diagnostics.Trace.WriteLine($"Database Migration {DateTime.Now} - Stage [{this.CurrentDatabaseMigrationStage}] - Migration [{migration}] - {message}. {ex}");
        }

        /// <summary>
        /// Retrieves all the files with *.sql extension from the folder containing migrations.
        /// </summary>
        /// <param name="scriptsFolder">The path to the folder containing the scripts/migrations</param>
        /// <returns>A list of migrations (all, pending and executed alike)</returns>
        private static List<string> GetScriptsToExecute(string scriptsFolder)
        {
            return Directory.GetFiles(scriptsFolder, "*.sql").OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Iterates through the pending migrations and executes the scripts along with tracking the execution in the ScriptLog table.
        /// </summary>
        /// <param name="specificMigration">Specific migration, if the migration should go only to this stage.</param>
        /// <param name="executedScriptNames">List of scripts already saved as executed in ScriptLog.</param>
        /// <param name="files">List of files in the migration folder. </param>
        private void IterateMigrations(string specificMigration, List<string> executedScriptNames, List<string> files)
        {
            var pending = files.Count(f => !executedScriptNames.Contains(Path.GetFileName(f)));
            Log(DatabaseMigrationStage.CheckingPendingMigrations, $"There are {pending} pending migrations. {files.Count} all migrations, {executedScriptNames.Count} already executed migrations");

            this.CurrentDatabaseMigrationStage = DatabaseMigrationStage.Migrating;
            int scriptsExecutedCount = 0;
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                if (!executedScriptNames.Contains(fileName))
                {
                    Log(this.CurrentDatabaseMigrationStage, $"Starting {scriptsExecutedCount + 1}/{pending} migration {fileName}", fileName);

                    ExecuteScriptInTransaction(file);
                    RecordMigrationExecuted(fileName);
                    scriptsExecutedCount++;
                }

                if (specificMigration != null && fileName == specificMigration)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Inserts an entry to ScriptLog for the given fileName which is equivalent to the migration.
        /// </summary>
        /// <param name="fileName">The filename of the migration.</param>
        private void RecordMigrationExecuted(string fileName)
        {
            IDbCommand insertScriptLog = this.Connection.CreateCommand();
            insertScriptLog.CommandText = $"INSERT INTO ScriptLog (ScriptName, ScriptDate, Status, DomainUser) VALUES (@fileName,GETDATE(),'Done',@username); ";
            AddParameterToCommand(insertScriptLog, DbType.String, "fileName", fileName);
            AddParameterToCommand(insertScriptLog, DbType.String, "username", this.User);
            insertScriptLog.ExecuteNonQuery();
        }

        /// <summary>
        /// Helper method to add parameters to an IDbCommand.
        /// </summary>
        /// <param name="command">Command to add to.</param>
        /// <param name="type">Type of the parameter</param>
        /// <param name="name">Name of the parameter.</param>
        /// <param name="value">Value of the parameter.</param>
        /// <returns>The created parameter.</returns>
        private IDataParameter AddParameterToCommand(IDbCommand command, DbType type, string name, object value)
        {
            IDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value;
            command.Parameters.Add(parameter);
            return parameter;
        }

        /// <summary>
        /// Gets the path to the script folder. Evaluated from the DatabaseScripts property.
        /// </summary>
        /// <returns>The path to the script folder</returns>
        private string GetScriptsFolder()
        {
            string scriptsFolder = DatabaseScripts;
            if (scriptsFolder.StartsWith("~"))
            {
                scriptsFolder = System.Web.Hosting.HostingEnvironment.MapPath(scriptsFolder);
            }
            return scriptsFolder;
        }

        /// <summary>
        /// Executes the given script in a transaction.
        /// </summary>
        /// <param name="script"></param>
        private void ExecuteScriptInTransaction(string script)
        {
            using (IDbTransaction transaction = this.Connection.BeginTransaction())
            {
                string migrationName = Path.GetFileNameWithoutExtension(script);
                Stopwatch watch = new Stopwatch();
                try
                {
                    watch.Start();
                    string scriptCommand = File.ReadAllText(script);
                    IDbCommand initializationCommand = this.Connection.CreateCommand();
                    initializationCommand.CommandText = scriptCommand;
                    initializationCommand.Transaction = transaction;
                    initializationCommand.ExecuteNonQuery();
                    transaction.Commit();
                    Log(this.CurrentDatabaseMigrationStage, $"Executed migration {migrationName} in {watch.Elapsed}", migrationName);

                }
                catch (Exception ex)
                {
                    Log(this.CurrentDatabaseMigrationStage, $"Error on migration {migrationName} (time: {watch.Elapsed})", migrationName, ex);

                    transaction.Rollback();
                    throw;
                }
            }
        }

        #endregion Private/Protected members
    }
}
