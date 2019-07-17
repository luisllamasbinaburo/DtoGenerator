using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualBasic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;


namespace DtoGenerator
{
    public class ADOHelper
    {
        private static readonly Dictionary<Type, string> TypeAliases = new Dictionary<Type, string> {
                { typeof(int), "int" },
                { typeof(short), "short" },
                { typeof(byte), "byte" },
                { typeof(byte[]), "byte[]" },
                { typeof(long), "long" },
                { typeof(double), "double" },
                { typeof(decimal), "decimal" },
                { typeof(float), "float" },
                { typeof(bool), "bool" },
                { typeof(string), "string" }
         };

        private static readonly HashSet<Type> NullableTypes = new HashSet<Type> {
                typeof(int),
                typeof(short),
                typeof(long),
                typeof(double),
                typeof(decimal),
                typeof(float),
                typeof(bool),
                typeof(DateTime)
        };

        
       public static void CleanQuery(ref string sql)
        {
            sql = RemoveComments(sql, false, false);
            sql = sql.Replace("\r\n", " ").Trim();

            if (sql.StartsWith("select ", StringComparison.InvariantCultureIgnoreCase) && !sql.StartsWith("select top", StringComparison.InvariantCultureIgnoreCase))
                sql = ReplaceFirstOccurrence(sql, "select", "select top 1");

        }


        public static string RemoveComments(string input, bool preservePositions, bool removeLiterals = false)
        {
            Regex everythingExceptNewLines = new Regex("[^\r\n]");

            //based on http://stackoverflow.com/questions/3524317/regex-to-strip-line-comments-from-c-sharp/3524689#3524689
            var lineComments = @"--(.*?)\r?\n";
            var lineCommentsOnLastLine = @"--(.*?)$"; // because it's possible that there's no \r\n after the last line comment
                                                      // literals ('literals'), bracketedIdentifiers ([object]) and quotedIdentifiers ("object"), they follow the same structure:
                                                      // there's the start character, any consecutive pairs of closing characters are considered part of the literal/identifier, and then comes the closing character
            var literals = @"('(('')|[^'])*')"; // 'John', 'O''malley''s', etc
            var bracketedIdentifiers = @"\[((\]\])|[^\]])* \]"; // [object], [ % object]] ], etc
            var quotedIdentifiers = @"(\""((\""\"")|[^""])*\"")"; // "object", "object[]", etc - when QUOTED_IDENTIFIER is set to ON, they are identifiers, else they are literals
                                                                  //var blockComments = @"/\*(.*?)\*/";  //the original code was for C#, but Microsoft SQL allows a nested block comments // //https://msdn.microsoft.com/en-us/library/ms178623.aspx

            //so we should use balancing groups // http://weblogs.asp.net/whaggard/377025
            var nestedBlockComments = @"/\*
                                 (?>
                                 /\*  (?<LEVEL>)      # On opening push level
                                 | 
                                 \*/ (?<-LEVEL>)     # On closing pop level
                                 |
                                 (?! /\* | \*/ ) . # Match any char unless the opening and closing strings   
                                 )+                         # /* or */ in the lookahead string
                                 (?(LEVEL)(?!))             # If level exists then fail
                                 \*/";

            string noComments = Regex.Replace(input,
                nestedBlockComments + "|" + lineComments + "|" + lineCommentsOnLastLine + "|" + literals + "|" + bracketedIdentifiers + "|" + quotedIdentifiers,
                me =>
                {
                    if (me.Value.StartsWith("/*") && preservePositions)
                        return everythingExceptNewLines.Replace(me.Value, " "); // preserve positions and keep line-breaks // return new string(' ', me.Value.Length);
                    if (me.Value.StartsWith("/*") && !preservePositions)
                        return "";
                    else if (me.Value.StartsWith("--") && preservePositions)
                        return everythingExceptNewLines.Replace(me.Value, " "); // preserve positions and keep line-breaks
                    else if (me.Value.StartsWith("--") && !preservePositions)
                        return everythingExceptNewLines.Replace(me.Value, ""); // preserve only line-breaks // Environment.NewLine;
                    else if (me.Value.StartsWith("[") || me.Value.StartsWith("\""))
                        return me.Value; // do not remove object identifiers ever
                    else if (!removeLiterals) // Keep the literal strings
                        return me.Value;
                    else if (preservePositions) // remove literals, but preserving positions and line-breaks
                    {
                        var literalWithLineBreaks = everythingExceptNewLines.Replace(me.Value, " ");
                        return "'" + literalWithLineBreaks.Substring(1, literalWithLineBreaks.Length - 2) + "'";
                    }
                    else
                        return "''";
                },
                RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
            return noComments;
        }


        public static string DumpClass(IDbConnection connection, string sql, string className = null)
        {
            CleanQuery(ref sql);

            if (connection.State != ConnectionState.Open) connection.Open();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;

                using (var reader = cmd.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SingleRow))
                {
                    var builder = new StringBuilder();
                    do
                    {
                        if (reader.FieldCount <= 1) continue;

                        var schema = reader.GetSchemaTable();
                        if (schema != null)
                            foreach (DataRow row in schema.Rows)
                            {
                                if (string.IsNullOrWhiteSpace(builder.ToString()))
                                {
                                    var tableName = string.IsNullOrWhiteSpace(className) ? row["BaseTableName"] as string ?? "Info" : className;
                                    builder.AppendFormat("public class {0}{1}", tableName, Environment.NewLine);
                                    builder.AppendLine("{");
                                }


                                var type = (Type)row["DataType"];
                                var name = TypeAliases.ContainsKey(type) ? TypeAliases[type] : type.Name;
                                var isNullable = (bool)row["AllowDBNull"] && NullableTypes.Contains(type);
                                var collumnName = (string)row["ColumnName"];

                                builder.AppendLine($"\tpublic {name}{(isNullable ? "?" : string.Empty)} {collumnName} {{ get; set; }}");
                            }

                        builder.AppendLine("}");
                        builder.AppendLine();
                    } while (reader.NextResult());

                    return builder.ToString();
                }
            }
        }

        public static string ReplaceFirstOccurrence(string Source, string Find, string Replace)
        {
            int Place = Source.IndexOf(Find,0, StringComparison.InvariantCultureIgnoreCase);
            string result = Source.Remove(Place, Find.Length).Insert(Place, Replace);
            return result;
        }
    }

      
    }
