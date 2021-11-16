﻿using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public static class GeneratorUtils {
        public static string Indent(int level) => new(' ', level * 4);

        public static string Indent(string text, int level) {
            string[] lines = text.Trim().Split('\n');
            int maxIndent = 0;
            foreach (string line in lines) {
                int indent = line.TakeWhile(c => c == ' ').Count();
                if (indent > maxIndent) {
                    maxIndent = indent / 4;
                }
            }
            string indentString = Indent(level - maxIndent);
            return indentString + string.Join("\n" + indentString, lines);
        }
        
        public static string ToPascalCase(string name) {
            // remove any leading characters that are not letters
            name = Regex.Replace(name, @"^([^a-zA-Z]*)(.*)", "$2");
            
            // Convert to PascalCase
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        public static PortFieldType GetPortType(string type) {
            switch (type) {
                case "int":                return PortFieldType.Integer;
                case "float":              return PortFieldType.Float;
                case "float3":             return PortFieldType.Vector;
                case "bool":               return PortFieldType.Boolean;
                case "GeometryData":       return PortFieldType.Geometry;
                case "List<GeometryData>": return PortFieldType.Collection;
                case "string":             return PortFieldType.String;
                case "CurveData":          return PortFieldType.Curve;
                default: return PortFieldType.Unknown;
            }
        }

        public static string ExtractFieldNameFromExpression(ExpressionSyntax expression) {
            string expressionString = expression.ToString();
            if (expressionString.StartsWith("nameof")) {
                return expressionString.Substring(7, expressionString.Length - 8);
            }

            return ExtractStringFromExpression(expression);
        }

        public static string ExtractStringFromExpression(ExpressionSyntax expression) {
            string expressionString = expression.ToString();
            if (expressionString.StartsWith("\"") || expressionString.StartsWith("@\"")) {
                int startIndex = expressionString.IndexOf('"');
                int endIndex = expressionString.LastIndexOf('"');
                return expressionString.Substring(startIndex + 1, endIndex - startIndex - 1);
            }

            return null;
        }

        public static string InlineMethod(MethodDeclarationSyntax method) {
            if (method.Body != null) {
                return method.Body.ToString().Trim();
            } else {
                return method.ExpressionBody.Expression.ToString();
            }
        }
    }
}