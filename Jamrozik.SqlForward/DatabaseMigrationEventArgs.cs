using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jamrozik.SqlForward
{
    public enum DatabaseMigrationStage
    {
        None,
        Started,
        Initializing,
        CheckingPendingMigrations,
        Migrating,
        Finished
    }
    public class DatabaseMigrationEventArgs: EventArgs
    {
        internal DatabaseMigrationEventArgs(DatabaseMigrationStage currentStage, string currentMigration, string message, Exception exception)
        {
            Message = message;
            CurrentMigration = currentMigration;
            Exception = exception;
            CurrentStage = currentStage;
        }

        public string Message
        {
            get;
            private set;
        }

        public DatabaseMigrationStage CurrentStage
        {
            get;
            private set;
        }

        public string CurrentMigration
        {
            get;
            private set;
        }

        public Exception Exception
        {
            get;
            private set;
        }

    }
}
