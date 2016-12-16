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
using System.Data;
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
