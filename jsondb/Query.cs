using Newtonsoft.Json.Linq;
using System;
using System.Linq;
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

        public JObject Send(string query)
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

        private JObject _execute()
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

                    JObject QueryResult = new JObject();
                    QueryResult["result"] = (JToken)res;
                    QueryResult["elapsed_time"] = Benchmark.ElapsedTime("jsondb_(query)_start", "jsondb_(query)_end");
                    QueryResult["memory_usage"] = Benchmark.MemoryUsage("jsondb_(query)_start", "jsondb_(query)_end");

                    return QueryResult;
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

        private JToken _parseValue(JToken value, JObject properties)
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

        private JToken _parseFunction(string func, string value)
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

        private JArray _select(JObject data)
        {
            JArray result = Util.Values((JObject)data["data"]);

            if (ParsedQuery["extensions"]["where"] != null)
            {
                if (((JArray)ParsedQuery["extensions"]["where"]).Count > 0)
                {
                    JArray res = new JArray();
                    for (int i = 0, l = ((JArray)ParsedQuery["extensions"]["where"]).Count; i < l; i++)
                    {
                        res = Util.Concat(res, _filter((JObject)data["data"], (JArray)ParsedQuery["extensions"]["where"][i]));
                    }
                    result = res;
                }
            }

            if (ParsedQuery["extensions"]["order"] != null)
            {
                string order_by = ParsedQuery["extensions"]["order"][0].ToString();
                string order_method = ParsedQuery["extensions"]["order"][1].ToString();

                result = Util.Sort(result, (after, now) =>
                {
                    if (order_method == "desc")
                    {
                        return String.Compare(now[order_by].ToString(), after[order_by].ToString()) == -1;
                    }
                    else
                    {
                        return String.Compare(now[order_by].ToString(), after[order_by].ToString()) == 1;
                    }
                });
            }

            if (ParsedQuery["extensions"]["limit"] != null)
            {
                result = Util.Slice(result, int.Parse(ParsedQuery["extensions"]["limit"][0].ToString()), int.Parse(ParsedQuery["extensions"]["limit"][1].ToString()));
            }

            if (ParsedQuery["extensions"]["on"] != null)
            {
                for (int i = 0, l = ((JArray)ParsedQuery["extensions"]["on"]).Count; i < l; i++)
                {
                    var on = ParsedQuery["extensions"]["on"][i];
                    switch (on["action"]["name"].ToString())
                    {
                        case "link":
                            JObject links = new JObject();
                            string key = on["column"].ToString();
                            JArray columns = (JArray)on["action"]["parameters"];

                            for (int j = 0, m = result.Count; j < m; j++)
                            {
                                var result_p = result[j];
                                if (new Regex("link\\((.+)\\)").IsMatch(data["properties"][key]["type"].ToString()))
                                {
                                    string link = new Regex("link\\((.+)\\)").Replace(data["properties"][key]["type"].ToString(), "$1");
                                    var link_info = link.Split('.');
                                    var link_table_path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), link_info[0] + ".jdbt");
                                    var link_table_data = Database.GetTableData(link_table_path);

                                    foreach (var linkID in (JObject)link_table_data["data"])
                                    {
                                        if (linkID.Key == result_p[key].ToString())
                                        {
                                            if (Array.IndexOf(columns.ToArray(), "*") != -1)
                                            {
                                                columns = (JArray)link_table_data["prototype"];
                                                columns.RemoveAt(Array.IndexOf(columns.ToArray(), "#rowid"));
                                            }
                                            result[j][key] = Util.IntersectKey((JObject)linkID.Value, Util.Flip(columns));
                                        }
                                    }
                                }
                                else
                                {
                                    throw new Exception("JSONDB Error: Can't link tables with the column \"" + key + "\". The column is not of type link.");
                                }
                            }
                            break;
                    }
                }
            }

            JArray temp = new JArray();
            if (Array.IndexOf(((JArray)ParsedQuery["parameters"]).ToArray(), "last_insert_id") != -1)
            {
                JObject res = new JObject();
                res["last_insert_id"] = data["properties"]["last_insert_id"];
                temp.Add(res);
            }
            else if (Array.IndexOf(((JArray)ParsedQuery["parameters"]).ToArray(), "*") != -1)
            {
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    JObject line = (JObject)result[i];
                    line.Remove("#rowid");
                    temp.Add(line);
                }
            }
            else
            {
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    JObject line = (JObject)result[i];
                    JObject res = new JObject();

                    for (int j = 0, m = ((JArray)ParsedQuery["parameters"]).Count; j < m; j++)
                    {
                        string field = ParsedQuery["parameters"][j].ToString();
                        if (new Regex("\\w+\\(.*\\)").IsMatch(field))
                        {
                            var parts = new Regex("(\\w+)\\((.*)\\)").Replace(field.ToString(), "$1.$2").Split('.');
                            string name = parts[0].ToString().ToLower();
                            string param = parts[1].ToString();

                            res[field] = _parseFunction(name, line[param].ToString());
                        }
                        else if (Array.IndexOf(((JArray)data["prototype"]).ToArray(), field) != -1)
                        {
                            res[field] = line[field];
                        }
                        else
                        {
                            throw new Exception("JSONDB Error: The column " + field + " doesn't exists in the table.");
                        }
                    }
                    temp.Add(res);
                }
                if (ParsedQuery["extensions"]["as"] != null)
                {
                    for (int i = 0, l = ((JArray)ParsedQuery["parameters"]).Count - ((JArray)ParsedQuery["extensions"]["as"]).Count; i < l; i++)
                    {
                        ((JArray)ParsedQuery["extensions"]["as"]).Add("null");
                    }
                    JObject replace = Util.Combine((JArray)ParsedQuery["parameters"], (JArray)ParsedQuery["extensions"]["as"]);
                    for (int i = 0, l = temp.Count; i < l; i++)
                    {
                        foreach (var item in replace)
                        {
                            string n = item.Value.ToString();
                            if (n.ToLower() == "null")
                            {
                                continue;
                            }
                            temp[i][n] = temp[i][item.Key];
                            ((JObject)temp[i]).Remove(item.Key);
                        }
                    }
                }
            }

            result = temp;

            return result;
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

        private bool _insert(JObject data)
        {
            throw new NotImplementedException();
        }

        private JArray _filter(JObject data, JArray filters)
        {
            JObject result = data;
            JObject temp = new JObject();

            for (int i = 0, l = filters.Count; i < l; i++)
            {
                JObject filter = (JObject)filters[i];
                if (filter["value"].ToString().ToLower() == "last_insert_id")
                {
                    filter["value"] = data["properties"]["last_insert_id"];
                }

                foreach (var lid in result)
                {
                    var line = (JObject)lid.Value;
                    var value = line[filter["field"].ToString()];
                    if (new Regex("\\w+\\(.*\\)").IsMatch(filter["field"].ToString()))
                    {
                        var parts = new Regex("(\\w+)\\((.*)\\)").Replace(filter["field"].ToString(), "$1.$2").Split('.');
                        string name = parts[0].ToString().ToLower();
                        string param = parts[1].ToString();
                        value = _parseFunction(name, line[param].ToString());
                        filter["field"] = param;
                    }
                    if (line[filter["field"].ToString()] == null)
                    {
                        throw new Exception("JSONDB Error: The field \"" + filter["field"] + "\" doesn't exists in the table \"" + Table + "\".");
                    }
                    switch (filter["operator"].ToString())
                    {
                        case "<":
                            if (String.Compare(value.ToString(), filter["value"].ToString()) == -1)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "<=":
                            if (String.Compare(value.ToString(), filter["value"].ToString()) >= 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "=":
                            if (String.Compare(value.ToString(), filter["value"].ToString()) == 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case ">=":
                            if (String.Compare(value.ToString(), filter["value"].ToString()) <= 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case ">":
                            if (String.Compare(value.ToString(), filter["value"].ToString()) == 1)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "!=":
                        case "<>":
                            if (String.Compare(value.ToString(), filter["value"].ToString()) != 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "%=":
                            if (int.Parse(value.ToString()) % int.Parse(filter["value"].ToString()) == 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "%!":
                            if (int.Parse(value.ToString()) % int.Parse(filter["value"].ToString()) != 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        default:
                            throw new Exception("JSONDB Error: The operator \"" + filter["operator"] +"\" is not supported. Try to use one of these operators: \"<\", \"<=\", \"=\", \">=\", \">\", \"<>\", \"!=\", \"%=\" or \"%!\".");
                    }
                }
                result = temp;
                temp = new JObject();
            }

            return Util.Values(result);
        }
    }
}
