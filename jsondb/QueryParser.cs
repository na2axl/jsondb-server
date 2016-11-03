using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections;

namespace JSONDB.Library
{
    public class QueryParser
    {
        /// <summary>
        /// Reserved query's characters to trim
        /// </summary>
        private static string TRIM_CHAR = ";'\"`";

        /// <summary>
        /// Reserved query's characters to escape
        /// </summary>
        private static string ESCAPE_CHAR = ".,;'()";

        /// <summary>
        /// A list of supported queries
        /// </summary>
        private static JArray SupportedQueries = new JArray("select", "insert", "delete", "replace", "truncate", "update", "count");

        /// <summary>
        /// Registerd query operators
        /// </summary>
        private static JArray Operators = new JArray("%!", "%=", "!=", "<>", "<=", ">=", "=", "<", ">");

        /// <summary>
        /// Quotes a value and escape reserved characters.
        /// </summary>
        /// <param name="value">The value to quote</param>
        /// <returns>The parsed value</returns>
        public static string Quote(string value)
        {
            value = new Regex("\\\\'|\\\\,|\\\\\\.|\\\\\\(|\\\\\\)|\\\\;|\\\\\r\\n|\\\\\r|\\\\\n", RegexOptions.IgnoreCase).Replace(
                new Regex("([" + Regex.Escape(ESCAPE_CHAR) + "])", RegexOptions.IgnoreCase).Replace(
                    Regex.Replace(value, "(" + Environment.NewLine + ")", "\\$1"), "\\$1"),
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
                    }
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
            query = new Regex("\\\\'|\\\\,|\\\\\\.|\\\\\\(|\\\\\\)|\\\\;|\\\\\r\\n|\\\\\r|\\\\\n", RegexOptions.IgnoreCase).Replace(query,
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
                }
            );

            // Initialize variables
            JObject ParsedQuery = new JObject();
            var queryParts = query.Split('.');

            // If the query is not at the minimal form (table_name.action())
            if (queryParts.Length < 2)
            {
                throw new Exception("JSONDB Query Parse Error: This is not a JQL query.");
            }

            // Getting the table's name
            ParsedQuery["table"] = queryParts[0];
            if ((string)ParsedQuery["table"] == String.Empty)
            {
                throw new Exception("JSONDB Query Parse Error: No table detected in the query.");
            }

            // Checking query's parts validity
            for (int i = 1, l = queryParts.Length; i < l; ++i)
            {
                var part = queryParts[i];
                if (null == part || part == String.Empty)
                {
                    throw new Exception("JSONDB Query Parse Error: Unexpected \".\" after extension \"" + part + "\".");
                }
                if (!new Regex("^\\w+\\(.*\\)$").IsMatch(part))
                {
                    throw new Exception("JSONDB Query Parse Error: There is an error at the extension \"" + part + "\".");
                }
            }

            // Getting the query's main action
            ParsedQuery["action"] = new Regex("\\(.*\\)").Replace(queryParts[1], "");
            if (Array.IndexOf(SupportedQueries.ToArray(), ParsedQuery["action"].ToString().ToLower()) == -1)
            {
                throw new Exception("JSONDB Query Parse Error: The query \"" + ParsedQuery["action"] + "\" isn't supported by JSONDB.");
            }

            // Getting the action's parameters
            ParsedQuery["parameters"] = new Regex("^\\w+\\((.*)\\)$").Replace(queryParts[1], "$1").Trim();
            ParsedQuery["parameters"] = new Regex("\\(([^)]*)\\)").Replace(ParsedQuery["parameters"].ToString(), (match) =>
            {
                return new Regex(",").Replace(match.Value, ";");
            });
            ParsedQuery["parameters"] = new JArray(ParsedQuery["parameters"].ToString().Split(','));
            ParsedQuery["parameters"] = ((JArray)ParsedQuery["parameters"]).Count > 0 ? ParsedQuery["parameters"] : new JArray();
            Array.ForEach(((JArray)ParsedQuery["parameters"]).ToArray(), (field) =>
            {
                ParsedQuery["parameters"][Array.IndexOf(((JArray)ParsedQuery["parameters"]).ToArray(), field)] = field.ToString().Trim();
            });

            // Parsing values for some actions
            if (Array.IndexOf(new JArray("insert", "replace").ToArray(), ParsedQuery["action"].ToString().ToLower()) > -1)
            {
                Array.ForEach(((JArray)ParsedQuery["parameters"]).ToArray(), (field) =>
                {
                    ParsedQuery["parameters"][Array.IndexOf(((JArray)ParsedQuery["parameters"]).ToArray(), field)] = _parseValue(field);
                });
            }

