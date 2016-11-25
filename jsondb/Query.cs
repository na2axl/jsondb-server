using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace JSONDB.Library
{
    public class Query
    {
        private Database DBConnection { get; set; }

        private string Table { get; set; }

        private bool QueryExecuted { get; set; }

        private JObject ParsedQuery { get; set; }

        public Query(Database database)
        {
            DBConnection = database;
        }

        public JObject Send(string query)
        {
            ParsedQuery = QueryParser.Parse(query);

            QueryExecuted = false;

            return _execute();
        }

        public JObject[] MultiSend(string queries)
        {
            JObject[] parsedQueries = QueryParser.MultilineParse(queries);
            JObject[] Results = new JObject[parsedQueries.Length];

            for (int i = 0, l = parsedQueries.Length; i < l; i++)
            {
                ParsedQuery = parsedQueries[i];

                QueryExecuted = false;

                Results[i] = _execute();
            }

            return Results;
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

                // Wait until the file is unlocked and check the state each 100ms
                while (Util.FileIsLocked(Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt")))
                {
                    long end = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds + 100;
                    while (true)
                    {
                        long now = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalMilliseconds;
                        if (now >= end) break;
                    }
                }

                Benchmark.Mark("jsondb_(query)_start");
                Util.LockFile(Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt"));
                Cache.Reset();

                JObject json_array = JObject.Parse(Cache.Get(table_path));
                JObject QueryResult = new JObject();

                try
                {
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
                        case "delete":
                            res = _delete(json_array);
                            break;
                    }

                    Util.UnlockFile(Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt"));
                    Benchmark.Mark("jsondb_(query)_end");

                    QueryExecuted = true;
                    QueryResult["error"] = false;
                    QueryResult["result"] = (JToken)res;
                    QueryResult["elapsed_time"] = Benchmark.ElapsedTime("jsondb_(query)_start", "jsondb_(query)_end");
                    QueryResult["memory_usage"] = Benchmark.MemoryUsage("jsondb_(query)_start", "jsondb_(query)_end");
                }
                catch (Exception e)
                {
                    Util.UnlockFile(Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt"));
                    Benchmark.Mark("jsondb_(query)_end");

                    QueryExecuted = false;
                    QueryResult["error"] = true;
                    QueryResult["result"] = e.Message;
                    QueryResult["elapsed_time"] = Benchmark.ElapsedTime("jsondb_(query)_start", "jsondb_(query)_end");
                    QueryResult["memory_usage"] = Benchmark.MemoryUsage("jsondb_(query)_start", "jsondb_(query)_end");
                }

                return QueryResult;
            }
            else
            {
                throw new Exception("Query Error: There is no query to execute, or the query is already executed.");
            }
        }

        private int _getLastValidRowID(JObject data, bool min)
        {
            int last = 0;
            foreach (var item in data)
            {
                var line = item.Value;
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

        private int _getLastValidRowID(JArray data, bool min)
        {
            int last = 0;
            for (int i = 0, l = data.Count; i < l; i++)
            {
                var line = data[i];
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
                            if (data[link_info[1]].ToString() == value.ToString())
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

            if (((JArray)ParsedQuery["parameters"]).Count == 0)
            {
                throw new Exception("Query Error: No columns to select in the query.");
            }

            if (ParsedQuery["extensions"]["where"] != null)
            {
                if (((JArray)ParsedQuery["extensions"]["where"]).Count > 0)
                {
                    JArray res = new JArray();
                    for (int i = 0, l = ((JArray)ParsedQuery["extensions"]["where"]).Count; i < l; i++)
                    {
                        res = Util.Merge(res, _filter((JObject)data["data"], (JArray)ParsedQuery["extensions"]["where"][i]));
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

        private JArray _count(JObject data)
        {
            JArray rows = (JArray)data["prototype"].DeepClone();
            JArray result = Util.Values((JObject)data["data"]);
            JArray final = new JArray();

            if (Array.IndexOf(ParsedQuery["parameters"].ToArray(), "*") == -1)
            {
                rows = (JArray)ParsedQuery["parameters"];
            }

            if (ParsedQuery["extensions"]["where"] != null)
            {
                if (((JArray)ParsedQuery["extensions"]["where"]).Count > 0)
                {
                    JArray res = new JArray();
                    for (int i = 0, l = ((JArray)ParsedQuery["extensions"]["where"]).Count; i < l; i++)
                    {
                        res = Util.Merge(res, _filter((JObject)data["data"], (JArray)ParsedQuery["extensions"]["where"][i]));
                    }
                    result = res;
                }
            }

            if (ParsedQuery["extensions"]["group"] != null)
            {
                JArray used = new JArray();
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    var array_data_p = result[i];
                    string current_column = ParsedQuery["extensions"]["group"][0].ToString();
                    string current_data = array_data_p[current_column].ToString();
                    var current_counter = 0;
                    if (Array.IndexOf(used.ToArray(), current_data) == -1)
                    {
                        for (int j = 0; j < l; j++)
                        {
                            var array_data_c = result[j];
                            if (array_data_c[current_column].ToString() == current_data)
                            {
                                ++current_counter;
                            }
                        }
                        JObject add = new JObject();
                        if (ParsedQuery["extensions"]["as"] != null)
                        {
                            add[ParsedQuery["extensions"]["as"][0].ToString()] = current_counter;
                        }
                        else
                        {
                            add["count(" + String.Join(",", (JArray)ParsedQuery["parameters"]) + ")"] = current_counter;
                        }
                        add[current_column] = current_data;
                        final.Add(add);
                        used.Add(current_data);
                    }
                }
            }
            else
            {
                JObject counter = new JObject();
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    var array_data = result[i];
                    for (int j = 0, m = rows.Count; j < m; j++)
                    {
                        string row = rows[j].ToString();
                        if (array_data[row] != null)
                        {
                            counter[row] = counter[row] != null ? 1 + (int)counter[row] : counter[row] = 1;
                        }
                    }
                }
                var count = counter.Count > 0 ? (int)Util.Values(counter).Max() : 0;

                JObject temp = new JObject();
                if (ParsedQuery["extensions"]["as"] != null)
                {
                    Console.WriteLine(ParsedQuery.ToString());
                    temp[ParsedQuery["extensions"]["as"][0].ToString()] = count;
                }
                else
                {
                    temp["count(" + String.Join(",", (JArray)ParsedQuery["parameters"]) + ")"] = count;
                }

                final.Add(temp);
            }

            return final;
        }

        private JValue _truncate(JObject data)
        {
            data["properties"]["last_insert_id"] = 0;
            data["properties"]["last_valid_row_id"] = 0;
            data["data"] = new JObject();

            string path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt");

            try
            {
                Cache.Update(path, data.ToString());
                Database.WriteTableData(path, data);
                return new JValue(true);
            }
            catch (Exception)
            {
                return new JValue(false);
            }
        }

        private object _delete(JObject data)
        {
            JObject current_data = (JObject)data["data"].DeepClone();
            JArray to_delete = Util.Values(current_data);

            if (ParsedQuery["extensions"]["where"] != null)
            {
                if (((JArray)ParsedQuery["extensions"]["where"]).Count > 0)
                {
                    JArray res = new JArray();
                    for (int i = 0, l = ((JArray)ParsedQuery["extensions"]["where"]).Count; i < l; i++)
                    {
                        res = Util.Merge(res, _filter((JObject)data["data"], (JArray)ParsedQuery["extensions"]["where"][i]));
                    }
                    to_delete = res;
                }
            }

            JObject final_data = (JObject)current_data.DeepClone();
            for (int i = 0, l = to_delete.Count; i < l; i++)
            {
                foreach (var item in current_data)
                {
                    if (JToken.DeepEquals(item.Value, to_delete[i]))
                    {
                        final_data.Remove(item.Key);
                    }
                }
            }

            foreach (var lid in final_data)
            {
                final_data[lid.Key] = Util.KeySort((JObject)lid.Value, (after, now) =>
                {
                    return Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString());
                });
            }

            final_data = Util.KeySort(final_data, (after, now) =>
            {
                return (int)final_data[now]["#rowid"] > (int)final_data[after]["#rowid"];
            });

            data["data"] = final_data;
            if (to_delete.Count > 0)
            {
                data["properties"]["last_valid_row_id"] = _getLastValidRowID(to_delete, false) - 1;
            }

            string path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt");

            try
            {
                Cache.Update(path, data.ToString());
                Database.WriteTableData(path, data);
                return new JValue(true);
            }
            catch (Exception)
            {
                return new JValue(false);
            }
        }

        private object _update(JObject data)
        {
            JArray result = Util.Values((JObject)data["data"]);

            if (ParsedQuery["extensions"]["where"] != null)
            {
                if (((JArray)ParsedQuery["extensions"]["where"]).Count > 0)
                {
                    JArray res = new JArray();
                    for (int i = 0, l = ((JArray)ParsedQuery["extensions"]["where"]).Count; i < l; i++)
                    {
                        res = Util.Merge(res, _filter((JObject)data["data"], (JArray)ParsedQuery["extensions"]["where"][i]));
                    }
                    result = res;
                }
            }

            if (ParsedQuery["extensions"]["with"] == null)
            {
                throw new Exception("JSONDB Error: Can't execute the \"update()\" query without values. The \"with()\" extension is required.");
            }

            int fields_nb = ((JArray)ParsedQuery["parameters"]).Count;
            int values_nb = ((JArray)ParsedQuery["extensions"]["with"]).Count;

            if (fields_nb != values_nb)
            {
                throw new Exception("JSONDB Error: Can't execute the \"update()\" query. Invalid number of parameters (trying to update \"" + fields_nb + "\" columns with \"" + values_nb + "\" values).");
            }

            JObject values = Util.Combine((JArray)ParsedQuery["parameters"], (JArray)ParsedQuery["extensions"]["with"]);

            bool pk_error = false;
            JObject non_pk = Util.Flip(Util.Diff((JArray)data["prototype"], (JArray)data["properties"]["primary_keys"]));
            foreach (var item in (JObject)data["data"])
            {
                JObject array_data = Util.DiffKey((JObject)item.Value, non_pk);
                pk_error = (pk_error || (JObject.DeepEquals(Util.DiffKey(values, non_pk), array_data) && array_data.Count > 0));
                if (pk_error)
                {
                    throw new Exception("JSONDB Error: Can't insert value. Duplicate values \"" + String.Join(", ", Util.Values(array_data)) + "\" for primary keys \"" + String.Join(", ", data["properties"]["primary_keys"]) + "\".");
                }
            }

            bool uk_error = false;
            for (int i = 0, l = ((JArray)data["properties"]["unique_keys"]).Count; i < l; i++)
            {
                string uk = data["properties"]["unique_keys"][i].ToString();
                JObject array = new JObject();
                array[uk] = values[uk];
                foreach (var item in (JObject)data["data"])
                {
                    JObject array_data = new JObject();
                    array_data[uk] = item.Value[uk];
                    uk_error = (uk_error || (array[uk] != null && JObject.DeepEquals(array, array_data)));
                    if (uk_error)
                    {
                        throw new Exception("JSONDB Error: Can't insert value. Duplicate values \"" + array[uk] + "\" for unique key \"" + uk + "\".");
                    }
                }
            }

            for (int i = 0, l = result.Count; i < l; i++)
            {
                JObject res_line = (JObject)result[i];
                foreach (var row in values)
                {
                    result[i][row.Key] = _parseValue(row.Value, (JObject)data["properties"][row.Key]);
                }
                foreach (var key in (JObject)data["data"])
                {
                    JObject data_line = (JObject)key.Value;
                    if ((int)data_line["#rowid"] == (int)res_line["#rowid"])
                    {
                        data["data"][key.Key] = result[i];
                        break;
                    }
                }
            }

            foreach (var lid in (JObject)data["data"])
            {
                data["data"][lid.Key] = Util.KeySort((JObject)lid.Value, (after, now) =>
                {
                    return Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString());
                });
            }

            data["data"] = Util.KeySort((JObject)data["data"], (after, now) =>
            {
                return (int)data["data"][now]["#rowid"] > (int)data["data"][after]["#rowid"];
            });

            int last_ai = 0;
            foreach (var item in (JObject)data["properties"])
            {
                var prop = item.Value;
                if (prop.Type == JTokenType.Object && prop["auto_increment"] != null && (bool)prop["auto_increment"])
                {
                    foreach (var lid in (JObject)data["data"])
                    {
                        last_ai = Math.Max((int)lid.Value[item.Key], last_ai);
                    }
                    break;
                }
            }

            data["properties"]["last_insert_id"] = last_ai;

            string path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt");

            try
            {
                Cache.Update(path, data.ToString());
                Database.WriteTableData(path, data);
                return new JValue(true);
            }
            catch (Exception)
            {
                return new JValue(false);
            }
        }

        private JValue _insert(JObject data)
        {
            JArray rows = (JArray)data["prototype"].DeepClone();
            rows.RemoveAt(Array.IndexOf(rows.ToArray(), "#rowid"));

            if (ParsedQuery["extensions"]["in"] != null)
            {
                rows = (JArray)ParsedQuery["extensions"]["in"];
                for (int i = 0, l = rows.Count; i < l; i++)
                {
                    if (Array.IndexOf(((JArray)data["prototype"]).ToArray(), rows[i].ToString()) == -1)
                    {
                        throw new Exception("JSONDB Error: Can't insert data in the table \"" + Table + "\". The column \"" + rows[i] + "\" doesn't exist.");
                    }
                }
            }

            if (((JArray)ParsedQuery["parameters"]).Count != rows.Count)
            {
                throw new Exception("JSONDB Error: Can't insert data in the table \"" + Table + "\". Invalid number of parameters (given \"" + ((JArray)ParsedQuery["parameters"]).Count + "\" values to insert in \"" + rows.Count + "\" columns).");
            }

            JObject current_data = (JObject)data["data"];
            int ai_id = (int)data["properties"]["last_insert_id"];
            int lk_id = (int)data["properties"]["last_link_id"] + 1;
            JObject insert = new JObject();

            insert["#" + lk_id] = new JObject();
            insert["#" + lk_id]["#rowid"] = (int)data["properties"]["last_valid_row_id"] + 1;
            for (int i = 0, l = ((JArray)ParsedQuery["parameters"]).Count; i < l; i++)
            {
                insert["#" + lk_id][rows[i].ToString()] = _parseValue(ParsedQuery["parameters"][i], (JObject)data["properties"][rows[i].ToString()]);
            }

            if (ParsedQuery["extensions"]["and"] != null)
            {
                for (int i = 0, l = ((JArray)ParsedQuery["extensions"]["and"]).Count; i < l; i++)
                {
                    JArray values = (JArray)ParsedQuery["extensions"]["and"][i];
                    if (values.Count != rows.Count)
                    {
                        throw new Exception("JSONDB Error: Can't insert data in the table \"" + Table + "\". Invalid number of parameters (given \"" + values.Count + "\" values to insert in \"" + rows.Count + "\" columns).");
                    }
                    JObject to_add = new JObject();
                    to_add["#rowid"] = _getLastValidRowID(Util.Merge(current_data, insert), false) + 1;
                    for (int k = 0, vl = values.Count; k < vl; k++)
                    {
                        to_add[rows[k].ToString()] = _parseValue(values[k], (JObject)data["properties"][rows[k].ToString()]);
                    }
                    insert["#" + (++lk_id)] = to_add;
                }
            }

            foreach (var item in (JObject)data["properties"])
            {
                var prop = item.Value;
                if (prop.Type == JTokenType.Object && prop["auto_increment"] != null && (bool)prop["auto_increment"])
                {
                    foreach (var lid in insert)
                    {
                        var val = insert[lid.Key][item.Key];
                        if (val != null && val.Type != JTokenType.Null)
                        {
                            continue;
                        }
                        insert[lid.Key][item.Key] = ++ai_id;
                    }
                    break;
                }
            }

            for (int i = 0, l = ((JArray)data["prototype"]).Count; i < l; i++)
            {
                foreach (var item in insert)
                {
                    if (insert[item.Key][data["prototype"][i].ToString()] == null)
                    {
                        insert[item.Key][data["prototype"][i].ToString()] = _parseValue(null, (JObject)data["properties"][data["prototype"][i].ToString()]);
                    }
                }
            }

            insert = Util.Merge(current_data, insert);

            bool pk_error = false;
            JObject non_pk = Util.Flip(Util.Diff((JArray)data["prototype"], (JArray)data["properties"]["primary_keys"]));
            int index = 0;
            foreach (var lid in insert)
            {
                JObject array_data = Util.DiffKey((JObject)insert[lid.Key], non_pk);
                foreach (var slid in Util.Slice(insert, index + 1))
                {
                    JObject value = Util.DiffKey((JObject)slid.Value, non_pk);
                    pk_error = (pk_error || (JObject.DeepEquals(value, array_data) && array_data.Count > 0));
                    if (pk_error)
                    {
                        throw new Exception("JSONDB Error: Can't insert value. Duplicate values \"" + String.Join(", ", Util.Values(value)) + "\" for primary keys \"" + String.Join(", ", (JArray)data["properties"]["primary_keys"]) + "\".");
                    }
                }
                ++index;
            }

            bool uk_error = false;
            index = 0;
            for (int i = 0, l = ((JArray)data["properties"]["unique_keys"]).Count; i < l; i++)
            {
                string uk = data["properties"]["unique_keys"][i].ToString();
                foreach (var lid in insert)
                {
                    JObject array_data = new JObject();
                    array_data[uk] = lid.Value[uk];
                    foreach (var slid in Util.Slice(insert, index + 1))
                    {
                        JObject value = new JObject();
                        value[uk] = slid.Value[uk];
                        uk_error = (uk_error || (value[uk] != null && JObject.DeepEquals(value, array_data)));
                        if (uk_error)
                        {
                            throw new Exception("JSONDB Error: Can't insert value. Duplicate value \"" + value[uk] + "\" for unique key \"" + uk + "\".");
                        }
                    }
                    ++index;
                }
            }

            foreach (var lid in insert)
            {
                insert[lid.Key] = Util.KeySort((JObject)lid.Value, (after, now) =>
                {
                    return Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString());
                });
            }

            insert = Util.KeySort(insert, (after, now) =>
            {
                return (int)insert[now]["#rowid"] > (int)insert[after]["#rowid"];
            });

            int last_ai = 0;
            foreach (var item in (JObject)data["properties"])
            {
                var prop = item.Value;
                if (prop.Type == JTokenType.Object && prop["auto_increment"] != null && (bool)prop["auto_increment"])
                {
                    foreach (var lid in insert)
                    {
                        last_ai = Math.Max((int)lid.Value[item.Key], last_ai);
                    }
                    break;
                }
            }

            data["data"] = insert;
            data["properties"]["last_valid_row_id"] = _getLastValidRowID(insert, false);
            data["properties"]["last_insert_id"] = last_ai;
            data["properties"]["last_link_id"] = lk_id;

            string path = Util.MakePath(DBConnection.GetServer(), DBConnection.GetDatabase(), Table + ".jdbt");

            try
            {
                Cache.Update(path, data.ToString());
                Database.WriteTableData(path, data);
                return new JValue(true);
            }
            catch (Exception)
            {
                return new JValue(false);
            }
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
