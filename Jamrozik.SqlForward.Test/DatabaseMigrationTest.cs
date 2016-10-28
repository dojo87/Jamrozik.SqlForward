using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Configuration;
using System.Data;
using System.IO;
using System.Reflection;

namespace Jamrozik.SqlForward.Test
{
    [TestFixture]
    public class DatabaseMigrationTest
    {
        #region Setups

        [OneTimeSetUp]
        public void Setup()
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        protected DatabaseMigration Initialize(bool cleanup = true)
        {
            if (cleanup && ConfigurationManager.AppSettings["SqlForward.Test.Cleanup"] != null)
            {
                using (var connection = InitializeConnectionForTests())
                {
                    connection.ChangeDatabase("master");
                    var cleanCommand = connection.CreateCommand();
                    cleanCommand.CommandText = ConfigurationManager.AppSettings["SqlForward.Test.Cleanup"];
                    cleanCommand.ExecuteNonQuery();
                }
            }

            DatabaseMigration migration = new DatabaseMigration(InitializeConnectionForTests);
            migration.Changed += (o, e) => System.Console.WriteLine(e.Message);
            return migration;
        }

        protected IDbConnection InitializeConnectionForTests()
        {
            var connection = new System.Data.SqlClient.SqlConnection(ConfigurationManager.ConnectionStrings["DefaultTestConnection"].ConnectionString);
            connection.Open();
            return connection;
        }

        #endregion

        [Test]
        public void TestImplementationExists()
        {
            Assert.Throws(typeof(ArgumentNullException), () =>
            {
                DatabaseMigration migration = new DatabaseMigration(null);
            });
        }

        [Test]
        public void TestConfiguration()
        {
            DatabaseMigration migration = Initialize();
            Assert.AreEqual(ConfigurationManager.AppSettings["SqlForward.DatabaseScripts"],
                migration.DatabaseScripts,
                "Expected that the DatabaseScripts property will be equal to the Application Config SqlForward.DatabaseScripts");

            migration.DatabaseScripts = "DatabaseMigrations/TestInitialization";
            Assert.AreEqual("DatabaseMigrations/TestInitialization",
                migration.DatabaseScripts,
                "Expected that the DatabaseScripts property will be equal to what it is explicitly set to it.");

        }

        [Test]
        public void TestInitialization()
        {
            DatabaseMigration migration = Initialize();
            migration.DatabaseScripts = "./DatabaseMigrations/TestInitialization/";
            migration.Synchronize();

            using (IDbConnection connection = InitializeConnectionForTests())
            {
                IDbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM ScriptLog";
                var count = (int)command.ExecuteScalar();
                Assert.Greater(count, 0, "There is no ScriptLog entries, but it should have at least the initialization script");
            }
        }

        [Test]
        public void TestSuccessStory()
        {
            // First Migrations
            int events = 0;
            DatabaseMigration migration = Initialize();
            migration.DatabaseScripts = "./DatabaseMigrations/TestSuccessStory/";
            migration.Changed += (o, e) => events++;
            migration.Synchronize();

            using (IDbConnection connection = InitializeConnectionForTests())
            {
                //Assert.AreEqual(5, events, "Expected events: Started, Initialization, Migration x 2, Finished")

                IDbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM ScriptLog";
                var count = (int)command.ExecuteScalar();
                Assert.AreEqual(3, count, "Rev001, Rev002 seem to be not executed.");
                
                command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Mytable";
                count = (int)command.ExecuteScalar();
                Assert.AreEqual(4, count, "Mytable doesn't have the correct number of entries. ");
            }
            
            //Second Migrations
            migration = Initialize(false);
            migration.DatabaseScripts = "./DatabaseMigrations/TestSuccessStorySecondGo/";
            migration.Synchronize();

            using (IDbConnection connection = InitializeConnectionForTests())
            {
                IDbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM ScriptLog";
                var count = (int)command.ExecuteScalar();
                Assert.AreEqual(5, count, "Rev003, Rev004 seem to be not executed.");

                command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM MySecondTable";
                count = (int)command.ExecuteScalar();
                Assert.AreEqual(5, count, "MySecondTable doesn't have the correct number of entries. ");
            }
        }

        [Test]
        public void TestFailureStory()
        {
            // First Migrations
            DatabaseMigration migration = Initialize();
            migration.DatabaseScripts = "./DatabaseMigrations/TestFailureStory/";
            bool hadErrorOnRev2 = false;
            migration.Changed += (o, e) =>
            {
                if (e.CurrentStage == DatabaseMigrationStage.Migrating 
                    && e.CurrentMigration != null && e.CurrentMigration.StartsWith("Rev002"))
                {
                    if (e.Exception != null)
                    {
                        hadErrorOnRev2 = true;
                    }
                }
            };
            Assert.Throws<System.Data.SqlClient.SqlException>(() => migration.Synchronize());

            Assert.IsTrue(hadErrorOnRev2, "There should be at least one error shown on this migration (Rev002)");


            using (IDbConnection connection = InitializeConnectionForTests())
            {
                IDbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM ScriptLog";
                var count = (int)command.ExecuteScalar();
                Assert.AreEqual(2, count, "There should be the initialization script, Rev001 only!");

                command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Mytable";
                count = (int)command.ExecuteScalar();
                Assert.AreEqual(1, count, "Mytable should have only one entry, because Rev003 shouldn't be executed if Rev002 failed..");
            }

        }

    }
}
