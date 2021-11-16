using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public class GeneratedField {
        public FieldDeclarationSyntax Field { get; }
        public TypeSyntax Type { get; }
        public string TypeString { get; }
        public string Name { get; }
        public GeneratedFieldKind Kind { get; }
        
        public string PortName { get; }
        public PortFieldType PortType { get; }
        
        public bool GenerateSerialization { get; private set; }
        public bool GenerateUpdateFromEditorMethod { get; private set; }
        public bool GenerateEquality { get; private set; }

        public GeneratedField(FieldDeclarationSyntax field, GeneratedFieldKind kind) {
            Field = field;
            Kind = kind;
            Type = field.Declaration.Type;
            TypeString = Type.ToString();
            Name = field.Declaration.Variables.First().Identifier.ToString();
            Debugger.Break();

            if (kind != GeneratedFieldKind.OutputPort) {
                Field.AttributeLists.SelectMany(attrs => attrs.Attributes).Select(attr => {
                    if (attr.ArgumentList != null) {
                        attr.ArgumentList.Arguments.Select(arg => {
                            if (arg.NameEquals != null && arg.NameEquals.Name.Identifier.Text == "IsSerialized") {
                                Debugger.Break();
                            }

                            return 0;
                        });
                    }

                    return 0;
                });
            }


            if (kind != GeneratedFieldKind.Setting) {
                PortName = GeneratorUtils.ToPascalCase(Name);
                PortType = GeneratorUtils.GetPortType(TypeString);
            }
            
            GenerateEquality = CanGenerateEquality();
        }

        public string GetSerializationCode() {
            return $"{GeneratorUtils.Indent(4)}{GetSerializationCodeImpl()}";
        }

        public string GetDeserializationCode(int index) {
            return $"{GeneratorUtils.Indent(3)}{GetDeserializationCodeImpl(index)}";
        }

        public string GetPortPropertyDeclaration() {
            return $"{GeneratorUtils.Indent(2)}{string.Format(Templates.PortPropertyTemplate, PortName)}";
        }

        public string GetPortCtorDeclaration() {
            return $"{GeneratorUtils.Indent(3)}{string.Format(Templates.PortCtorTemplate, PortName, PortType.ToString(), Kind == GeneratedFieldKind.InputPort ? "Input" : "Output")}";
        }

        public string GetEqualityComparison(string otherVariableName) {
            return GetEqualityComparisonImpl(otherVariableName);
        }
        
        private bool CanGenerateEquality() {
            if (Kind == GeneratedFieldKind.OutputPort) return false;
            
            if (Kind == GeneratedFieldKind.InputPort) {
                switch (PortType) {
                    case PortFieldType.Integer:
                    case PortFieldType.Float:
                    case PortFieldType.Boolean:
                    case PortFieldType.String:
                        return true;
                    
                    case PortFieldType.Vector:
                    case PortFieldType.Geometry:
                    case PortFieldType.Collection:
                    case PortFieldType.Curve:
                    case PortFieldType.Unknown:
                        return false;
                    
                    default: throw new ArgumentOutOfRangeException();
                }
            }
            
            if (GeneratorContext.EnumTypes.Any(enumDecl => string.Equals(enumDecl.Identifier.ToString(), TypeString, StringComparison.InvariantCulture))) {
                return true;
            }
            
            switch (TypeString) {
                case "int":
                case "bool":
                case "string":
                case "float":
                    return true;
                    
                case "float2":
                case "float3":
                    return false;
                
                default: return true;
            }
        }

        private string GetDeserializationCodeImpl(int index) {
            // Check if Type (TypeSyntax) is an enum
            if (GeneratorContext.EnumTypes.Any(enumDecl => string.Equals(enumDecl.Identifier.ToString(), TypeString, StringComparison.InvariantCulture))) {
                return $"{Name} = ({TypeString}) array.Value<int>({index});";
            }
            
            switch (TypeString) {
                case "bool": return $"{Name} = array.Value<int>({index}) == 1;";
                case "float2": return $"{Name} = JsonSerializer.DeserializeObject<float2>(array.Value<string>({index}), GeometryGraph.Runtime.Serialization.float2Converter.Converter);";
                case "float3": return $"{Name} = JsonSerializer.DeserializeObject<float3>(array.Value<string>({index}), GeometryGraph.Runtime.Serialization.float3Converter.Converter);";
                
                default: return $"{Name} = array.Value<{TypeString}>({index});";
            }
        }
        
        private string GetSerializationCodeImpl() {
            // Check if Type (TypeSyntax) is an enum
            if (GeneratorContext.EnumTypes.Any(enumDecl => string.Equals(enumDecl.Identifier.ToString(), TypeString, StringComparison.InvariantCulture))) {
                return $"(int){Name},";
            }
            
            switch (TypeString) {
                case "bool": return $"{Name} ? 1 : 0,";
                case "float2": return $"JsonSerializer.SerializeObject({Name}, GeometryGraph.Runtime.Serialization.float2Converter.Converter),";
                case "float3": return $"JsonSerializer.SerializeObject({Name}, GeometryGraph.Runtime.Serialization.float3Converter.Converter),";
                
                default: return $"{Name},";
            }
        }
        
        private string GetEqualityComparisonImpl(string otherVariableName) {
            // Check if Type (TypeSyntax) is an enum
            if (GeneratorContext.EnumTypes.Any(enumDecl => string.Equals(enumDecl.Identifier.ToString(), TypeString, StringComparison.InvariantCulture))) {
                return $"{Name} == {otherVariableName}";
            }
            
            switch (TypeString) {
                case "int":
                case "bool":
                    return $"{Name} == {otherVariableName}";
                case "string":
                    return $"string.Equals({Name}, {otherVariableName}, StringComparison.InvariantCulture)";
                case "float":
                    return $"Math.Abs({Name} - {otherVariableName}) < Constants.FLOAT_TOLERANCE";

                default: return $"{Name} == {otherVariableName}";
            }
        }

    }

    public enum GeneratedFieldKind {
        InputPort,
        OutputPort,
        Setting
    }
    
    public enum PortFieldType {
        Integer,
        Float,
        Vector,
        Boolean,
        Geometry,
        Collection,
        String,
        Curve,
        
        Unknown = int.MaxValue
    }

}