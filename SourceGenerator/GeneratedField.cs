using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public class GeneratedField {
        public FieldDeclarationSyntax Field { get; }
        public TypeSyntax Type { get; }
        public string TypeString { get; }
        public string Name { get; }
        public GeneratedFieldKind Kind { get; }
        public string PascalCaseName { get; }

        public PortFieldType PortType { get; }

        public bool UpdatesAllFields { get; set; }
        public List<string> UpdatesFields { get; }

        public bool GenerateUpdateFromEditorMethod { get; set; }
        public bool GenerateSerialization { get; set; }
        public bool GenerateEquality { get; }

        public bool CustomSerialization { get; set; }
        public string SerializationCode { get; set; }
        public string DeserializationCode { get; set; }

        public bool CustomEquality { get; set; }
        public string EqualityCode { get; set; }

        public bool CustomGetter { get; set; }
        public string GetterCode { get; set; }

        public GeneratedField(FieldDeclarationSyntax field, GeneratedFieldKind kind) {
            Field = field;
            Kind = kind;
            Type = field.Declaration.Type;
            TypeString = Type.ToString();
            Name = field.Declaration.Variables.First().Identifier.ToString();
            PascalCaseName = GeneratorUtils.ToPascalCase(Name);

            GenerateSerialization = true;
            GenerateUpdateFromEditorMethod = true;

            UpdatesAllFields = kind != GeneratedFieldKind.OutputPort;
            UpdatesFields = new List<string>();

            if (Kind != GeneratedFieldKind.OutputPort) {
                CollectInputAndSettingAttributes();
            }

            if (Kind == GeneratedFieldKind.OutputPort) {
                CollectOutputAttributes();
            }

            if (Kind != GeneratedFieldKind.Setting) {
                PortType = GeneratorUtils.GetPortType(TypeString);
            }

            GenerateSerialization = CustomSerialization || GenerateSerialization && CanGenerateSerialization();
            GenerateEquality = CustomEquality || CanGenerateEquality();
            GenerateUpdateFromEditorMethod = GenerateUpdateFromEditorMethod && CanGenerateUpdateFromEditorMethod();
        }

        private void CollectInputAndSettingAttributes() {
            foreach (AttributeSyntax attribute in Field.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.ArgumentList == null) continue;
                List<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments.ToList();
                if (arguments.Count == 0) continue;

                switch (attribute.Name.ToString()) {
                    case "CustomSerialization": {
                        CustomSerialization = true;

                        string serializationCodeString = GeneratorUtils.ExtractStringFromExpression(arguments[0].Expression);
                        SerializationCode = serializationCodeString.Replace("{field}", Name);

                        string deserializationCodeString = GeneratorUtils.ExtractStringFromExpression(arguments[1].Expression);
                        DeserializationCode = deserializationCodeString.Replace("{field}", Name).Replace("{storage}", "array");
                        break;
                    }
                    case "CustomEquality": {
                        CustomEquality = true;
                        string equalityCodeString = arguments[0].Expression.ToString();
                        EqualityCode = equalityCodeString.Replace("{this}", Name);
                        break;
                    }
                    case "In":
                    case "Setting": {
                        foreach (AttributeArgumentSyntax argument in arguments.Where(argument => argument.NameEquals != null)) {
                            string argName = argument.NameEquals.Name.Identifier.Text;
                            string argValue = GeneratorUtils.ExtractStringFromExpression(argument.Expression);

                            if (argValue != "false") continue;
                            
                            if (argName == "IsSerialized") {
                                GenerateSerialization = false;
                            } else if (argName == "UpdatedFromEditorNode") {
                                GenerateUpdateFromEditorMethod = false;
                            }
                        }

                        break;
                    }
                    case "UpdatesFields": {
                        UpdatesAllFields = false;
                        foreach (AttributeArgumentSyntax argument in arguments) {
                            string variableName = GeneratorUtils.ExtractFieldNameFromExpression(argument.Expression);
                            if (!string.IsNullOrEmpty(variableName)) {
                                UpdatesFields.Add(variableName);
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void CollectOutputAttributes() {
            foreach (AttributeSyntax attribute in Field.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.ArgumentList == null) continue;
                List<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments.ToList();
                if (arguments.Count == 0) continue;
                
                switch (attribute.Name.ToString()) {
                    case "Getter": {
                        CustomGetter = true;
                        string getterString = GeneratorUtils.ExtractStringFromExpression(arguments[0].Expression);
                        GetterCode = getterString.Replace("{field}", Name);
                        break;
                    }
                }
            }
        }

        #region Code Generation API

        public string GetSerializationCode() {
            return $"{GeneratorUtils.Indent(4)}{GetSerializationCodeImpl()}";
        }

        public string GetDeserializationCode(int index) {
            return $"{GeneratorUtils.Indent(3)}{GetDeserializationCodeImpl(index)}";
        }

        public string GetPortPropertyDeclaration() {
            return $"{GeneratorUtils.Indent(2)}{string.Format(Templates.PortPropertyTemplate, PascalCaseName)}";
        }

        public string GetPortCtorDeclaration() {
            return
                $"{GeneratorUtils.Indent(3)}{string.Format(Templates.PortCtorTemplate, PascalCaseName, PortType.ToString(), Kind == GeneratedFieldKind.InputPort ? "Input" : "Output")}";
        }

        public string GetEqualityComparison(string otherVariableName) {
            return GetEqualityComparisonImpl(otherVariableName);
        }

        public string GetUpdateFromEditorNodeMethod(string calculate, string notify) {
            return GetUpdateFromEditorNodeMethodImpl(calculate, notify);
        }

        public string GetGetValueForPortCode() {
            return GetGetValueForPortCodeImpl();
        }

        public string GetOnPortValueChangedCode(string calculate, string notify) {
            return GetOnPortValueChangedCodeImpl(calculate, notify);
        }

        public string GetDebugCode() {
            return
                $"Debug Info for field `{Name}`:\n    UpdatesAllFields: {UpdatesAllFields}\n    UpdatesFields: {string.Join(", ", UpdatesFields)}\n    HasCustomGetter: {CustomGetter}\n    GetterCode: `{GetterCode}`\n";
        }

        #endregion

        #region Code Generation Implementation

        private string GetSerializationCodeImpl() {
            if (CustomSerialization) {
                return $"{SerializationCode},";
            }

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

        private string GetDeserializationCodeImpl(int index) {
            if (CustomSerialization) {
                return $"{DeserializationCode.Replace("{index}", index.ToString())};";
            }

            // Check if Type (TypeSyntax) is an enum
            if (GeneratorContext.EnumTypes.Any(enumDecl => string.Equals(enumDecl.Identifier.ToString(), TypeString, StringComparison.InvariantCulture))) {
                return $"{Name} = ({TypeString}) array.Value<int>({index});";
            }

            switch (TypeString) {
                case "bool": return $"{Name} = array.Value<int>({index}) == 1;";
                case "float2":
                    return $"{Name} = JsonSerializer.DeserializeObject<float2>(array.Value<string>({index}), GeometryGraph.Runtime.Serialization.float2Converter.Converter);";
                case "float3":
                    return $"{Name} = JsonSerializer.DeserializeObject<float3>(array.Value<string>({index}), GeometryGraph.Runtime.Serialization.float3Converter.Converter);";

                default: return $"{Name} = array.Value<{TypeString}>({index});";
            }
        }

        private string GetEqualityComparisonImpl(string otherVariableName) {
            if (CustomEquality) {
                return EqualityCode.Replace("{other}", otherVariableName);
            }

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

        private string GetUpdateFromEditorNodeMethodImpl(string calculate, string notify) {
            string equality = "";
            if (GenerateEquality) {
                equality = $"\n{GeneratorUtils.Indent(3)}if({GetEqualityComparisonImpl("newValue")}) return;";
            }

            return string.Format(Templates.UpdateFromEditorNodeTemplate, GeneratorUtils.Indent(2), PascalCaseName, TypeString, equality, Name, $"\n{calculate}", notify.TrimEnd());
        }

        private string GetGetValueForPortCodeImpl() {
            return $"{GeneratorUtils.Indent(3)}if (port == {PascalCaseName}Port) return {Name};";
        }

        public string GetOnPortValueChangedCodeImpl(string calculate, string notify) {
            string equality = "";
            if (GenerateEquality) {
                equality = $"\n{GeneratorUtils.Indent(4)}if({GetEqualityComparisonImpl("newValue")}) return;";
            }

            return string.Format(Templates.OnPortValueChangedIfTemplate, GeneratorUtils.Indent(3), PascalCaseName, Name, equality, $"\n{calculate}", $"{notify.TrimEnd()}");
        }

        #endregion

        #region Checks

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

        private bool CanGenerateUpdateFromEditorMethod() {
            if (Kind == GeneratedFieldKind.OutputPort) return false;

            if (Kind == GeneratedFieldKind.InputPort) {
                switch (PortType) {
                    case PortFieldType.Integer:
                    case PortFieldType.Float:
                    case PortFieldType.Boolean:
                    case PortFieldType.String:
                    case PortFieldType.Vector:
                        return true;

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
                case "float2":
                case "float3":
                    return true;

                default: return true;
            }
        }

        private bool CanGenerateSerialization() {
            if (CustomSerialization) return true;
            if (Kind == GeneratedFieldKind.OutputPort) return false;

            if (Kind == GeneratedFieldKind.InputPort) {
                switch (PortType) {
                    case PortFieldType.Integer:
                    case PortFieldType.Float:
                    case PortFieldType.Boolean:
                    case PortFieldType.String:
                    case PortFieldType.Vector:
                        return true;

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
                case "float2":
                case "float3":
                    return true;
                default: return false;
            }
        }

        #endregion
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