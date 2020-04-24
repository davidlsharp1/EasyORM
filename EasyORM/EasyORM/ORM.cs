using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EasyORM
{
    public static class ORM
    {
        public static string ConnectionString { get; set; }
        public static bool Error { get; set; }
        public static string ErrorMessage { get; set; }

        private static object GetPropertyValue(object obj, string propertyName)
        {
            return obj.GetType().GetProperties()
               .Single(pi => pi.Name == propertyName)
               .GetValue(obj, null);
        }


        public static class DataBase<T>
        {
            private static bool TableCheck(T currentObject)
            {
                var currentType = typeof(T);
                var allTablesDS = new DataSet();
                var tableLS = new List<string>();

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter())
                    {
                        string TableCheckSQL = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'";
                        adapter.SelectCommand = new SqlCommand(TableCheckSQL, db);
                        adapter.Fill(allTablesDS);
                    }
                }
                foreach (DataRow row in allTablesDS.Tables[0].Rows)
                {
                    tableLS.Add(row[2].ToString().ToUpper());
                }

                // making sure tablename is an existing table in database
                if (tableLS.Contains(currentType.Name.ToUpper()))
                {
                    return true;
                }
                else
                {
                    Error = true;
                    ErrorMessage = $"{currentType.Name.ToUpper()} does not exist in the database";
                    throw new Exception(ErrorMessage);
                }
            }
        }

        public static class Model
        {
            public static string GetModelFromTable(string tableName)
            {
                var allTablesDS = new DataSet();
                var tablesInfoDS = new DataSet();

                var tableLS = new List<string>();

                // first connect to db and get list of tables to ensure we can concatenate the SQL safely using 
                // the table name.  Apparently you can not parameterize the table name.
                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter())
                    {
                        string TableCheckSQL = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'";
                        adapter.SelectCommand = new SqlCommand(TableCheckSQL, db);
                        adapter.Fill(allTablesDS);
                    }
                }
                foreach (DataRow row in allTablesDS.Tables[0].Rows)
                {
                    tableLS.Add(row[2].ToString().ToUpper());
                }

                // making sure tablename is an existing table in database
                if (!tableLS.Contains(tableName.ToUpper()))
                {
                    Error = true;
                    ErrorMessage = $"{tableName.ToUpper()} does not exist in the database";
                    throw new Exception(ErrorMessage);
                }

                // now query to get data from the table

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();

                    using (SqlDataAdapter adapter = new SqlDataAdapter())
                    {
                        string TableFieldSQL = $"select * from information_schema.columns where table_name = '{tableName}'";
                        adapter.SelectCommand = new SqlCommand(TableFieldSQL, db);
                        adapter.Fill(tablesInfoDS);
                    }
                }

                var modelLS = new List<string>();

                foreach (DataRow row in tablesInfoDS.Tables[0].Rows)
                {
                    modelLS.Add($"public {PropertyTypeForModel(row[7].ToString())} {row[3]} {{ get; set; }}");
                }

                return String.Join(Environment.NewLine, modelLS);
            }

            public static string PropertyTypeForModel(string dataType)
            {
                switch (dataType)
                {
                    case "bit":
                        return "bool";

                    case "decimal":
                        return "decimal";

                    case "int":
                        return "int";

                    case "date":
                        return "datetime";

                    case "float":
                        return "double";

                    case "nvarchar":
                    case "nvarchar2":
                        return "string";

                    default:
                        return "not found";
                }
            }

        }


        public static class Actions<T>
        {

            public static async Task InsertAsync(T currentObject, CancellationToken cancellationToken = default(CancellationToken))
            {
                await Task.Run(() => Insert(currentObject, cancellationToken));
            }

            public static async Task Insert(T currentObject, CancellationToken cancellationToken = default(CancellationToken))
            {
                var currentType = typeof(T);
                var ob = Convert.ChangeType(currentObject, currentType);
                var props = currentObject.GetType().GetProperties();


                var fieldLS = new List<string>();
                var parameterLS = new List<string>();
                var valueLS = new List<string>();

                foreach (var prop in props)
                {
                    if (prop.Name.ToUpper() != "ID")
                    {
                        if (GetPropertyValue(ob, prop.Name) != null)
                        {
                            fieldLS.Add(prop.Name);
                            parameterLS.Add($"@{prop.Name}");
                            valueLS.Add(GetPropertyValue(ob, prop.Name).ToString());
                        }
                    }
                }

                string insertSQL = $"INSERT INTO {currentType.Name.ToUpper()}({String.Join(",", fieldLS)}) VALUES({ String.Join(",", parameterLS)})";

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    using (SqlCommand cmd = new SqlCommand(insertSQL, db))
                    {
                        foreach (var item in parameterLS)
                        {
                            var p = new SqlParameter();
                            p.ParameterName = item;
                            var i = parameterLS.FindIndex(a => a == item);
                            p.Value = valueLS[i];
                            cmd.Parameters.Add(p);
                        }
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

            public static async Task UpdateAsync(T currentObject, CancellationToken cancellationToken = default(CancellationToken))
            {
                await Task.Run(() => Update(currentObject, cancellationToken));
            }

            public static async Task Update(T currentObject, CancellationToken cancellationToken = default(CancellationToken))
            {
                var currentType = typeof(T);
                var ob = Convert.ChangeType(currentObject, currentType);
                var ID = "";

                var props = currentObject.GetType().GetProperties();

                var fieldLS = new List<string>();
                var parameterLS = new List<string>();
                var valueLS = new List<string>();

                foreach (var prop in props)
                {
                    if (prop.Name.ToUpper() == "ID")
                    {
                        ID = GetPropertyValue(ob, prop.Name).ToString();
                    }

                    if (prop.Name.ToUpper() != "ID")
                    {
                        fieldLS.Add($"{prop.Name}=@{prop.Name}");
                        parameterLS.Add($"@{prop.Name}");
                        valueLS.Add(GetPropertyValue(ob, prop.Name).ToString());
                    }
                }

                string updateSQL = $"UPDATE {currentType.Name.ToUpper()} SET {String.Join(",", fieldLS)} WHERE ID={ID}";

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    using (SqlCommand cmd = new SqlCommand(updateSQL, db))
                    {
                        foreach (var item in parameterLS)
                        {
                            var p = new SqlParameter();
                            p.ParameterName = item;
                            var i = parameterLS.FindIndex(a => a == item);
                            if (valueLS[i] is null)
                            {
                                p.Value = DBNull.Value;
                            }
                            else
                            {
                                p.Value = valueLS[i];
                            }
                            cmd.Parameters.Add(p);
                        }
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }

            public static async Task DeleteAsync(T currentObject, CancellationToken cancellationToken = default(CancellationToken))
            {
                await Task.Run(() => Delete(currentObject, cancellationToken));
            }

            public static async Task Delete(T currentObject, CancellationToken cancellationToken = default(CancellationToken))
            {
                var currentType = currentObject.GetType();
                var props = currentObject.GetType().GetProperties();

                var ID = "";

                var fieldLS = new List<string>();
                var parameterLS = new List<string>();
                var valueLS = new List<string>();

                foreach (var prop in props)
                {
                    if (prop.Name.ToUpper() == "ID")
                    {
                        ID = GetPropertyValue(currentObject, prop.Name).ToString();
                    }
                }

                string updateSQL = $"DELETE FROM {currentType.Name.ToUpper()} WHERE ID = @ID";

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    using (SqlCommand cmd = new SqlCommand(updateSQL, db))
                    {
                        var p = new SqlParameter();
                        p.ParameterName = "ID";
                        p.Value = ID;
                        cmd.Parameters.Add(p);
                        await cmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }
        }


        public static class Query<T>
        {

            public static async Task<List<T>> RunQuerySetAsync(QuerySet qs, CancellationToken cancellationToken = default(CancellationToken))
            {
                return await Task.Run(() => RunQuerySet(qs, cancellationToken));
            }

            public static async Task<List<T>> RunQuerySet(QuerySet qs, CancellationToken cancellationToken = default(CancellationToken))
            {
                DataTable dataTable = new DataTable();
                var whereLS = new List<string>();

                var SQL = $"SELECT * FROM {typeof(T).Name} WHERE";

                whereLS.Add(SQL);

                for (int i = 0; i < qs.ParamLS.Count; i++)
                {
                    if (i == 0)
                    {
                        whereLS.Add($"{qs.ParamLS[i].FieldName} {qs.ParamLS[i].Operator} @{qs.ParamLS[i].FieldName}");
                    }
                    else
                    {
                        whereLS.Add($"AND {qs.ParamLS[i].FieldName} {qs.ParamLS[i].Operator} @{qs.ParamLS[i].FieldName}");
                    }
                }

                var newSQL = string.Join(" ", whereLS);

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    using (SqlCommand command = new SqlCommand(newSQL, db))
                    {
                        foreach (var item in qs.ParamLS)
                        {
                            var param = new SqlParameter();
                            param.ParameterName = item.FieldName;
                            param.Value = item.Value;
                            command.Parameters.Add(param);
                        }

                        SqlDataReader reader;
                        reader = await command.ExecuteReaderAsync(cancellationToken);
                        if (reader.HasRows)
                        {
                            dataTable.Load(reader);
                        }
                    }
                }

                var props = typeof(T).GetProperties();
                var returnLS = new List<T>();

                foreach (DataRow row in dataTable.Rows)
                {
                    var tempObject = Activator.CreateInstance<T>();

                    foreach (DataColumn col in dataTable.Columns)
                    {
                        PropertyInfo prop = typeof(T).GetProperty(col.ColumnName);
                        if (row[col] != System.DBNull.Value)
                        {
                            prop.SetValue(tempObject, row[col]);
                        }
                    }
                    returnLS.Add(tempObject);
                }
                return returnLS;
            }

            public static async Task<List<T>> RunParamSQlAsync(string sql, CancellationToken cancellationToken = default(CancellationToken))
            {
                return await Task.Run(() => RunParamSQL(sql, cancellationToken));
            }


            public static async Task<List<T>> RunParamSQL(string sql, CancellationToken cancellationToken = default(CancellationToken))
            {
                DataTable dataTable = new DataTable();

                using (var db = new SqlConnection(ConnectionString))
                {

                    bool isParam = false;
                    // split up sql and get params from it
                    var paramLS = new List<string>();
                    var removeLS = new List<string>();
                    string param = string.Empty;

                    foreach (var letter in sql)
                    {
                        if (letter == 91) // [
                        {
                            isParam = true;
                            param = string.Empty;
                        }

                        if (isParam && letter == 93) // ]
                        {
                            isParam = false;
                            paramLS.Add(param);
                        }

                        if (isParam)
                        {
                            if (letter != 91)
                            {
                                param = param + letter.ToString();
                            }
                        }
                    }

                    foreach (var item in paramLS)
                    {
                        var itemLS = item.Split('|');
                        sql = sql.Replace($"[{item}]", $"@{itemLS[0]}");
                    }

                    db.Open();
                    using (SqlCommand command = new SqlCommand(sql, db))
                    {


                        foreach (var item in paramLS)
                        {
                            var pList = item.Split('|');
                            var sqlParam = new SqlParameter();
                            sqlParam.ParameterName = pList[0];
                            sqlParam.Value = pList[1];
                            command.Parameters.Add(sqlParam);
                        }

                        SqlDataReader reader;
                        reader = await command.ExecuteReaderAsync(cancellationToken);
                        if (reader.HasRows)
                        {
                            dataTable.Load(reader);
                        }
                    }
                }

                var props = typeof(T).GetProperties();
                var returnLS = new List<T>();

                foreach (DataRow row in dataTable.Rows)
                {
                    var tempObject = Activator.CreateInstance<T>();

                    foreach (DataColumn col in dataTable.Columns)
                    {
                        PropertyInfo prop = typeof(T).GetProperty(col.ColumnName);
                        if (row[col] != System.DBNull.Value)
                        {
                            prop.SetValue(tempObject, row[col]);
                        }
                    }
                    returnLS.Add(tempObject);
                }
                return returnLS;
            }



            public static async Task<List<T>> RunSQLAsync(string sql, CancellationToken cancellationToken = default(CancellationToken))
            {
                return await Task.Run(() => RunSQL(sql, cancellationToken));
            }

            public static async Task<List<T>> RunSQL(string sql, CancellationToken cancellationToken = default(CancellationToken))
            {
                DataTable dataTable = new DataTable();

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    using (SqlCommand command = new SqlCommand(sql, db))
                    {
                        SqlDataReader reader;
                        reader = await command.ExecuteReaderAsync(cancellationToken);
                        if (reader.HasRows)
                        {
                            dataTable.Load(reader);
                        }
                    }
                }

                var props = typeof(T).GetProperties();
                var returnLS = new List<T>();

                foreach (DataRow row in dataTable.Rows)
                {
                    var tempObject = Activator.CreateInstance<T>();

                    foreach (DataColumn col in dataTable.Columns)
                    {
                        PropertyInfo prop = typeof(T).GetProperty(col.ColumnName);
                        if (row[col] != System.DBNull.Value)
                        {
                            prop.SetValue(tempObject, row[col]);
                        }
                    }
                    returnLS.Add(tempObject);
                }
                return returnLS;
            }

            public static async Task<T> GetObjectByIDAsync(int ID)
            {
                return await Task.Run(() => GetObjectByID(ID));
            }

            public static async Task<T> GetObjectByID(int ID)
            {
                var props = typeof(T).GetProperties();
                var tempObject = Activator.CreateInstance<T>();

                foreach (var p in props)
                {
                    if (p.CustomAttributes.Count() > 0)
                    {
                        if (p.CustomAttributes.ToList()[0].AttributeType.ToString().Contains("ForeignKeyAttribute"))
                        {
                            if (p.PropertyType.Namespace == "System.Collections.Generic")
                            {
                                string FK = "";
                                string fkClass = "";
                                string fkField = "";

                                if (p.CustomAttributes.Count() > 0)
                                {
                                    FK = p.CustomAttributes.ToList()[0].ConstructorArguments.ToList()[0].Value.ToString();
                                    var FKLS = FK.Split('.');
                                    fkClass = FKLS[0];
                                    fkField = FKLS[1];
                                }

                                var start = p.ToString().IndexOf("[");
                                var end = p.ToString().IndexOf("]");
                                var type = p.ToString().Substring(start + 1, (end - start) - 1);
                                var typeLS = type.Split('.');
                                var collectionType = typeLS[1];

                                if (fkField.Length > 0)
                                {
                                    var qs = new QuerySet();
                                    var p1 = new QueryParams();
                                    p1.FieldName = fkField;
                                    p1.Operator = "=";
                                    p1.Value = ID.ToString();
                                    qs.AddParameter(fkField, "=", ID.ToString());

                                    var callingAssembly = Assembly.GetCallingAssembly();
                                    var callingProject = callingAssembly.FullName.Split(',')[0];

                                    Type objectType = (from asm in AppDomain.CurrentDomain.GetAssemblies()
                                                       from t in asm.GetTypes()
                                                       where t.IsClass && t.Name == fkClass
                                                       select t).Single();
                                    var tempObject2 = Activator.CreateInstance(objectType);

                                    var resultsLS = Query<T>.RunSQL($"Select * from {typeof(T).Name}");

                                    //p.SetValue(tempObject, resultsLS);
                                }
                            }
                            else if (p.PropertyType.Assembly.DefinedTypes.ToList().Where(x => x.Name == p.PropertyType.Name).Count() > 0)  // linked to other objects
                            {
                                //var field = p.CustomAttributes.ToList()[0].ConstructorArguments[0].Value.ToString();
                                //var callingAssembly = Assembly.GetCallingAssembly();
                                //var callingProject = callingAssembly.FullName.Split(',')[0];
                                //var tempObject2 = GetInstance($"{callingProject}.{p.Name}");
                                //var SQL = $"SELECT * FROM {tempObject2.GetType().Name} WHERE ID = {field}";

                                //var data = ORM2.GetObject<T>.ByID(1);
                                //p.SetValue(tempObject, data);
                            }
                        }
                    }
                }

                var queryset = new QuerySet();
                queryset.AddParameter("ID", "=", ID.ToString());
                var dataLS = await Query<T>.RunSQLAsync($"SELECT * FROM {typeof(T).Name} WHERE ID = {ID}");
                return dataLS.FirstOrDefault();
            }

            private static object GetInstance(string FullyQualifiedNameOfClass)
            {
                Type type = Type.GetType(FullyQualifiedNameOfClass);
                if (type != null)
                {
                    return Activator.CreateInstance(type);
                }
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(FullyQualifiedNameOfClass);
                    if (type != null)
                        return Activator.CreateInstance(type);
                }
                return null;
            }
        }

    }
}
