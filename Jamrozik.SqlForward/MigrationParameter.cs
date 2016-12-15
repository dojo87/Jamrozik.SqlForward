using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Jamrozik.SqlForward
{
    public delegate object ResolveMigrationParameter(string scriptName, string parameterName);

    public class MigrationParameterColletion: Dictionary<string,MigrationParameter>
    {
        public MigrationParameterColletion Add(MigrationParameter parameter)
        {
            this.Add(parameter.Name, parameter);
            return this;
        }

        public MigrationParameterColletion Add(string name, DbType dbType, ResolveMigrationParameter valueFactory)
        {
            return this.Add(new MigrationParameter(name, dbType, valueFactory));
        }
    }

    public class MigrationParameter
    {
        public MigrationParameter(string name, DbType dbType, ResolveMigrationParameter valueFactory)
        {
            Name = name;
            DatabaseType = dbType;
            ValueFactory = valueFactory;
        }

        public string Name { get; set; }
        public DbType DatabaseType { get; set; }
        public ResolveMigrationParameter ValueFactory { get; set; }

        public object Value(string migrationName)
        {
            return ValueFactory(migrationName, this.Name);
        }
    }
}
