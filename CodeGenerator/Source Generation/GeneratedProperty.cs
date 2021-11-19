using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public class GeneratedProperty {
        public PropertyDeclarationSyntax Property { get; }
        
        // General info about the property that gets parsed from the property declaration
        public string Type { get; }
        public string Name { get; }
        public GeneratedPropertyKind Kind { get; }

        // PortType if the property is an In or Out property
        public PortPropertyType PortType { get; }
        public string PortName { get; }
        public string OverridePortName { get; private set; }

        // Optionally, the property can update other properties when it changes its value
        public bool UpdatesAllProperties { get; private set; }
        public List<string> UpdatesProperties { get; }

        // Generating a method for updating the property from an editor node can be disabled
        public bool GenerateUpdateFromEditorMethod { get; private set; }
        
        // These indicate whether serialization and equality checks are supported for the property type
        public bool GenerateSerialization { get; private set; }
        public bool GenerateEquality { get; }

        // You can specify custom serialization code for the property
        public bool CustomSerialization { get; private set; }
        public string SerializationCode { get; private set; }
        public string DeserializationCode { get; private set; }

        // You can specify custom equality code for the property
        public bool CustomEquality { get; private set; }
        public string EqualityCode { get; private set; }

        // You can specify a custom getter for the property. It's used in the `GetValueForPort` method
        public bool CustomGetter { get; private set; }
        public string GetterCode { get; private set; }

        public GeneratedProperty(PropertyDeclarationSyntax property, GeneratedPropertyKind kind) {
            Property = property;
            Kind = kind;
            Type = property.Type.ToString();
            Name = property.Identifier.Text;

            GenerateSerialization = true;
            GenerateUpdateFromEditorMethod = true;

            UpdatesAllProperties = kind is not GeneratedPropertyKind.OutputPort;
            UpdatesProperties = new List<string>();

            // Collect specific attributes if the property is an InputPort or Setting
            if (Kind is GeneratedPropertyKind.InputPort or GeneratedPropertyKind.Setting) {
                CollectInputAndSettingAttributes();
            }

            // Collect specific attributes if the property is an OutputPort
            if (Kind is GeneratedPropertyKind.OutputPort) {
                CollectOutputAttributes();
            }

            // Calculate port type if the property is an In or Out property
            if (Kind is not GeneratedPropertyKind.Setting) {
                PortType = GeneratorUtils.GetPortType(Type);
            }

            GenerateSerialization = CustomSerialization || GenerateSerialization && CanGenerateSerialization();
            GenerateEquality = CustomEquality || CanGenerateEquality();
            GenerateUpdateFromEditorMethod = GenerateUpdateFromEditorMethod && CanGenerateUpdateFromEditorMethod();

            PortName = string.IsNullOrEmpty(OverridePortName) ? GeneratorUtils.CapitalizeName(Name) + "Port" : OverridePortName;
        }

        #region Property Parsing

        private void CollectInputAndSettingAttributes() {
            foreach (AttributeSyntax attribute in Property.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) continue;
                SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;

                switch (attribute.Name.ToString()) {
                    case "CustomSerialization": {
                        CustomSerialization = true;

                        string serializationCodeString = GeneratorUtils.ExtractStringFromExpression(arguments[0].Expression);
                        SerializationCode = serializationCodeString.Replace("{self}", Name);

                        string deserializationCodeString = GeneratorUtils.ExtractStringFromExpression(arguments[1].Expression);
                        DeserializationCode = deserializationCodeString.Replace("{self}", Name).Replace("{storage}", "array");
                        break;
                    }
                    case "CustomEquality": {
                        CustomEquality = true;
                        string equalityCodeString = GeneratorUtils.ExtractStringFromExpression(arguments[0].Expression);
                        EqualityCode = equalityCodeString.Replace("{self}", Name);
                        break;
                    }
                    case "In":
                    case "Setting": {
                        foreach (AttributeArgumentSyntax argument in arguments) {
                            if (argument.NameEquals == null) continue;
                            
                            string argName = argument.NameEquals.Name.Identifier.Text;

                            if (argName == "IsSerialized") {
                                string argValue = argument.Expression.ToString();
                                if (argValue != "false") continue;
                                GenerateSerialization = false;
                            } else if (argName == "UpdatedFromEditorNode") {
                                string argValue = argument.Expression.ToString();
                                if (argValue != "false") continue;
                                GenerateUpdateFromEditorMethod = false;
                            } else if (argName == "PortName") {
                                string portName = GeneratorUtils.ExtractStringFromExpression(argument.Expression);
                                OverridePortName = portName;
                            }
                        }

                        break;
                    }
                    case "UpdatesProperties": {
                        UpdatesAllProperties = false;
                        foreach (AttributeArgumentSyntax argument in arguments) {
                            string variableName = GeneratorUtils.ExtractNameFromExpression(argument.Expression);
                            if (!string.IsNullOrEmpty(variableName)) {
                                UpdatesProperties.Add(variableName);
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void CollectOutputAttributes() {
            foreach (AttributeSyntax attribute in Property.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) continue;
                SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;
                
                switch (attribute.Name.ToString()) {
                    case "Getter": {
                        CustomGetter = true;
                        string getterString = GeneratorUtils.ExtractStringFromExpression(arguments[0].Expression);
                        GetterCode = getterString.Replace("{self}", Name);
                        break;
                    }
                    case "Out": {
                        foreach (AttributeArgumentSyntax argument in arguments) {
                            if (argument.NameEquals == null) continue;
                            
                            string argName = argument.NameEquals.Name.Identifier.Text;
                            if (argName == "PortName") {
                                string portName = GeneratorUtils.ExtractStringFromExpression(argument.Expression);
                                OverridePortName = portName;
                            }
                        }
                        break;
                    }
                }
            }
        }
        

        #endregion
        
        #region Code Generation API

        public string GetSerializationCode(int indentation) {
            return $"{GeneratorUtils.Indent(indentation + 2)}{GetSerializationCodeImpl()}";
        }

        public string GetDeserializationCode(int indentation, int index) {
            return $"{GeneratorUtils.Indent(indentation + 1)}{GetDeserializationCodeImpl(index)}";
        }

        public string GetPortPropertyDeclaration(int indentation) {
            return $"{GeneratorUtils.Indent(indentation)}{string.Format(Templates.PortPropertyTemplate, PortName)}";
        }

        public string GetPortCtorDeclaration(int indentation) {
            return
                $"{GeneratorUtils.Indent(indentation + 1)}{string.Format(Templates.PortCtorTemplate, PortName, PortType.ToString(), Kind == GeneratedPropertyKind.InputPort ? "Input" : "Output")}";
        }

        public string GetEqualityComparison(string otherVariableName) {
            return GetEqualityComparisonImpl(otherVariableName);
        }

        public string GetUpdateFromEditorNodeMethod(int indentation, string calculate, string notify) {
            return GetUpdateFromEditorNodeMethodImpl(indentation, calculate, notify);
        }

        public string GetGetValueForPortCode(int indentation) {
            return GetGetValueForPortCodeImpl(indentation);
        }

        public string GetOnPortValueChangedCode(int indentation, string calculate, string notify) {
            return GetOnPortValueChangedCodeImpl(indentation, calculate, notify);
        }

        public string GetDebugCode() {
            return
                $"Debug Info for property `{Name}`:\n    UpdatesAllProperties: {UpdatesAllProperties}\n    UpdatesProperties: {string.Join(", ", UpdatesProperties)}\n    HasCustomGetter: {CustomGetter}\n    GetterCode: `{GetterCode}`\n";
        }

        #endregion

        #region Code Generation Implementation

        private string GetSerializationCodeImpl() {
            if (CustomSerialization) {
                return $"{SerializationCode},";
            }

            // Check if Type (TypeSyntax) is an enum
            if (GeneratorContext.EnumTypes.ContainsKey(Type)) {
                string backingType = GeneratorContext.EnumTypes[Type];
                return $"({backingType}){Name},";
            }

            switch (Type) {
                case "bool": return $"{Name} ? 1 : 0,";
                case "float2": return $"JsonConvert.SerializeObject({Name}, float2Converter.Converter),";
                case "float3": return $"JsonConvert.SerializeObject({Name}, float3Converter.Converter),";

                default: return $"{Name},";
            }
        }

        private string GetDeserializationCodeImpl(int index) {
            if (CustomSerialization) {
                return $"{DeserializationCode.Replace("{index}", index.ToString())};";
            }

            // Check if Type (TypeSyntax) is an enum
            if (GeneratorContext.EnumTypes.ContainsKey(Type)) {
                string backingType = GeneratorContext.EnumTypes[Type];
                return $"{Name} = ({Type}) array.Value<{backingType}>({index});";
            }

            switch (Type) {
                case "bool": return $"{Name} = array.Value<int>({index}) == 1;";
                case "float2": return $"{Name} = JsonConvert.DeserializeObject<float2>(array.Value<string>({index}), float2Converter.Converter);";
                case "float3": return $"{Name} = JsonConvert.DeserializeObject<float3>(array.Value<string>({index}), float3Converter.Converter);";

                default: return $"{Name} = array.Value<{Type}>({index});";
            }
        }

        private string GetEqualityComparisonImpl(string otherVariableName) {
            if (CustomEquality) {
                return EqualityCode.Replace("{other}", otherVariableName);
            }

            // Check if Type (TypeSyntax) is an enum
            if (GeneratorContext.EnumTypes.ContainsKey(Type)) {
                return $"{Name} == {otherVariableName}";
            }

            switch (Type) {
                case "int":
                case "bool":
                    return $"{Name} == {otherVariableName}";
                case "string":
                    return $"string.Equals({Name}, {otherVariableName}, StringComparison.InvariantCulture)";
                case "float":
                    return $"Math.Abs({Name} - {otherVariableName}) < Constants.FLOAT_TOLERANCE";
                case "float2":
                case "float3":
                    return $"math.distancesq({Name}, {otherVariableName}) < Constants.FLOAT_TOLERANCE * Constants.FLOAT_TOLERANCE";

                default: return $"{Name} == {otherVariableName}";
            }
        }

        private string GetUpdateFromEditorNodeMethodImpl(int indentation, string calculate, string notify) {
            string equality = "";
            if (GenerateEquality) {
                equality = $"\n{GeneratorUtils.Indent(indentation + 1)}if({GetEqualityComparisonImpl("newValue")}) return;";
            }

            return string.Format(Templates.UpdateFromEditorNodeTemplate, GeneratorUtils.Indent(indentation), PortName, Type, equality, Name, $"\n{calculate}", notify.TrimEnd());
        }

        private string GetGetValueForPortCodeImpl(int indentation) {
            return $"{GeneratorUtils.Indent(indentation + 1)}if (port == {PortName}) return {Name};";
        }

        private string GetOnPortValueChangedCodeImpl(int indentation, string calculate, string notify) {
            string equality = "";
            if (GenerateEquality) {
                equality = $"\n{GeneratorUtils.Indent(indentation + 2)}if({GetEqualityComparisonImpl("newValue")}) return;";
            }

            return string.Format(Templates.OnPortValueChangedIfTemplate, GeneratorUtils.Indent(indentation + 1), PortName, Name, equality, $"\n{calculate}", $"{notify.TrimEnd()}");
        }
        
        #endregion

        #region Checks

        private bool CanGenerateEquality() {
            if (CustomEquality) return true;
            if (Kind == GeneratedPropertyKind.OutputPort) return false;

            if (GeneratorContext.EnumTypes.ContainsKey(Type)) {
                return true;
            }

            switch (Type) {
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

        private bool CanGenerateUpdateFromEditorMethod() {
            if (Kind == GeneratedPropertyKind.OutputPort) return false;

            if (GeneratorContext.EnumTypes.ContainsKey(Type)) {
                return true;
            }

            switch (Type) {
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
            if (Kind == GeneratedPropertyKind.OutputPort) return false;

            if (GeneratorContext.EnumTypes.ContainsKey(Type)) {
                return true;
            }

            switch (Type) {
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

    public enum GeneratedPropertyKind {
        InputPort,
        OutputPort,
        Setting
    }

    public enum PortPropertyType {
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