using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;

namespace JSONDB
{
    public class Query
    {
        private string QueryString { get; set; } = String.Empty;

        private Database DBConnection { get; set; }

        private string Table { get; set; }

        private bool QueryPrepared { get; set; }

        private bool QueryExecuted { get; set; }

        private JObject ParsedQuery { get; set; }

        public Query(Database database)
        {
            DBConnection = database;
        }

        public object Send(string query)
        {
            QueryString = query;

            try
            {
                ParsedQuery = QueryParser.Parse(QueryString);
            }
            catch (Exception e)
            {
                throw e;
            }

            QueryPrepared = false;
            QueryExecuted = false;

            return _execute();
        }

        private object _execute()
        {
            if (!QueryExecuted)
            {
                _setTable(ParsedQuery["table"].ToString());

                string table_path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt");

                if (!Util.Exists(table_path))
                {
                    throw new Exception("Query Error: Can't execute the query. The table \"" + Table + "\" doesn't exists in database \"" + DBConnection.GetDatabase() + "\" or file access denied.");
                }

                JObject json_array = Database.GetTableData(table_path);

                try
                {
                    QueryExecuted = true;
                    Benchmark.Mark("jsondb_(query)_start");
                    object res = null;
                    switch (ParsedQuery["action"].ToString())
                    {
                        case "select":
                            res = _select(json_array);
                            break;
                        case "insert":
                            res = _insert(json_array);
                            break;
                        case "update":
                            res = _update(json_array);
                            break;
                        case "truncate":
                            res = _truncate(json_array);
                            break;
                        case "count":
                            res = _count(json_array);
                            break;
                    }
                    Benchmark.Mark("jsondb_(query)_end");
                    return res;
                }
                catch (Exception e)
                {
                    QueryExecuted = false;
                    throw e;
                }
            }
            else
            {
                throw new Exception("Query Error: There is no query to execute, or the query is already executed.");
            }
        }

        private int _getLastValidRowID(JObject data, bool min)
        {
            int last = 0;
            var dataIterator = data.GetEnumerator();
            while (dataIterator.MoveNext())
            {
                var line = dataIterator.Current.Value;
                if (last == 0)
                {
                    last = (int)line["#rowid"];
                }
                else
                {
                    last = min ? Math.Min(last, (int)line["#rowid"]) : Math.Max(last, (int)line["#rowid"]);
                }
            }
            return last;
        }

        private void _setTable(string table)
        {
            if (!DBConnection.IsWorkingDatabase())
            {
                throw new Exception("Query Error: Can't use the table \"" + table + "\", there is no database selected.");
            }

            string path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), table + ".jdbt");
            if (!Util.Exists(path))
            {
                throw new Exception("Query Error: Can't use the table \"" + table + "\", the table doesn't exist in the database.");
            }

            Table = table;
        }

        private object _parseValue(object value, JObject properties)
        {
            if (value != null || (properties["not_null"] != null && bool.Parse(properties["not_null"].ToString()) == true))
            {
                if (properties["type"] != null)
                {
                    if (new Regex("link\\(.+\\)").IsMatch(properties["type"].ToString()))
                    {
                        string link = new Regex("link\\((.+)\\)").Replace(properties["type"].ToString(), "$1");
                        var link_info = link.Split('.');
                        var link_table_path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), link_info[0] + ".jdbt");
                        var link_table_data = Database.GetTableData(link_table_path);
                        value = _parseValue(value, (JObject)link_table_data["properties"][link_info[1]]);
                        var dataIterator = ((JObject)link_table_data["data"]).GetEnumerator();
                        while (dataIterator.MoveNext())
                        {
                            var data = (JObject)dataIterator.Current.Value;
                            if (data[link_info[1]] == value)
                            {
                                return dataIterator.Current.Key;
                            }
                        }
                    }
                }
                else
                {
                    switch (properties["type"].ToString())
                    {
                        case "int":
                        case "integer":
                        case "number":
                            int i;
                            if (int.TryParse(value.ToString(), out i))
                            {
                                value = i;
                            }
                            else
                            {
                                value = 0;
                            }
                            break;
                        case "decimal":
                        case "float":
                            float f;
                            if (float.TryParse(value.ToString(), out f))
                            {
                                value = f;
                            }
                            else
                            {
                                value = 0f;
                            }
                            break;
                        case "string":
                            value = value.ToString();
                            if (properties["max_length"] != null && value.ToString().Length > (int)properties["max_length"])
                            {
                                value = value.ToString().Substring(0, (int)properties["max_length"]);
                            }
                            break;
                        case "char":
                            value = value.ToString()[0];
                            break;
                        case "bool":
                        case "boolean":
                            bool b;
                            if (bool.TryParse(value.ToString(), out b))
                            {
                                value = b;
                            }
                            else
                            {
                                value = value.ToString() != String.Empty;
                            }
                            break;
                        case "array":
                            value = JObject.Parse(value.ToString());
                            break;
                        default:
                            throw new Exception("JSONDB Error: Trying to parse a value with an unsupported type \"" + properties["type"].ToString() + "\"");
                        }
                }
            }
            else if (properties["default"] != null)
            {
                value = _parseValue(properties["default"], properties);
            }
            return value;
        }

        private object _parseFunction(string func, string value)
        {
            switch (func)
            {
                case "sha1":
                    return Util.SHA1(value);
                case "md5":
                    return Util.MD5(value);
                case "lowercase":
                    return value.ToLower();
                case "uppercase":
                    return value.ToUpper();
                case "ucfirst":
                    string first = value[0].ToString().ToUpper();
                    return first + value.Substring(1).ToLower();
                case "strlen":
                    return value.Length;
                default:
                    throw new Exception("JSONDB Query Parse Error: Sorry but the function " + func + "() is not implemented in JQL.");

            }
        }

        private object _count(JObject json_array)
        {
            throw new NotImplementedException();
        }

        private object _truncate(JObject json_array)
        {
            throw new NotImplementedException();
        }

        private object _update(JObject json_array)
        {
            throw new NotImplementedException();
        }

        private object _insert(JObject json_array)
        {
            throw new NotImplementedException();
        }

        private object _select(JObject json_array)
        {
            throw new NotImplementedException();
        }
    }
}
