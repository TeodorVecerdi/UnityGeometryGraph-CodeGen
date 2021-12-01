using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public static class GeneratorUtils {
        /// <summary>
        /// Returns a string of spaces of length 4 * <paramref name="level"/>.
        /// </summary>
        public static string Indent(int level) => new(' ', level * 4);
        
        /// <summary>
        /// Changes the indentation of <paramref name="text"/> to be at level <paramref name="level"/>.
        /// </summary>
        /// <param name="text">The text to indent.</param>
        /// <param name="level">The level to indent to.</param>
        /// <returns><paramref name="text"/> indented to level <paramref name="level"/>.</returns>
        public static string Indent(string text, int level) {
            string[] lines = text.Trim().Split('\n');
            
            // Find the maximum indentation level of all lines
            int maxIndent = 0;
            foreach (string line in lines) {
                int indent = line.TakeWhile(c => c == ' ').Count();
                if (indent > maxIndent) {
                    maxIndent = indent / 4;
                }
            }
            // Indent all lines by level - maxIndent
            string indentString = Indent(level - maxIndent);
            return indentString + string.Join("\n" + indentString, lines);
        }
        
        /// <summary>
        /// Capitalizes the first letter of <paramref name="name"/> and removes any leading characters that are not letters.
        /// </summary>
        /// <param name="name">The name to convert.</param>
        /// <returns>The name capitalized and with any leading characters that are not letters removed.</returns>
        /// <example>
        /// <code>
        /// ToPascalCase("hello") == "Hello"
        /// ToPascalCase("_012_hello") == "Hello"
        /// </code>
        /// </example>
        public static string CapitalizeName(string name) {
            // remove any leading characters that are not letters
            name = Regex.Replace(name, @"^([^a-zA-Z]*)(.*)", "$2");
            
            // Convert to PascalCase
            return char.ToUpper(name[0]) + name.Substring(1);
        }
        
        /// <summary>
        /// Converts a type name to the equivalent PortPropertyType enum.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The equivalent PortPropertyType enum.</returns>
        /// <example>
        /// <code>
        /// GetPortType("int") == PortPropertyType.Integer
        /// GetPortType("float") == PortPropertyType.Float
        /// </code>
        /// </example>
        public static PortPropertyType GetPortType(string type) {
            switch (type) {
                case "int":                return PortPropertyType.Integer;
                case "float":              return PortPropertyType.Float;
                case "float3":             return PortPropertyType.Vector;
                case "bool":               return PortPropertyType.Boolean;
                case "GeometryData":       return PortPropertyType.Geometry;
                case "List<GeometryData>": return PortPropertyType.Collection;
                case "string":             return PortPropertyType.String;
                case "CurveData":          return PortPropertyType.Curve;
                case "InstancedGeometryData": return PortPropertyType.Instances;
                default: return PortPropertyType.Unknown;
            }
        }
       
        /// <summary>
        /// Extracts the name of a variable from an expression.
        /// </summary>
        /// <param name="expression">The expression to extract the name from.</param>
        /// <returns>The name of the variable.</returns>
        /// <remarks>
        /// This method works on expressions of the form: <c>nameof(variable)</c>, <c>"variable"</c>
        /// </remarks>
        public static string ExtractNameFromExpression(ExpressionSyntax expression) {
            string expressionString = expression.ToString();
            if (expressionString.StartsWith("nameof")) {
                return expressionString.Substring(7, expressionString.Length - 8);
            }

            return ExtractStringFromExpression(expression);
        }
        
        /// <summary>
        /// Extracts a string from an expression.
        /// </summary>
        /// <param name="expression">The expression to extract the string from.</param>
        /// <returns>The string that the expression represents.</returns>
        /// <example>
        /// <code>
        /// ExtractStringFromExpression("\"hello\"") == "hello"
        /// ExtractStringFromExpression("@\"hello\"") == "hello"
        /// </code>
        /// </example>
        public static string ExtractStringFromExpression(ExpressionSyntax expression) {
            string expressionString = expression.ToString();
            if (expressionString.StartsWith("\"") || expressionString.StartsWith("@\"")) {
                int startIndex = expressionString.IndexOf('"');
                int endIndex = expressionString.LastIndexOf('"');
                return expressionString.Substring(startIndex + 1, endIndex - startIndex - 1);
            }

            return null;
        }
        
        /// <summary>
        /// Returns the body of the method, or the expression body if the method has no body.
        /// </summary>
        /// <param name="method">The method to extract the body from.</param>
        /// <returns>The body of the method, or the expression body if the method has no body.</returns>
        public static string InlineMethod(MethodDeclarationSyntax method) {
            return method.Body != null ? method.Body.ToString().Trim() : method.ExpressionBody.Expression.ToString();
        }

        public static string GetQualifiedClassName(GeneratedClass generatedClass) {
            return $"{generatedClass.AssemblyName}::{generatedClass.NamespaceName}::{generatedClass.ClassName}";
        }
        
        public static string UnescapeString(string str) {
            str = str
                  .Replace("\\r\\n", "\n")
                  .Replace("\\n", "\n")
                  .Replace("\\t", "\t")
                  .Replace("\\\"", "\"")
                  .Replace("\\'", "'")
                  .Replace("\\\\", "\\");
            return str;
        }
        
        public static string AddSemicolonIfNeeded(string str) {
            if (!str.EndsWith(";") && !str.TrimStart().StartsWith("//") && !str.TrimEnd().EndsWith("}")) str = $"{str};";
            return str;
        }

        public static bool IsEnum(string type, string ownerClassName) {
            if (GeneratorContext.EnumTypes.ContainsKey(type)) return true;
            if (GeneratorContext.EnumTypes.ContainsKey($"{ownerClassName}.{type}")) return true;
            return false;
        }
        
        public static string GetEnumBackingType(string type, string ownerClassName) {
            if (GeneratorContext.EnumTypes.ContainsKey(type)) return GeneratorContext.EnumTypes[type];
            if (GeneratorContext.EnumTypes.ContainsKey($"{ownerClassName}.{type}")) return GeneratorContext.EnumTypes[$"{ownerClassName}.{type}"];
            return null;
        }
        
        public static string CleanupUsingStatement(string usingStatement) {
            usingStatement = usingStatement.Trim();
            if (usingStatement.StartsWith("using")) usingStatement = usingStatement[6..];
            if (usingStatement.EndsWith(";")) usingStatement = usingStatement[..^1];

            return usingStatement;
        }

        private static readonly List<char> invalidCharacters = Path.GetInvalidFileNameChars().Union(Path.GetInvalidPathChars()).Union(new char[] {'#', '<', '>', '#', '+', '%', '!', '`', '&', '*', '\'', '"', '|', '{', '}', '?', '=', '/', ':', '\\', '@', ';'}).ToList();
        public static string RemoveInvalidPathCharacters(string path) {
            return new string(path.Where(c => !invalidCharacters.Contains(c)).ToArray());
        }
    }
}