            // Getting query's extension
            JObject extensions = new JObject();
            for (int i = 2, l = queryParts.Length; i < l; i++)
            {
                var extension = queryParts[i];
                var name = new Regex("\\(.*\\)").Replace(extension, "");
                var parameters = new Regex("\\(([^)]*)\\)").Replace(new Regex("^" + name + "\\((.*)\\)$").Replace(extension, "$1"), (match) =>
                {
                    return new Regex(",").Replace(match.Value, ";");
                });

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
            ParsedQuery["extensions"] = extensions;

            // Stop the Benchmark
            Benchmark.Mark("jsondb_query_parse_end");

            ParsedQuery["benchmark"] = new JObject();
            ParsedQuery["benchmark"]["elapsed_time"] = Benchmark.ElapsedTime("jsondb_query_parse_start", "jsondb_query_parse_end");
            ParsedQuery["benchmark"]["memory_usage"] = Benchmark.MemoryUsage("jsondb_query_parse_start", "jsondb_query_parse_end");

            return ParsedQuery;
        }

        /// <summary>
        /// Paarse multiline JQL queries.
        /// </summary>
        /// <param name="queriesBlock">The text which contains JQL queries</param>
        /// <returns>An array of parsed queries.</returns>
        public static JObject[] MultilineParse(string queriesBlock)
        {
            // Start the Benchmark
            Benchmark.Mark("jsondb_query_parse_start");

            // Quote all escaped reserved characters
            queriesBlock = new Regex("\\\\'|\\\\,|\\\\\\.|\\\\\\(|\\\\\\)|\\\\;|\\\\\r\\n|\\\\\r|\\\\\n", RegexOptions.IgnoreCase).Replace(queriesBlock,
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
                }
            ).Trim('\r', '\n', ' ');

            // Remove comments
            queriesBlock = new Regex("//.*[\\r\\n]?").Replace(queriesBlock, "");
            
            // Split
            string[] queriesArray = new Regex(";(?:[\\r\\n]*)").Split(queriesBlock);

            // Initialize variables
            JArray queriesLines = new JArray();
            int i = 0;

            foreach (string query in queriesArray)
            {
                if (query.Length > 0)
                {
                    queriesLines.Add(query);

                    // Split
                    string[] subQueryParts = new Regex("[\\r\\n]+").Split(queriesLines[i].ToString());

                    for (int j = 0, l = subQueryParts.Length; j < l; j++)
                    {
                        // Remove child indentations
                        subQueryParts[j] = subQueryParts[j].Trim('\t', ' ');
                    }

                    // Join query parts
                    queriesLines[i] = String.Join(String.Empty, subQueryParts);

                    i++;
                }
            }

            // Parse all queries
            JObject[] ParsedQueries = new JObject[queriesLines.Count];
            i = 0;

            for (int q = 0, l = queriesLines.Count; q < l; q++)
            {
                if (queriesLines[q].ToString().Trim().Length > 0)
                {
                    try
                    {
                        ParsedQueries[i] = Parse(queriesLines[q].ToString());
                        i++;
                    }
                    catch (Exception e)
                    {
                        throw new MultilineQueryParseException(e.Message, q + 1);
                    }
                }
            }

            return ParsedQueries;
        }

