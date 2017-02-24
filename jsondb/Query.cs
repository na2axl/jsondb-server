using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace JSONDB
{
    /// <summary>
    /// Class Query
    /// </summary>
    public class Query
    {
        /// <summary>
        /// The database instance in which the query will be executed.
        /// </summary>
        private Database _db;

        /// <summary>
        /// The name of the table.
        /// </summary>
        private string _table;

        /// <summary>
        /// Define is the query is executed or not.
        /// </summary>
        private bool _queryExecuted;

        /// <summary>
        /// The parsed query object.
        /// </summary>
        private JObject _parsedQuery;

        /// <summary>
        /// Create a new instance of <see cref="Query"/> by defining
        /// the <see cref="Database"/> instance.
        /// </summary>
        /// <param name="database">The <see cref="Database"/> instance to use</param>
        public Query(Database database)
        {
            _db = database;
        }

        /// <summary>
        /// Send a new query to the server.
        /// </summary>
        /// <param name="query">The query to send (in JQL format)</param>
        /// <returns>The query result</returns>
        public JObject Send(string query)
        {
            _parsedQuery = QueryParser.Parse(query);

            _queryExecuted = false;

            return _execute();
        }

        /// <summary>
        /// Send multiples queries at once.
        /// </summary>
        /// <param name="queries">Queries to send (in JQL format)</param>
        /// <returns>A set of query results</returns>
        public JObject[] MultiSend(string queries)
        {
            var parsedQueries = QueryParser.MultilineParse(queries);
            var results = new JObject[parsedQueries.Length];

            for (int i = 0, l = parsedQueries.Length; i < l; i++)
            {
                _parsedQuery = parsedQueries[i];

                _queryExecuted = false;

                results[i] = _execute();
            }

            return results;
        }

        /// <summary>
        /// Execute the current query.
        /// </summary>
        /// <returns>The query result</returns>
        private JObject _execute()
        {
            if (_queryExecuted)
            {
                throw new InvalidOperationException("Query Error: There is no query to execute, or the query is already executed.");
            }

            _setTable(_parsedQuery["table"].ToString());

            var tablePath = Util.MakePath(_db.GetServer(), _db.GetDatabase(), _table + ".jdbt");

            if (!Util.Exists(tablePath))
            {
                throw new Exception("Query Error: Can't execute the query. The table \"" + _table + "\" doesn't exists in database \"" + _db.GetDatabase() + "\" or file access denied.");
            }

            // Wait until the file is unlocked and check the state each 100ms
            while (Util.FileIsLocked(tablePath))
            {
                Thread.Sleep(100);
            }

            Benchmark.Mark("jsondb_(query)_start");
            Util.LockFile(tablePath);
            Cache.Reset();

            var jsonArray = JObject.Parse(Cache.Get(tablePath));
            var queryResult = new JObject();

            try
            {
                dynamic res;
                switch (_parsedQuery["action"].ToString())
                {
                    case "select":
                        res = _select(jsonArray);
                        break;
                    case "insert":
                        res = _insert(jsonArray);
                        break;
                    case "update":
                        res = _update(jsonArray);
                        break;
                    case "truncate":
                        res = _truncate(jsonArray);
                        break;
                    case "count":
                        res = _count(jsonArray);
                        break;
                    case "delete":
                        res = _delete(jsonArray);
                        break;
                    default:
                        Util.UnlockFile(tablePath);
                        Benchmark.Mark("jsondb_(query)_end");
                        throw new NotSupportedException("Query Error: The query \"" + _parsedQuery["action"] + "\" is not supported by JSONDB.");
                }

                Util.UnlockFile(tablePath);
                Benchmark.Mark("jsondb_(query)_end");

                _queryExecuted = true;
                queryResult["error"] = false;
                queryResult["result"] = (JToken)res;
                queryResult["elapsed_time"] = Benchmark.ElapsedTime("jsondb_(query)_start", "jsondb_(query)_end");
                queryResult["memory_usage"] = Benchmark.MemoryUsage("jsondb_(query)_start", "jsondb_(query)_end");
            }
            catch (Exception e)
            {
                Util.UnlockFile(tablePath);
                Benchmark.Mark("jsondb_(query)_end");

                _queryExecuted = false;
                queryResult["error"] = true;
                queryResult["result"] = e.Message;
                queryResult["elapsed_time"] = Benchmark.ElapsedTime("jsondb_(query)_start", "jsondb_(query)_end");
                queryResult["memory_usage"] = Benchmark.MemoryUsage("jsondb_(query)_start", "jsondb_(query)_end");
            }

            return queryResult;
        }

        /// <summary>
        /// Retrieve the last valid row ID from the table's data.
        /// </summary>
        /// <param name="data">Data in which retrieve the ID.</param>
        /// <param name="min">Define if we have to get the minimal or the maximal value</param>
        /// <returns>The last valid row ID</returns>
        private int _getLastValidRowID(JObject data, bool min)
        {
            var last = 0;
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

        /// <summary>
        /// Retrieve the last valid row ID from the table's data.
        /// </summary>
        /// <param name="data">Data in which retrieve the ID.</param>
        /// <param name="min">Define if we have to get the minimal or the maximal value</param>
        /// <returns>The last valid row ID</returns>
        private int _getLastValidRowID(JArray data, bool min)
        {
            var last = 0;
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

        /// <summary>
        /// Change the current working table.
        /// </summary>
        /// <param name="table">The name of the table</param>
        private void _setTable(string table)
        {
            if (!_db.IsWorkingDatabase())
            {
                throw new Exception("Query Error: Can't use the table \"" + table + "\", there is no database selected.");
            }

            var path = Util.MakePath(_db.GetServer(), _db.GetDatabase(), table + ".jdbt");
            if (!Util.Exists(path))
            {
                throw new Exception("Query Error: Can't use the table \"" + table + "\", the table doesn't exist in the database.");
            }

            _table = table;
        }

        /// <summary>
        /// Parse a value.
        /// </summary>
        /// <param name="value">The value to parse</param>
        /// <param name="properties">A set of properties used to parse the value</param>
        /// <returns>The parsed value</returns>
        private JToken _parseValue(JToken value, JObject properties)
        {
            if (value != null || (properties["not_null"] != null && bool.Parse(properties["not_null"].ToString())))
            {
                if (properties["type"] == null)
                {
                    return value;
                }
                else if (Regex.IsMatch(properties["type"].ToString(), "link\\(.+\\)"))
                {
                    var link = Regex.Replace(properties["type"].ToString(), "link\\((.+)\\)", "$1");
                    var linkInfo = link.Split('.');
                    var linkTablePath = Util.MakePath(_db.GetServer(), _db.GetDatabase(), linkInfo[0] + ".jdbt");
                    var linkTableData = Database.GetTableData(linkTablePath);
                    value = _parseValue(value, (JObject)linkTableData["properties"][linkInfo[1]]);
                    foreach (var item in (JObject)linkTableData["data"])
                    {
                        var data = (JObject)item.Value;
                        if (data[linkInfo[1]].ToString() == value.ToString())
                        {
                            return item.Key;
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
                            value = int.TryParse(value.ToString(), out i) ? i : 0;
                            break;
                        case "decimal":
                        case "float":
                            float f;
                            value = float.TryParse(value.ToString(), out f) ? f : 0f;
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
                            value = bool.TryParse(value.ToString(), out b) ? b : !string.IsNullOrEmpty(value.ToString());
                            break;
                        case "array":
                            value = JObject.Parse(value.ToString());
                            break;
                        default:
                            throw new NotSupportedException("JSONDB Error: Trying to parse a value with an unsupported type \"" + properties["type"].ToString() + "\"");
                    }
                }
            }
            else if (properties["default"] != null)
            {
                value = _parseValue(properties["default"], properties);
            }

            return value;
        }

        /// <summary>
        /// Parse and execute a function used in the select() query.
        /// </summary>
        /// <param name="func">The function name</param>
        /// <param name="row">The table's row on which the function will be applied</param>
        /// <param name="value">The value on which the function will be applied</param>
        /// <param name="data">The table data</param>
        /// <returns>The function's result</returns>
        private JToken _parseSelectFunction(string func, string row, string value, JArray data)
        {
            var temp = new List<JToken>();

            switch (func)
            {
                case "sha1":
                    return Util.Sha1(value);
                case "md5":
                    return Util.Md5(value);
                case "lowercase":
                    return value.ToLower();
                case "uppercase":
                    return value.ToUpper();
                case "ucfirst":
                    return value[0].ToString().ToUpper() + value.Substring(1).ToLower();
                case "strlen":
                    return value.Length;
                case "min":
                    for (int i = 0, l = data.Count; i < l; i++)
                    {
                        var line = (JObject)data[i];
                        temp.Add(line[row.ToString()]);
                    }

                    var min = temp.Select((val) =>
                    {
                        float m;
                        return float.TryParse(val.ToString(), out m) ? m : 0;
                    }).Min();

                    return min;
                case "max":
                    for (int i = 0, l = data.Count; i < l; i++)
                    {
                        var line = (JObject)data[i];
                        temp.Add(line[row.ToString()]);
                    }

                    var max = temp.Select((val) =>
                    {
                        float m;
                        return float.TryParse(val.ToString(), out m) ? m : 0;
                    }).Max();

                    return max;
                case "sum":
                    for (int i = 0, l = data.Count; i < l; i++)
                    {
                        var line = (JObject)data[i];
                        temp.Add(line[row.ToString()]);
                    }

                    var sum = temp.Select((val) =>
                    {
                        float m;
                        return float.TryParse(val.ToString(), out m) ? m : 0;
                    }).Sum();

                    return sum;
                case "avg":
                    for (int i = 0, l = data.Count; i < l; i++)
                    {
                        var line = (JObject)data[i];
                        temp.Add(line[row.ToString()]);
                    }

                    var avg = temp.Select((val) =>
                    {
                        float m;
                        return float.TryParse(val.ToString(), out m) ? m : 0;
                    }).Average();

                    return avg;
                default:
                    throw new NotImplementedException("JSONDB Query Parse Error: Sorry but the function " + func + "() is not implemented in JQL.");

            }
        }

        /// <summary>
        /// Parse and execute a function used in the where() extension.
        /// </summary>
        /// <param name="func">The function name</param>
        /// <param name="value">The value on which the function will be applied</param>
        /// <returns>The function's result</returns>
        private JToken _parseWhereFunction(string func, string value)
        {
            switch (func)
            {
                case "sha1":
                    return Util.Sha1(value);
                case "md5":
                    return Util.Md5(value);
                case "lowercase":
                    return value.ToLower();
                case "uppercase":
                    return value.ToUpper();
                case "ucfirst":
                    return value[0].ToString().ToUpper() + value.Substring(1).ToLower();
                case "strlen":
                    return value.Length;
                default:
                    throw new NotImplementedException("JSONDB Query Parse Error: Sorry but the function " + func + "() is not implemented in JQL.");

            }
        }

        /// <summary>
        /// Execute the select() query.
        /// </summary>
        /// <param name="data">The table's data</param>
        /// <returns>The query result</returns>
        private JArray _select(JObject data)
        {
            var result = Util.Values((JObject)data["data"]);

            if (((JArray)_parsedQuery["parameters"]).Count == 0)
            {
                throw new Exception("Query Error: No columns to select in the query.");
            }

            if (((JArray)_parsedQuery["extensions"]["where"])?.Count > 0)
            {
                var res = new JArray();
                for (int i = 0, l = ((JArray)_parsedQuery["extensions"]["where"]).Count; i < l; i++)
                {
                    res = Util.Merge(res, _filter((JObject)data["data"], (JArray)_parsedQuery["extensions"]["where"][i]));
                }
                result = res;
            }

            if (_parsedQuery["extensions"]["order"] != null)
            {
                var orderBy = _parsedQuery["extensions"]["order"][0].ToString();
                var orderMethod = _parsedQuery["extensions"]["order"][1].ToString();

                result = Util.Sort(result, (now, pivot) =>
                {
                    int left, right;
                    if (orderMethod == "desc")
                    {
                        return int.TryParse(now[orderBy].ToString(), out left) && int.TryParse(pivot[orderBy].ToString(), out right) ? left > right : string.Compare(now[orderBy].ToString(), pivot[orderBy].ToString()) == 1;
                    }
                    return int.TryParse(now[orderBy].ToString(), out left) && int.TryParse(pivot[orderBy].ToString(), out right) ? left < right : string.Compare(now[orderBy].ToString(), pivot[orderBy].ToString()) == -1;
                }, (now, pivot) =>
                {
                    int left, right;
                    if (orderMethod == "desc")
                    {
                        return int.TryParse(now[orderBy].ToString(), out left) && int.TryParse(pivot[orderBy].ToString(), out right) ? left < right : string.Compare(now[orderBy].ToString(), pivot[orderBy].ToString()) == -1;
                    }
                    return int.TryParse(now[orderBy].ToString(), out left) && int.TryParse(pivot[orderBy].ToString(), out right) ? left > right : string.Compare(now[orderBy].ToString(), pivot[orderBy].ToString()) == 1;
                });
            }

            if (_parsedQuery["extensions"]["limit"] != null)
            {
                result = Util.Slice(result, int.Parse(_parsedQuery["extensions"]["limit"][0].ToString()), int.Parse(_parsedQuery["extensions"]["limit"][1].ToString()));
            }

            if (_parsedQuery["extensions"]["on"] != null)
            {
                for (int i = 0, l = ((JArray)_parsedQuery["extensions"]["on"]).Count; i < l; i++)
                {
                    var on = _parsedQuery["extensions"]["on"][i];
                    switch (on["action"]["name"].ToString())
                    {
                        case "link":
                            var key = on["column"].ToString();
                            var columns = (JArray)on["action"]["parameters"];

                            for (int j = 0, m = result.Count; j < m; j++)
                            {
                                var resultP = result[j];
                                if (Regex.IsMatch(data["properties"][key]["type"].ToString(), "link\\((.+)\\)"))
                                {
                                    var link = Regex.Replace(data["properties"][key]["type"].ToString(), "link\\((.+)\\)", "$1");
                                    var linkInfo = link.Split('.');
                                    var linkTablePath = Util.MakePath(_db.GetServer(), _db.GetDatabase(), linkInfo[0] + ".jdbt");
                                    var linkTableData = Database.GetTableData(linkTablePath);

                                    foreach (var linkId in (JObject)linkTableData["data"])
                                    {
                                        if (linkId.Key != resultP[key].ToString()) continue;
                                        if (Array.IndexOf(columns.ToArray(), "*") != -1)
                                        {
                                            columns = (JArray)linkTableData["prototype"];
                                            columns.RemoveAt(Array.IndexOf(columns.ToArray(), "#rowid"));
                                        }
                                        result[j][key] = Util.IntersectKey((JObject)linkId.Value, Util.Flip(columns));
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

            var temp = new JArray();
            if (Array.IndexOf(((JArray)_parsedQuery["parameters"]).ToArray(), "last_insert_id") != -1)
            {
                var res = new JObject { ["last_insert_id"] = data["properties"]["last_insert_id"] };
                temp.Add(res);
            }
            else if (Array.IndexOf(((JArray)_parsedQuery["parameters"]).ToArray(), "*") != -1)
            {
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    var line = (JObject)result[i];
                    line.Remove("#rowid");
                    temp.Add(line);
                }
            }
            else
            {
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    var line = (JObject)result[i];
                    var res = new JObject();

                    for (int j = 0, m = ((JArray)_parsedQuery["parameters"]).Count; j < m; j++)
                    {
                        var field = _parsedQuery["parameters"][j].ToString();
                        if (Regex.IsMatch(field, "\\w+\\(.*\\)"))
                        {
                            var parts = Regex.Replace(field, "(\\w+)\\((.*)\\)", "$1.$2").Split('.');
                            var name = parts[0].ToLower();
                            var param = parts[1];

                            res[field] = _parseSelectFunction(name, param, line[param].ToString(), result);
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
                if (_parsedQuery["extensions"]["as"] != null)
                {
                    for (int i = 0, l = ((JArray)_parsedQuery["parameters"]).Count - ((JArray)_parsedQuery["extensions"]["as"]).Count; i < l; i++)
                    {
                        ((JArray)_parsedQuery["extensions"]["as"]).Add("null");
                    }
                    var replace = Util.Combine((JArray)_parsedQuery["parameters"], (JArray)_parsedQuery["extensions"]["as"]);
                    for (int i = 0, l = temp.Count; i < l; i++)
                    {
                        foreach (var item in replace)
                        {
                            var n = item.Value.ToString();
                            if (n.ToLower() == item.Key.ToLower())
                                break;
                            if (n.ToLower() == "null")
                                continue;
                            temp[i][n] = temp[i][item.Key];
                            ((JObject)temp[i]).Remove(item.Key);
                        }
                    }
                }
            }

            result = temp;

            return result;
        }

        /// <summary>
        /// Execute the count() query.
        /// </summary>
        /// <param name="data">The table's data</param>
        /// <returns>The query result</returns>
        private JArray _count(JObject data)
        {
            var rows = (JArray)data["prototype"].DeepClone();
            var result = Util.Values((JObject)data["data"]);
            var final = new JArray();

            if (Array.IndexOf(_parsedQuery["parameters"].ToArray(), "*") == -1)
            {
                rows = (JArray)_parsedQuery["parameters"];
            }

            if (((JArray)_parsedQuery["extensions"]["where"])?.Count > 0)
            {
                var res = new JArray();
                for (int i = 0, l = ((JArray)_parsedQuery["extensions"]["where"]).Count; i < l; i++)
                {
                    res = Util.Merge(res, _filter((JObject)data["data"], (JArray)_parsedQuery["extensions"]["where"][i]));
                }
                result = res;
            }

            if (_parsedQuery["extensions"]["group"] != null)
            {
                var used = new JArray();
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    var arrayDataP = result[i];
                    var currentColumn = _parsedQuery["extensions"]["group"][0].ToString();
                    var currentData = arrayDataP[currentColumn].ToString();
                    var currentCounter = 0;
                    if (Array.IndexOf(used.ToArray(), currentData) != -1) continue;
                    for (var j = 0; j < l; j++)
                    {
                        var arrayDataC = result[j];
                        if (arrayDataC[currentColumn].ToString() == currentData)
                        {
                            ++currentCounter;
                        }
                    }
                    var add = new JObject();
                    if (_parsedQuery["extensions"]["as"] != null)
                    {
                        add[_parsedQuery["extensions"]["as"][0].ToString()] = currentCounter;
                    }
                    else
                    {
                        add["count(" + string.Join(",", (JArray)_parsedQuery["parameters"]) + ")"] = currentCounter;
                    }
                    add[currentColumn] = currentData;
                    final.Add(add);
                    used.Add(currentData);
                }
            }
            else
            {
                var counter = new JObject();
                for (int i = 0, l = result.Count; i < l; i++)
                {
                    var arrayData = result[i];
                    for (int j = 0, m = rows.Count; j < m; j++)
                    {
                        var row = rows[j].ToString();
                        if (arrayData[row] != null)
                        {
                            counter[row] = counter[row] != null ? 1 + (int)counter[row] : counter[row] = 1;
                        }
                    }
                }
                var count = counter.Count > 0 ? (int)Util.Values(counter).Max() : 0;

                var temp = new JObject();
                if (_parsedQuery["extensions"]["as"] != null)
                {
                    temp[_parsedQuery["extensions"]["as"][0].ToString()] = count;
                }
                else
                {
                    temp["count(" + string.Join(",", (JArray)_parsedQuery["parameters"]) + ")"] = count;
                }

                final.Add(temp);
            }

            return final;
        }

        /// <summary>
        /// Execute the truncate() query.
        /// </summary>
        /// <param name="data">The table's data</param>
        /// <returns>The query result</returns>
        private JValue _truncate(JObject data)
        {
            data["properties"]["last_insert_id"] = 0;
            data["properties"]["last_valid_row_id"] = 0;
            data["data"] = new JObject();

            var path = Util.MakePath(_db.GetServer(), _db.GetDatabase(), _table + ".jdbt");

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

        /// <summary>
        /// Execute the delete() query.
        /// </summary>
        /// <param name="data">The table's data</param>
        /// <returns>The query result</returns>
        private JValue _delete(JObject data)
        {
            var currentData = (JObject)data["data"].DeepClone();
            var toDelete = Util.Values(currentData);

            if (((JArray)_parsedQuery["extensions"]["where"])?.Count > 0)
            {
                var res = new JArray();
                for (int i = 0, l = ((JArray)_parsedQuery["extensions"]["where"]).Count; i < l; i++)
                {
                    res = Util.Merge(res, _filter((JObject)data["data"], (JArray)_parsedQuery["extensions"]["where"][i]));
                }
                toDelete = res;
            }

            var finalData = (JObject)currentData.DeepClone();
            for (int i = 0, l = toDelete.Count; i < l; i++)
            {
                foreach (var item in currentData)
                {
                    if (JToken.DeepEquals(item.Value, toDelete[i]))
                    {
                        finalData.Remove(item.Key);
                    }
                }
            }

            foreach (var lid in finalData)
            {
                finalData[lid.Key] = Util.KeySort(
                    (JObject)lid.Value,
                    (after, now) => Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString()),
                    (after, now) => Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) < Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString())
                );
            }

            var copy = finalData;
            finalData = Util.KeySort(
                finalData,
                (after, now) => (int)copy[now.ToString()]["#rowid"] > (int)copy[after.ToString()]["#rowid"],
                (after, now) => (int)copy[now.ToString()]["#rowid"] < (int)copy[after.ToString()]["#rowid"]
            );

            data["data"] = finalData;
            if (toDelete.Count > 0)
            {
                data["properties"]["last_valid_row_id"] = _getLastValidRowID(toDelete, false) - 1;
            }

            var path = Util.MakePath(_db.GetServer(), _db.GetDatabase(), _table + ".jdbt");

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

        /// <summary>
        /// Execute the update() query.
        /// </summary>
        /// <param name="data">The table's data</param>
        /// <returns>The query result</returns>
        private JValue _update(JObject data)
        {
            var result = Util.Values((JObject)data["data"]);

            if (((JArray)_parsedQuery["extensions"]["where"])?.Count > 0)
            {
                var res = new JArray();
                for (int i = 0, l = ((JArray)_parsedQuery["extensions"]["where"]).Count; i < l; i++)
                {
                    res = Util.Merge(res, _filter((JObject)data["data"], (JArray)_parsedQuery["extensions"]["where"][i]));
                }
                result = res;
            }

            if (_parsedQuery["extensions"]["with"] == null)
            {
                throw new Exception("JSONDB Error: Can't execute the \"update()\" query without values. The \"with()\" extension is required.");
            }

            var fieldsNb = ((JArray)_parsedQuery["parameters"]).Count;
            var valuesNb = ((JArray)_parsedQuery["extensions"]["with"]).Count;

            if (fieldsNb != valuesNb)
            {
                throw new Exception("JSONDB Error: Can't execute the \"update()\" query. Invalid number of parameters (trying to update \"" + fieldsNb + "\" columns with \"" + valuesNb + "\" values).");
            }

            var values = Util.Combine((JArray)_parsedQuery["parameters"], (JArray)_parsedQuery["extensions"]["with"]);

            for (int i = 0, l = result.Count; i < l; i++)
            {
                var resLine = (JObject)result[i];
                foreach (var row in values)
                {
                    result[i][row.Key] = _parseValue(row.Value, (JObject)data["properties"][row.Key]);
                }
                foreach (var key in (JObject)data["data"])
                {
                    var dataLine = (JObject)key.Value;
                    if ((int)dataLine["#rowid"] != (int)resLine["#rowid"]) continue;
                    data["data"][key.Key] = result[i];
                    break;
                }
            }

            var pkError = false;
            var nonPk = Util.Flip(Util.Diff((JArray)data["prototype"], (JArray)data["properties"]["primary_keys"]));
            foreach (var item in (JObject)data["data"])
            {
                var arrayData = Util.DiffKey((JObject)item.Value, nonPk);
                pkError = (pkError || (JToken.DeepEquals(Util.DiffKey(values, nonPk), arrayData) && arrayData.Count > 0));
                if (pkError)
                {
                    throw new Exception("JSONDB Error: Can't insert value. Duplicate values \"" + string.Join(", ", Util.Values(arrayData)) + "\" for primary keys \"" + string.Join(", ", data["properties"]["primary_keys"]) + "\".");
                }
            }

            var ukError = false;
            for (int i = 0, l = ((JArray)data["properties"]["unique_keys"]).Count; i < l; i++)
            {
                var uk = data["properties"]["unique_keys"][i].ToString();
                var array = new JObject { [uk] = values[uk] };
                foreach (var item in (JObject)data["data"])
                {
                    var arrayData = new JObject { [uk] = item.Value[uk] };
                    ukError = (ukError || (array[uk] != null && JToken.DeepEquals(array, arrayData)));
                    if (ukError)
                    {
                        throw new Exception("JSONDB Error: Can't insert value. Duplicate values \"" + array[uk] + "\" for unique key \"" + uk + "\".");
                    }
                }
            }

            foreach (var lid in (JObject)data["data"])
            {
                data["data"][lid.Key] = Util.KeySort(
                    (JObject)lid.Value,
                    (after, now) => Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString()),
                    (after, now) => Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString())
                );
            }

            data["data"] = Util.KeySort(
                (JObject)data["data"],
                (after, now) => (int)data["data"][now.ToString()]["#rowid"] > (int)data["data"][after.ToString()]["#rowid"],
                (after, now) => (int)data["data"][now.ToString()]["#rowid"] < (int)data["data"][after.ToString()]["#rowid"]
            );

            var lastAi = 0;
            foreach (var item in (JObject)data["properties"])
            {
                var prop = item.Value;

                if (prop.Type != JTokenType.Object || prop["auto_increment"] == null || !(bool)prop["auto_increment"])
                    continue;

                foreach (var lid in (JObject)data["data"])
                {
                    lastAi = Math.Max((int)lid.Value[item.Key], lastAi);
                }
                break;
            }

            data["properties"]["last_insert_id"] = lastAi;

            var path = Util.MakePath(_db.GetServer(), _db.GetDatabase(), _table + ".jdbt");

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

        /// <summary>
        /// Execute the insert() query.
        /// </summary>
        /// <param name="data">The table's data</param>
        /// <returns>The query result</returns>
        private JValue _insert(JObject data)
        {
            var rows = (JArray)data["prototype"].DeepClone();
            rows.RemoveAt(Array.IndexOf(rows.ToArray(), "#rowid"));

            if (_parsedQuery["extensions"]["in"] != null)
            {
                rows = (JArray)_parsedQuery["extensions"]["in"];
                for (int i = 0, l = rows.Count; i < l; i++)
                {
                    if (Array.IndexOf(((JArray)data["prototype"]).ToArray(), rows[i].ToString()) == -1)
                    {
                        throw new Exception("JSONDB Error: Can't insert data in the table \"" + _table + "\". The column \"" + rows[i] + "\" doesn't exist.");
                    }
                }
            }

            if (((JArray)_parsedQuery["parameters"]).Count != rows.Count)
            {
                throw new Exception("JSONDB Error: Can't insert data in the table \"" + _table + "\". Invalid number of parameters (given \"" + ((JArray)_parsedQuery["parameters"]).Count + "\" values to insert in \"" + rows.Count + "\" columns).");
            }

            var currentData = (JObject)data["data"];
            var aiId = (int)data["properties"]["last_insert_id"];
            var lkId = (int)data["properties"]["last_link_id"] + 1;
            var insert = new JObject
            {
                ["#" + lkId] = new JObject { ["#rowid"] = (int)data["properties"]["last_valid_row_id"] + 1 }
            };

            for (int i = 0, l = ((JArray)_parsedQuery["parameters"]).Count; i < l; i++)
            {
                insert["#" + lkId][rows[i].ToString()] = _parseValue(_parsedQuery["parameters"][i], (JObject)data["properties"][rows[i].ToString()]);
            }

            if (_parsedQuery["extensions"]["and"] != null)
            {
                for (int i = 0, l = ((JArray)_parsedQuery["extensions"]["and"]).Count; i < l; i++)
                {
                    var values = (JArray)_parsedQuery["extensions"]["and"][i];
                    if (values.Count != rows.Count)
                    {
                        throw new Exception("JSONDB Error: Can't insert data in the table \"" + _table + "\". Invalid number of parameters (given \"" + values.Count + "\" values to insert in \"" + rows.Count + "\" columns).");
                    }
                    var toAdd = new JObject
                    {
                        ["#rowid"] = _getLastValidRowID(Util.Merge(currentData, insert), false) + 1
                    };
                    for (int k = 0, vl = values.Count; k < vl; k++)
                    {
                        toAdd[rows[k].ToString()] = _parseValue(values[k], (JObject)data["properties"][rows[k].ToString()]);
                    }
                    insert["#" + (++lkId)] = toAdd;
                }
            }

            foreach (var item in (JObject)data["properties"])
            {
                var prop = item.Value;

                if (prop.Type != JTokenType.Object || prop["auto_increment"] == null || !(bool)prop["auto_increment"])
                    continue;

                foreach (var lid in insert)
                {
                    var val = insert[lid.Key][item.Key];
                    if (val != null && val.Type != JTokenType.Null)
                    {
                        continue;
                    }
                    insert[lid.Key][item.Key] = ++aiId;
                }
                break;
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

            insert = Util.Merge(currentData, insert);

            var pkError = false;
            var nonPk = Util.Flip(Util.Diff((JArray)data["prototype"], (JArray)data["properties"]["primary_keys"]));
            var index = 0;
            foreach (var lid in insert)
            {
                var arrayData = Util.DiffKey((JObject)insert[lid.Key], nonPk);
                foreach (var slid in Util.Slice(insert, index + 1))
                {
                    var value = Util.DiffKey((JObject)slid.Value, nonPk);
                    pkError = (pkError || (JToken.DeepEquals(value, arrayData) && arrayData.Count > 0));
                    if (pkError)
                    {
                        throw new Exception("JSONDB Error: Can't insert value. Duplicate values \"" + string.Join(", ", Util.Values(value)) + "\" for primary keys \"" + string.Join(", ", (JArray)data["properties"]["primary_keys"]) + "\".");
                    }
                }
                ++index;
            }

            var ukError = false;
            for (int i = 0, l = ((JArray)data["properties"]["unique_keys"]).Count; i < l; i++)
            {
                index = 0;
                var uk = data["properties"]["unique_keys"][i].ToString();
                foreach (var lid in insert)
                {
                    var arrayData = new JObject { [uk] = lid.Value[uk] };
                    foreach (var slid in Util.Slice(insert, index + 1))
                    {
                        var value = new JObject { [uk] = slid.Value[uk] };
                        ukError = (ukError || (value[uk] != null && JToken.DeepEquals(value, arrayData)));
                        if (ukError)
                        {
                            throw new Exception("JSONDB Error: Can't insert value. Duplicate value \"" + value[uk] + "\" for unique key \"" + uk + "\".");
                        }
                    }
                    ++index;
                }
            }

            foreach (var lid in insert)
            {
                insert[lid.Key] = Util.KeySort(
                    (JObject)lid.Value,
                    (after, now) => Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString()),
                    (after, now) => Array.IndexOf(((JArray)data["prototype"]).ToArray(), now.ToString()) > Array.IndexOf(((JArray)data["prototype"]).ToArray(), after.ToString())
                );
            }

            var copy = insert;
            insert = Util.KeySort(
                insert,
                (after, now) => (int)copy[now.ToString()]["#rowid"] > (int)copy[after.ToString()]["#rowid"],
                (after, now) => (int)copy[now.ToString()]["#rowid"] < (int)copy[after.ToString()]["#rowid"]
            );

            var lastAi = 0;
            foreach (var item in (JObject)data["properties"])
            {
                var prop = item.Value;

                if (prop.Type != JTokenType.Object || prop["auto_increment"] == null || !(bool)prop["auto_increment"])
                    continue;

                foreach (var lid in insert)
                {
                    lastAi = Math.Max((int)lid.Value[item.Key], lastAi);
                }
                break;
            }

            data["data"] = insert;
            data["properties"]["last_valid_row_id"] = _getLastValidRowID(insert, false);
            data["properties"]["last_insert_id"] = lastAi;
            data["properties"]["last_link_id"] = lkId;

            var path = Util.MakePath(_db.GetServer(), _db.GetDatabase(), _table + ".jdbt");

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

        /// <summary>
        /// Return the set of <paramref name="data"/> which pass tests in <paramref name="filters"/>.
        /// </summary>
        /// <param name="data">The set of data to filter</param>
        /// <param name="filters">A set of tests</param>
        /// <returns>The filtered data</returns>
        private JArray _filter(JObject data, JArray filters)
        {
            var temp = new JObject();

            for (int i = 0, l = filters.Count; i < l; i++)
            {
                var filter = (JObject)filters[i];
                if (filter["value"].ToString().ToLower() == "last_insert_id")
                {
                    filter["value"] = data["properties"]["last_insert_id"];
                }

                foreach (var lid in data)
                {
                    var line = (JObject)lid.Value;
                    var value = line[filter["field"].ToString()];
                    if (Regex.IsMatch(filter["field"].ToString(), "\\w+\\(.*\\)"))
                    {
                        var parts = Regex.Replace(filter["field"].ToString(), "(\\w+)\\((.*)\\)", "$1.$2").Split('.');
                        var name = parts[0].ToLower();
                        var param = parts[1];
                        value = _parseWhereFunction(name, line[param].ToString());
                        filter["field"] = param;
                    }
                    if (line[filter["field"].ToString()] == null)
                    {
                        throw new Exception("JSONDB Error: The field \"" + filter["field"] + "\" doesn't exists in the table \"" + _table + "\".");
                    }
                    float left, right;
                    switch (filter["operator"].ToString())
                    {
                        case "<":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) ? left < right : string.Compare(value.ToString(), filter["value"].ToString()) == -1)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "<=":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) ? left <= right : string.Compare(value.ToString(), filter["value"].ToString()) >= 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "=":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) ? left == right : string.Compare(value.ToString(), filter["value"].ToString()) == 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case ">=":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) ? left >= right : string.Compare(value.ToString(), filter["value"].ToString()) <= 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case ">":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) ? left > right : string.Compare(value.ToString(), filter["value"].ToString()) == 1)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "!=":
                        case "<>":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) ? left != right : string.Compare(value.ToString(), filter["value"].ToString()) != 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "%=":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) && left % right == 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        case "%!":
                            if (float.TryParse(value.ToString(), out left) && float.TryParse(filter["value"].ToString(), out right) && left % right != 0)
                            {
                                temp[line["#rowid"].ToString()] = line;
                            }
                            break;
                        default:
                            throw new Exception("JSONDB Error: The operator \"" + filter["operator"] + "\" is not supported. Try to use one of these operators: \"<\", \"<=\", \"=\", \">=\", \">\", \"<>\", \"!=\", \"%=\" or \"%!\".");
                    }
                }
                data = temp;
                temp = new JObject();
            }

            return Util.Values((JObject)data);
        }
    }
}
