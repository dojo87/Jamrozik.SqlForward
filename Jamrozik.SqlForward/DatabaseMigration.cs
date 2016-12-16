/*
 * The MIT License (MIT)
Copyright (c) 2016 Jamrozik.Net

Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
and associated documentation files (the "Software"), to deal in the Software without restriction, 
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial 
portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT 
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

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
        /// Specifies an event on any change in stage or progress in migrations.
        /// </summary>
        public event EventHandler<DatabaseMigrationEventArgs> Changed;

        /// <summary>
        /// ScriptParameter collection is for resolving at runtime any parameters that are suppose to be subsituted
        /// when executing a script. The delegate which is the factory defines the object return value for a given parameter and given
        /// script name. 
        /// </summary>
        /// <remarks>
        /// To pass parametric values to the scripts executed on the database, you can define them in the ScriptParameters:
        /// <code>
        ///   ((DatabaseMigration)migration).ScriptParameters
        ///     .Add("TestApplicationName", DbType.String, (script, parameter) => ConfigurationManager.AppSettings["TestApplicationName"]); 
        /// </code>
        /// This example shows how to bind a parameter in the migration script named @TestApplicationName to a value from the AppSettings. 
        /// </remarks>
        public virtual MigrationParameterColletion ScriptParameters { get; protected set; } = new MigrationParameterColletion();

        /// <summary>
        /// ScriptLogParameter collection is for resolving at runtime any parameters that are suppose to be subsituted
        /// when executing the script that writes to the migration log. The delegate which is the factory defines the object return value for a given parameter and given
        /// script name. 
        /// </summary>
        /// <remarks>
        /// To pass parametric values to the script writting into the script log, you can define them in the ScriptLogParameters:
        /// <code>
        ///   ((DatabaseMigration)migration).ScriptLogParameters
        ///     .Add("TestApplicationName", DbType.String, (script, parameter) => ConfigurationManager.AppSettings["TestApplicationName"]); 
        /// </code>
        /// This example shows how to bind a parameter in the INSERT script named @TestApplicationName to a value from the AppSettings. 
        /// </remarks>
        public virtual MigrationParameterColletion ScriptLogParameters { get; protected set; } = new MigrationParameterColletion();

        /// <summary>
        /// Gets or sets the Insert script used for adding information about migrations executed on the database.
        /// Please ensure that it is parametrized so that it will not be vulnerable for SQL injection. The parameter values
        /// are defined by factory methods in the ScriptLogParameters.
        /// </summary>
        public string ScriptLogInsert { get; set; } = "INSERT INTO ScriptLog(ScriptName, ScriptDate, Status, DomainUser) VALUES(@name, GETDATE(),NULL,NULL);";

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
            ExecuteScriptInTransaction(scriptLogCreation, ScriptLogParameters);
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

                    ExecuteScriptInTransaction(file, ScriptParameters);
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
            insertScriptLog.CommandText = this.ScriptLogInsert;
            AddParameterToCommand(insertScriptLog, DbType.String, "name", fileName);
            if (ScriptLogParameters.Count > 0)
            {
                foreach (var parameter in ScriptLogParameters)
                {
                    AddParameterToCommand(insertScriptLog, parameter.Value.DatabaseType , parameter.Value.Name, parameter.Value.Value(fileName));
                }
            }

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
        /// <param name="parameterCollection">The parameter collection to use.</param>
        protected virtual void ExecuteScriptInTransaction(string script, MigrationParameterColletion parameterCollection)
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

                    if (parameterCollection.Count > 0)
                    {
                        foreach (var parameter in parameterCollection)
                        {
                            AddParameterToCommand(initializationCommand, parameter.Value.DatabaseType, parameter.Value.Name, parameter.Value.Value(Path.GetFileName(script)));
                        }
                    }

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
