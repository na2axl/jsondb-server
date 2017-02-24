using System;
using System.Collections;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace JSONDB
{
    /// <summary>
    /// Class QueryParser
    /// </summary>
    public class QueryParser
    {
        /// <summary>
        /// Reserved query's characters to trim
        /// </summary>
        private const string TrimChar = ";'\"`";

        /// <summary>
        /// Reserved query's characters to escape
        /// </summary>
        private const string EscapeChar = ".,;'()";

        /// <summary>
        /// A list of supported queries
        /// </summary>
        private static readonly string[] SupportedQueries = {"select", "insert", "delete", "replace", "truncate", "update", "count", "min", "max", "sum", "avg"};

        /// <summary>
        /// Registerd query operators
        /// </summary>
        private static readonly string[] Operators = {"%!", "%=", "!=", "<>", "<=", ">=", "=", "<", ">"};

        /// <summary>
        /// Quotes a value and escape reserved characters.
        /// </summary>
        /// <param name="value">The value to quote</param>
        /// <returns>The parsed value</returns>
        public static string Quote(string value)
        {
            value = Regex.Replace(
                Regex.Replace(Regex.Replace(value, "(" + Environment.NewLine + ")", "\\$1"), "([" + Regex.Escape(EscapeChar) + "])", "\\$1", RegexOptions.IgnoreCase),
                "\\\\'|\\\\,|\\\\\\.|\\\\\\(|\\\\\\)|\\\\;|\\\\\r\\n|\\\\\r|\\\\\n",
                (match) =>
                {
                    switch (match.Value)
                    {
                        case "\\\r\n":
                        case "\\\n":
                        case "\\\r":
                            return "{{brk}}";
                        case "\\'":
                            return "{{quot}}";
                        case "\\,":
                            return "{{comm}}";
                        case "\\.":
                            return "{{dot}}";
                        case "\\(":
                            return "{{pto}}";
                        case "\\)":
                            return "{{ptc}}";
                        case "\\;":
                            return "{{semi}}";
                        default:
                            return match.Value;
                    }
                },
                RegexOptions.IgnoreCase
            );

            return "'" + value + "'";
        }

        /// <summary>
        /// Parse a JQL query.
        /// </summary>
        /// <param name="query">The query to parse</param>
        /// <returns>The parsed query</returns>
        public static JObject Parse(string query)
        {
            // Start the Benchmark
            Benchmark.Mark("jsondb_query_parse_start");

            // Quote all escaped reserved characters
            query = Regex.Replace(
                query,
                "\\\\'|\\\\,|\\\\\\.|\\\\\\(|\\\\\\)|\\\\;|\\\\\r\\n|\\\\\r|\\\\\n",
                (match) =>
                {
                    switch (match.Value)
                    {
                        case "\\\r\n":
                        case "\\\n":
                        case "\\\r":
                            return "{{brk}}";
                        case "\\'":
                            return "{{quot}}";
                        case "\\,":
                            return "{{comm}}";
                        case "\\.":
                            return "{{dot}}";
                        case "\\(":
                            return "{{pto}}";
                        case "\\)":
                            return "{{ptc}}";
                        case "\\;":
                            return "{{semi}}";
                        default:
                            return match.Value;
                    }
                },
                RegexOptions.IgnoreCase
            );

            // Initialize variables
            var parsedQuery = new JObject();
            var queryParts = query.Split('.');

            // If the query is not at the minimal form (table_name.action())
            if (queryParts.Length < 2)
            {
                throw new Exception("JSONDB Query Parse Error: This is not a JQL query.");
            }

            // Get the table's name
            if (string.IsNullOrEmpty(queryParts[0]))
            {
                throw new Exception("JSONDB Query Parse Error: No table detected in the query.");
            }
            parsedQuery["table"] = queryParts[0];

            // Check query's parts validity
            for (int i = 1, l = queryParts.Length; i < l; ++i)
            {
                var part = queryParts[i];
                if (string.IsNullOrEmpty(part))
                {
                    throw new Exception("JSONDB Query Parse Error: Unexpected \".\" after extension \"" + part + "\".");
                }
                if (!Regex.IsMatch(part, "^\\w+\\(.*\\)$"))
                {
                    throw new Exception("JSONDB Query Parse Error: There is an error at the extension \"" + part + "\".");
                }
            }

            // Get the query's main action
            parsedQuery["action"] = Regex.Replace(queryParts[1], "\\(.*\\)", "");
            if (Array.IndexOf(SupportedQueries, parsedQuery["action"].ToString().ToLower()) == -1)
            {
                throw new Exception("JSONDB Query Parse Error: The query \"" + parsedQuery["action"] + "\" isn't supported by JSONDB.");
            }

            // Get the action's parameters
            parsedQuery["parameters"] = Regex.Replace(queryParts[1], "^\\w+\\((.*)\\)$", "$1").Trim();
            parsedQuery["parameters"] = Regex.Replace(parsedQuery["parameters"].ToString(), "\\(([^)]*)\\)", (match) => Regex.Replace(match.Value, ",", ";"));
            parsedQuery["parameters"] = new JArray(parsedQuery["parameters"].ToString().Split(','));
            parsedQuery["parameters"] = ((JArray)parsedQuery["parameters"]).Count > 0 ? parsedQuery["parameters"] : new JArray();
            Array.ForEach(((JArray)parsedQuery["parameters"]).ToArray(), (field) =>
            {
                parsedQuery["parameters"][Array.IndexOf(((JArray)parsedQuery["parameters"]).ToArray(), field)] = field.ToString().Trim();
            });

            // Parse values for some actions
            if (Array.IndexOf(new string[] {"insert", "replace"}, parsedQuery["action"].ToString().ToLower()) > -1)
            {
                Array.ForEach(((JArray)parsedQuery["parameters"]).ToArray(), (field) =>
                {
                    parsedQuery["parameters"][Array.IndexOf(((JArray)parsedQuery["parameters"]).ToArray(), field)] = _parseValue(field);
                });
            }

            // Get query's extension
            var extensions = new JObject();
            for (int i = 2, l = queryParts.Length; i < l; i++)
            {
                var extension = queryParts[i];
                var name = Regex.Replace(extension, "\\(.*\\)", "");
                var parameters = Regex.Replace(Regex.Replace(extension, "^" + name + "\\((.*)\\)$", "$1"), "\\(([^)]*)\\)", (match) => Regex.Replace(match.Value, ",", ";"));

                switch (name.ToLower())
                {
                    case "order":
                        extensions["order"] = _parseOrderExtension(parameters);
                        break;
                    case "where":
                        if (extensions["where"] == null)
                        {
                            extensions["where"] = new JArray();
                        }
                        ((JArray)extensions["where"]).Add(_parseWhereExtension(parameters));
                        break;
                    case "and":
                        if (extensions["and"] == null)
                        {
                            extensions["and"] = new JArray();
                        }
                        ((JArray)extensions["and"]).Add(_parseAndExtension(parameters));
                        break;
                    case "limit":
                        extensions["limit"] = _parseLimitExtension(parameters);
                        break;
                    case "in":
                        extensions["in"] = _parseInExtension(parameters);
                        break;
                    case "with":
                        extensions["with"] = _parseWithExtension(parameters);
                        break;
                    case "as":
                        extensions["as"] = _parseAsExtension(parameters);
                        break;
                    case "group":
                        extensions["group"] = _parseGroupExtension(parameters);
                        break;
                    case "on":
                        if (extensions["on"] == null)
                        {
                            extensions["on"] = new JArray();
                        }
                        ((JArray)extensions["on"]).Add(_parseOnExtension(parameters));
                        break;
                    default:
                        throw new Exception("Query Parse Error: The extension " + name + "() is not a valid JQL extension.");
                }
            }
            parsedQuery["extensions"] = extensions;

            // Stop the Benchmark
            Benchmark.Mark("jsondb_query_parse_end");

            parsedQuery["benchmark"] = new JObject
            {
                ["elapsed_time"] = Benchmark.ElapsedTime("jsondb_query_parse_start", "jsondb_query_parse_end"),
                ["memory_usage"] = Benchmark.MemoryUsage("jsondb_query_parse_start", "jsondb_query_parse_end")
            };

            return parsedQuery;
        }

        /// <summary>
        /// Parse multiline JQL queries.
        /// </summary>
        /// <param name="queriesBlock">The text which contains JQL queries</param>
        /// <returns>An array of parsed queries.</returns>
        public static JObject[] MultilineParse(string queriesBlock)
        {
            // Start the Benchmark
            Benchmark.Mark("jsondb_query_parse_start");

            // Quote all escaped reserved characters
            queriesBlock = Regex.Replace(
                queriesBlock,
                "\\\\'|\\\\,|\\\\\\.|\\\\\\(|\\\\\\)|\\\\;|\\\\\r\\n|\\\\\r|\\\\\n",
                (match) =>
                {
                    switch (match.Value)
                    {
                        case "\\\r\n":
                        case "\\\n":
                        case "\\\r":
                            return "{{brk}}";
                        case "\\'":
                            return "{{quot}}";
                        case "\\,":
                            return "{{comm}}";
                        case "\\.":
                            return "{{dot}}";
                        case "\\(":
                            return "{{pto}}";
                        case "\\)":
                            return "{{ptc}}";
                        case "\\;":
                            return "{{semi}}";
                        default:
                            return match.Value;
                    }
                },
                RegexOptions.IgnoreCase
            ).Trim('\r', '\n', ' ');

            // Remove comments
            queriesBlock = Regex.Replace(queriesBlock, "//.*[\\r\\n]?", "");
            
            // Split
            var queriesArray = Regex.Split(queriesBlock, ";(?:[\\r\\n]*)");

            // Initialize variables
            var queriesLines = new JArray();
            var i = 0;

            foreach (var query in queriesArray)
            {
                if (query.Length <= 0) continue;
                queriesLines.Add(query);

                // Split
                var subQueryParts = Regex.Split(queriesLines[i].ToString(), "[\\r\\n]+");

                for (int j = 0, l = subQueryParts.Length; j < l; j++)
                {
                    // Remove child indentations
                    subQueryParts[j] = subQueryParts[j].Trim('\t', ' ');
                }

                // Join query parts
                queriesLines[i] = string.Join(string.Empty, subQueryParts);

                i++;
            }

            // Parse all queries
            var parsedQueries = new JObject[queriesLines.Count];
            i = 0;

            for (int q = 0, l = queriesLines.Count; q < l; q++)
            {
                if (queriesLines[q].ToString().Trim().Length <= 0) continue;
                try
                {
                    parsedQueries[i] = Parse(queriesLines[q].ToString());
                    i++;
                }
                catch (Exception e)
                {
                    throw new MultilineQueryParseException(e.Message, q + 1);
                }
            }

            return parsedQueries;
        }

        /// <summary>
        /// Parse the order() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseOrderExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));
            Array.ForEach(parsedClause.ToArray(), (field) =>
            {
                var index = Array.IndexOf(parsedClause.ToArray(), field);
                field = field.ToString().Trim(TrimChar.ToCharArray()).Trim();
                parsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"order()\" extension.");
            }
            if (parsedClause.Count > 2)
            {
                throw new Exception("JSONDB Query Parse Error: Too much parameters given to the \"order()\" extension, only two required.");
            }
            if (parsedClause.Count == 2 && Array.IndexOf(new JArray("asc", "desc").ToArray(), parsedClause[1].ToString().ToLower()) == -1)
            {
                throw new Exception("JSONDB Query Parse Error: The second parameter of the \"order()\" extension can only have values: \"asc\" or \"desc\".");
            }
            if (parsedClause.Count == 1)
            {
                parsedClause.Add("asc");
            }

            return parsedClause;
        }

        /// <summary>
        /// Parse the where() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseWhereExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"where()\" extension.");
            }

            for (int i = 0, l = parsedClause.Count; i < l; i++)
            {
                parsedClause[i] = _parseWhereExtensionCondition(parsedClause[i].ToString());
            }

            return parsedClause;
        }

        /// <summary>
        /// Parse the conditions of the where() extension.
        /// </summary>
        /// <param name="condition">Condition</param>
        /// <returns>The parsed condition</returns>
        protected static JObject _parseWhereExtensionCondition(string condition)
        {
            var filters = new JObject();
            var opFound = false;

            for (int i = 0, l = Operators.Length; i < l; i++)
            {
                var op = Operators[i];
                if (condition.IndexOf(op, StringComparison.Ordinal) <= -1 &&
                    Array.IndexOf(condition.ToCharArray(), op) <= -1 && Array.IndexOf(condition.Split(' '), op) <= -1)
                    continue;
                var index = condition.IndexOf(op, StringComparison.Ordinal);
                var identifier = condition.Substring(0, index).Trim();
                var value = condition.Substring(index + op.Length).Trim();

                var rowVal =  Regex.Split(condition, "\\s" + op + "\\s");
                if (!Regex.IsMatch(identifier, "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + identifier + "\".");
                }
                filters["operator"] = op;
                filters["field"] = Regex.Replace(identifier, "['\"`]", "").Trim();
                filters["value"] = _parseValue(value);
                opFound = true;
                break;
            }

            if (!opFound)
            {
                throw new Exception("JSONDB Query Parse Error: Unable to parse the condition \"" + condition + "\"");
            }

            return filters;
        }

        /// <summary>
        /// Parse the and() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseAndExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"and()\" extension.");
            }

            Array.ForEach(parsedClause.ToArray(), (field) =>
            {
                parsedClause[Array.IndexOf(parsedClause.ToArray(), field)] = _parseValue(field);
            });

            return parsedClause;
        }

        /// <summary>
        /// Parse the limit() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseLimitExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"limit()\" extension.");
            }
            if (parsedClause.Count > 2)
            {
                throw new Exception("JSONDB Query Parse Error: Too much parameters given to the \"limit()\" extension, only two required.");
            }

            if (parsedClause.Count == 1)
            {
                parsedClause.Add(parsedClause[0]);
                parsedClause[0] = 0;
            }

            Array.ForEach(parsedClause.ToArray(), (field) =>
            {
                parsedClause[Array.IndexOf(parsedClause.ToArray(), field)] = _parseValue(field);
            });

            return parsedClause;
        }

        /// <summary>
        /// Parse the in() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseInExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));
            Array.ForEach(parsedClause.ToArray(), (field) =>
            {
                var index = Array.IndexOf(parsedClause.ToArray(), field);
                field = field.ToString().Trim(TrimChar.ToCharArray()).Trim();
                parsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"in()\" extension.");
            }

            return parsedClause;
        }

        /// <summary>
        /// Parse the with() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseWithExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"with()\" extension.");
            }

            Array.ForEach(parsedClause.ToArray(), (field) =>
            {
                parsedClause[Array.IndexOf(parsedClause.ToArray(), field)] = _parseValue(field);
            });

            return parsedClause;
        }

        /// <summary>
        /// Parse the as() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseAsExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));
            Array.ForEach(parsedClause.ToArray(), (field) =>
            {
                var index = Array.IndexOf(parsedClause.ToArray(), field);
                field = field.ToString().Trim(TrimChar.ToCharArray()).Trim();
                parsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"as()\" extension.");
            }

            return parsedClause;
        }

        /// <summary>
        /// Parse the group() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JArray _parseGroupExtension(string clause)
        {
            var parsedClause = new JArray(clause.Split(','));
            Array.ForEach(parsedClause.ToArray(), (field) =>
            {
                var index = Array.IndexOf(parsedClause.ToArray(), field);
                field = field.ToString().Trim(TrimChar.ToCharArray()).Trim();
                parsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (parsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"group()\" extension.");
            }
            if (parsedClause.Count > 1)
            {
                throw new Exception("JSONDB Query Parse Error: Too much parameters given to the \"group()\" extension, only one required.");
            }

            return parsedClause;
        }

        /// <summary>
        /// Parse the on() extension.
        /// </summary>
        /// <param name="clause">Extension parameter</param>
        /// <returns>The parsed extension</returns>
        protected static JObject _parseOnExtension(string clause)
        {
            var parsedClause = new JObject();
            var extensionParts = clause.Split(',');

            if (extensionParts.Length < 2)
            {
                throw new Exception("JSONDB Query Parse Error: At least two parameters expected for the \"on()\" extension.");
            }

            var actionParts = Regex.Replace(extensionParts[1], "(\\w+)\\((.*)\\)", "$1.$2").Split('.');

            if (!Regex.IsMatch(extensionParts[0], "^\\w+$"))
            {
                throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + extensionParts[0] + "\".");
            }

            parsedClause["column"] = extensionParts[0];
            parsedClause["action"] = new JObject
            {
                ["name"] = actionParts[0].Trim(),
                ["parameters"] = new JArray(actionParts[1].Split(';'))
            };
            Array.ForEach(parsedClause["action"]["parameters"].ToArray(), (field) =>
            {
                parsedClause["action"]["parameters"][Array.IndexOf(parsedClause["action"]["parameters"].ToArray(), field)] = field.ToString().Trim();
            });

            return parsedClause;
        }

        /// <summary>
        /// Parse and execute a function.
        /// </summary>
        /// <param name="func">The function name</param>
        /// <returns>The function's result</returns>
        protected static JToken _parseFunction(string func)
        {
            var parts = Regex.Replace(func, "(\\w+)\\((.*)\\)", "$1.$2").Split('.');
            var name = parts[0];
            JArray parameters;
            if (parts[1] == string.Empty)
            {
                parameters = new JArray();
            }
            else
            {
                parameters = new JArray(parts[1].Split(';'));
                Array.ForEach(parameters.ToArray(), (field) =>
                {
                    parameters[Array.IndexOf(parameters.ToArray(), field)] = _parseValue(field);
                });
            }

            switch (name)
            {
                case "sha1":
                    if (parameters.Count == 0)
                    {
                        throw new Exception("JSONDB Query Parse Error: There is no parameters for the function sha1(). Can't execute the query.");
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function sha1(), only one is required.");
                    }
                    return Util.Sha1(parameters[0].ToString());
                case "md5":
                    if (parameters.Count == 0)
                    {
                        throw new Exception("JSONDB Query Parse Error: There is no parameters for the function md5(). Can't execute the query.");
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function md5(), only one is required.");
                    }
                    return Util.Md5(parameters[0].ToString());
                case "time":
                    if (parameters.Count == 0)
                    {
                        return DateTime.Now.Millisecond;
                    }
                    throw new Exception("JSONDB Query Parse Error: Too much parameters for the function time(), no one is required.");
                case "now":
                    var date = DateTime.Now;
                    var days = new JArray("Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday");
                    var months = new JArray("January", "Febuary", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December");
                    if (parameters.Count == 0)
                    {
                        return Util.Zeropad(date.Year) + "-" + Util.Zeropad(date.Month) + "-" + Util.Zeropad(date.Day) + " " + Util.Zeropad(date.Hour) + ":" + Util.Zeropad(date.Minute) + ":" + Util.Zeropad(date.Second);
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function now(), only one is required.");
                    }
                    return parameters[0].ToString()
                        .Replace("%a", days[(int)date.DayOfWeek].ToString().Substring(0, 3))
                        .Replace("%A", days[(int)date.DayOfWeek].ToString())
                        .Replace("%d", Util.Zeropad(date.Day))
                        .Replace("%m", Util.Zeropad(date.Month))
                        .Replace("%e", date.Month.ToString())
                        .Replace("%w", ((int)date.DayOfWeek).ToString())
                        .Replace("%W", Util.Zeropad((int)date.DayOfWeek))
                        .Replace("%b", months[date.Month].ToString().Substring(0, 3))
                        .Replace("%B", months[date.Month].ToString())
                        .Replace("%y", (date.Year % 1000).ToString())
                        .Replace("%Y", date.Year.ToString())
                        .Replace("%H", Util.Zeropad(date.Hour))
                        .Replace("%k", date.Hour.ToString())
                        .Replace("%M", Util.Zeropad(date.Minute))
                        .Replace("%S", Util.Zeropad(date.Second));
                case "lowercase":
                    if (parameters.Count == 0)
                    {
                        throw new Exception("JSONDB Query Parse Error: There is no parameters for the function lowercase(). Can't execute the query.");
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function lowercase(), only one is required.");
                    }
                    return parameters[0].ToString().ToLower();

                case "uppercase":
                    if (parameters.Count == 0)
                    {
                        throw new Exception("JSONDB Query Parse Error: There is no parameters for the function uppercase(). Can't execute the query.");
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function uppercase(), only one is required.");
                    }
                    return parameters[0].ToString().ToUpper();

                case "ucfirst":
                    if (parameters.Count == 0)
                    {
                        throw new Exception("JSONDB Query Parse Error: There is no parameters for the function ucfirst(). Can't execute the query.");
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function ucfirst(), only one is required.");
                    }
                    var first = parameters[0].ToString()[0].ToString().ToUpper();
                    return first + parameters[0].ToString().Substring(1).ToLower();

                case "strlen":
                    if (parameters.Count == 0)
                    {
                        throw new Exception("JSONDB Query Parse Error: There is no parameters for the function strlen(). Can't execute the query.");
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function strlen(), only one is required.");
                    }
                    return parameters[0].ToString().Length;

                default:
                    throw new Exception("JSONDB Query Parse Error: Sorry but the function " + name + "() is not implemented in JQL.");
            }
        }

        /// <summary>
        /// Parse a value.
        /// </summary>
        /// <param name="value">The value to parse</param>
        /// <returns>The parsed value</returns>
        protected static JToken _parseValue(JToken value)
        {
            var trimValue = value.ToString().Trim();

            if (trimValue == string.Empty)
            {
                return string.Empty;
            }
            if (trimValue.IndexOf(":JSONDB::TO_BOOL:", StringComparison.Ordinal) > -1)
            {
                return int.Parse(value.ToString().Replace(":JSONDB::TO_BOOL:", "")) == 1;
            }
            if (trimValue.ToLower() == "false")
            {
                return false;
            }
            if (trimValue.ToLower() == "true")
            {
                return true;
            }
            if (trimValue.IndexOf(":JSONDB::TO_NULL:", StringComparison.Ordinal) > -1 || trimValue.ToLower() == "null")
            {
                return null;
            }
            if (trimValue.IndexOf(":JSONDB::TO_ARRAY:", StringComparison.Ordinal) > -1)
            {
                return JObject.Parse(_parseValue(trimValue.Replace(":JSONDB::TO_ARRAY:", "")).ToString());
            }
            if (trimValue[0] == '\'' && trimValue[trimValue.Length - 1] == '\'')
            {
                return Regex.Replace(
                    Regex.Replace(trimValue, "[" + TrimChar + "]", ""),
                    "\\{\\{quot\\}\\}|\\{\\{comm\\}\\}|\\{\\{dot\\}\\}|\\{\\{pto\\}\\}|\\{\\{ptc\\}\\}|\\{\\{semi\\}\\}",
                    (match) =>
                    {
                        switch (match.Value)
                        {
                            case "{{quot}}":
                                return "'";
                            case "{{comm}}":
                                return ",";
                            case "{{dot}}":
                                return ".";
                            case "{{pto}}":
                                return "(";
                            case "{{ptc}}":
                                return ")";
                            case "{{semi}}":
                                return ";";
                            default:
                                return match.Value;
                        }
                    }
                );
            }
            else if (Regex.IsMatch(trimValue, "\\w+\\(.*\\)"))
            {
                return _parseFunction(trimValue);
            }
            else
            {
                int res;
                if (int.TryParse(Regex.Replace(trimValue, "[" + TrimChar + "]", "").Trim(), out res))
                {
                    return res;
                }
            }

            throw new Exception("JSONDB Query Parse Error: Unable to parse the value \"" + trimValue + "\".");
        }
    }

    /// <summary>
    /// Exception handler for multiline queryies parsing.
    /// </summary>
    public class MultilineQueryParseException : Exception
    {
        private readonly Exception _base;

        public int Line { get; }

        public override string Message => _base.Message;

        public override IDictionary Data => _base.Data;

        public override string HelpLink
        {
            get
            {
                return _base.HelpLink;
            }

            set
            {
                _base.HelpLink = value;
            }
        }

        public override string Source
        {
            get
            {
                return _base.Source;
            }

            set
            {
                _base.Source = value;
            }
        }

        public override string StackTrace => _base.StackTrace;

        public MultilineQueryParseException(string message, int line)
        {
            _base = new Exception(message);
            Line = line;
        }

        public override Exception GetBaseException()
        {
            return _base.GetBaseException();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            _base.GetObjectData(info, context);
        }

        public override string ToString()
        {
            return Message + " At the query #" + Line + ".";
        }
    }
}