        protected static JArray _parseOrderExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));
            Array.ForEach(ParsedClause.ToArray(), (field) =>
            {
                int index = Array.IndexOf(ParsedClause.ToArray(), field);
                field = field.ToString().Trim(TRIM_CHAR.ToCharArray()).Trim();
                ParsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"order()\" extension.");
            }
            if (ParsedClause.Count > 2)
            {
                throw new Exception("JSONDB Query Parse Error: Too much parameters given to the \"order()\" extension, only two required.");
            }
            if (ParsedClause.Count == 2 && Array.IndexOf(new JArray("asc", "desc").ToArray(), ParsedClause[1].ToString().ToLower()) == -1)
            {
                throw new Exception("JSONDB Query Parse Error: The second parameter of the \"order()\" extension can only have values: \"asc\" or \"desc\".");
            }
            if (ParsedClause.Count == 1)
            {
                ParsedClause.Add("asc");
            }

            return ParsedClause;
        }

        protected static JArray _parseWhereExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"where()\" extension.");
            }

            for (int i = 0, l = ParsedClause.Count; i < l; i++)
            {
                ParsedClause[i] = _parseWhereExtensionCondition(ParsedClause[i].ToString());
            }

            return ParsedClause;
        }

        protected static JObject _parseWhereExtensionCondition(string condition)
        {
            JObject filters = new JObject();
            bool opFound = false;

            for (int i = 0, l = Operators.Count; i < l; i++)
            {
                var op = Operators[i].ToString();
                if (condition.IndexOf(op) > -1 || Array.IndexOf(condition.ToCharArray(), op) > -1 || Array.IndexOf(condition.Split(' '), op) > -1)
                {
                    int index = condition.IndexOf(op);
                    string identifier = condition.Substring(0, index).Trim();
                    string value = condition.Substring(index + op.Length).Trim();

                    string[] row_val =  Regex.Split(condition, "\\s" + op + "\\s");
                    if (!Regex.IsMatch(identifier, "^\\w+$"))
                    {
                        throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + identifier + "\".");
                    }
                    filters["operator"] = op;
                    filters["field"] = new Regex("['\"`]").Replace(identifier, "").Trim();
                    filters["value"] = _parseValue(value);
                    opFound = true;
                    break;
                }
            }

            if (!opFound)
            {
                throw new Exception("JSONDB Query Parse Error: Unable to parse the condition \"" + condition + "\"");
            }

            return filters;
        }

        protected static JArray _parseAndExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"and()\" extension.");
            }

            Array.ForEach(ParsedClause.ToArray(), (field) =>
            {
                ParsedClause[Array.IndexOf(ParsedClause.ToArray(), field)] = _parseValue(field);
            });

            return ParsedClause;
        }

        protected static JArray _parseLimitExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"limit()\" extension.");
            }
            if (ParsedClause.Count > 2)
            {
                throw new Exception("JSONDB Query Parse Error: Too much parameters given to the \"limit()\" extension, only two required.");
            }

            if (ParsedClause.Count == 1)
            {
                ParsedClause.Add(ParsedClause[0]);
                ParsedClause[0] = 0;
            }

            Array.ForEach(ParsedClause.ToArray(), (field) =>
            {
                ParsedClause[Array.IndexOf(ParsedClause.ToArray(), field)] = _parseValue(field);
            });

            return ParsedClause;
        }

        protected static JArray _parseInExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));
            Array.ForEach(ParsedClause.ToArray(), (field) =>
            {
                int index = Array.IndexOf(ParsedClause.ToArray(), field);
                field = field.ToString().Trim(TRIM_CHAR.ToCharArray()).Trim();
                ParsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"in()\" extension.");
            }

            return ParsedClause;
        }

        protected static JArray _parseWithExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"with()\" extension.");
            }

            Array.ForEach(ParsedClause.ToArray(), (field) =>
            {
                ParsedClause[Array.IndexOf(ParsedClause.ToArray(), field)] = _parseValue(field);
            });

            return ParsedClause;
        }

        protected static JArray _parseAsExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));
            Array.ForEach(ParsedClause.ToArray(), (field) =>
            {
                int index = Array.IndexOf(ParsedClause.ToArray(), field);
                field = field.ToString().Trim(TRIM_CHAR.ToCharArray()).Trim();
                ParsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"as()\" extension.");
            }

            return ParsedClause;
        }

        protected static JArray _parseGroupExtension(string clause)
        {
            JArray ParsedClause = new JArray(clause.Split(','));
            Array.ForEach(ParsedClause.ToArray(), (field) =>
            {
                int index = Array.IndexOf(ParsedClause.ToArray(), field);
                field = field.ToString().Trim(TRIM_CHAR.ToCharArray()).Trim();
                ParsedClause[index] = field;
                if (!Regex.IsMatch(field.ToString(), "^\\w+$"))
                {
                    throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + field + "\".");
                }
            });

            if (ParsedClause.Count == 0)
            {
                throw new Exception("JSONDB Query Parse Error: At least one parameter expected for the \"group()\" extension.");
            }
            if (ParsedClause.Count > 1)
            {
                throw new Exception("JSONDB Query Parse Error: Too much parameters given to the \"group()\" extension, only one required.");
            }

            return ParsedClause;
        }

        protected static JObject _parseOnExtension(string clause)
        {
            JObject ParsedClause = new JObject();
            string[] extensionParts = clause.Split(',');

            if (extensionParts.Length < 2)
            {
                throw new Exception("JSONDB Query Parse Error: At least two parameters expected for the \"on()\" extension.");
            }

            string[] actionParts = new Regex("(\\w+)\\((.*)\\)").Replace(extensionParts[1], "$1.$2").Split('.');

            if (!Regex.IsMatch(extensionParts[0], "^\\w+$"))
            {
                throw new Exception("JSONDB Query Parse Error: Invalid identifier name \"" + extensionParts[0] + "\".");
            }

            ParsedClause["column"] = extensionParts[0];
            ParsedClause["action"] = new JObject();
            ParsedClause["action"]["name"] = actionParts[0].ToString().Trim();
            ParsedClause["action"]["parameters"] = new JArray(actionParts[1].Split(';'));

            return ParsedClause;
        }

        protected static JToken _parseFunction(string func)
        {
            var parts = new Regex("(\\w+)\\((.*)\\)").Replace(func, "$1.$2").Split('.');
            var name = parts[0];
            JArray parameters;
            if (parts[1] == String.Empty)
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
                    return Util.SHA1(parameters[0].ToString());
                case "md5":
                    if (parameters.Count == 0)
                    {
                        throw new Exception("JSONDB Query Parse Error: There is no parameters for the function md5(). Can't execute the query.");
                    }
                    if (parameters.Count > 1)
                    {
                        throw new Exception("JSONDB Query Parse Error: Too much parameters for the function md5(), only one is required.");
                    }
                    return Util.MD5(parameters[0].ToString());
                case "time":
                    if (parameters.Count == 0)
                    {
                        return DateTime.Now.Millisecond;
                    }
                    throw new Exception("JSONDB Query Parse Error: Too much parameters for the function time(), no one is required.");
                case "now":
                    var date = DateTime.Now;
                    JArray days = new JArray("Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday");
                    JArray months = new JArray("January", "Febuary", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December");
                    if (parameters.Count == 0)
                    {
                        return Util.Zeropad(date.Year) + "-" + Util.Zeropad(date.Month) + "-" + Util.Zeropad(date.Day) + " " + Util.Zeropad(date.Hour) + ":" + Util.Zeropad(date.Minute) + ":" + Util.Zeropad(date.Second);
                    }
                    else
                    {
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
                    }
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

        protected static JToken _parseValue(JToken value)
        {
            string trim_value = value.ToString().Trim();

            if (trim_value == String.Empty)
            {
                return String.Empty;
            }
            else if (trim_value.IndexOf(":JSONDB::TO_BOOL:") > -1)
            {
                return int.Parse(value.ToString().Replace(":JSONDB::TO_BOOL:", "")) == 1;
            }
            else if (trim_value.ToLower() == "false")
            {
                return false;
            }
            else if (trim_value.ToLower() == "true")
            {
                return true;
            }
            else if (trim_value.IndexOf(":JSONDB::TO_NULL:") > -1 || trim_value.ToLower() == "null")
            {
                return null;
            }
            else if (trim_value.IndexOf(":JSONDB::TO_ARRAY:") > -1)
            {
                return JObject.Parse(_parseValue(trim_value.Replace(":JSONDB::TO_ARRAY:", "")).ToString());
            }
            else if (trim_value[0] == '\'')
            {
                if (trim_value[trim_value.Length - 1] == '\'')
                {
                    return new Regex("\\{\\{quot\\}\\}|\\{\\{comm\\}\\}|\\{\\{dot\\}\\}|\\{\\{pto\\}\\}|\\{\\{ptc\\}\\}|\\{\\{semi\\}\\}").Replace(
                        new Regex("[" + TRIM_CHAR + "]").Replace(trim_value, ""),
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
            }
            else if (new Regex("\\w+\\(.*\\)").IsMatch(trim_value))
            {
                return _parseFunction(trim_value);
            }
            else
            {
                int res;
                if (int.TryParse(new Regex("[" + TRIM_CHAR + "]").Replace(trim_value, "").Trim(), out res))
                {
                    return res;
                }
            }

            throw new Exception("JSONDB Query Parse Error: Unable to parse the value \"" + trim_value + "\".");
        }
    }

    public class MultilineQueryParseException : Exception
    {
        private int _line;
        private Exception _base;

        public int Line
        {
            get { return _line; }
        }

        public override string Message
        {
            get
            {
                return _base.Message;
            }
        }

        public override IDictionary Data
        {
            get
            {
                return _base.Data;
            }
        }

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

        public override string StackTrace
        {
            get
            {
                return _base.StackTrace;
            }
        }

        public MultilineQueryParseException(string message, int line)
        {
            _base = new Exception(message);
            _line = line;
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
