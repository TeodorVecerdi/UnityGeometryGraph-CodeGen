using System.Text.RegularExpressions;

namespace SourceGenerator {
    public static class GeneratorUtils {
        public static string Indent(int level) => new string(' ', level * 4);
        
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
    }
}