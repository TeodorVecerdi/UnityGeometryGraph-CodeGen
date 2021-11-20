using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static SourceGenerator.GeneratorUtils;

namespace SourceGenerator {
    public class GeneratedProperty {
        public PropertyDeclarationSyntax Property { get; }

        // General info about the property that gets parsed from the property declaration
        public string Type { get; }
        public string Name { get; }
        public string UppercaseName { get; }
        public GeneratedPropertyKind Kind { get; }

        // Port-specific info about the property
        public PortPropertyType PortType { get; }
        public string PortName { get; }
        public string OverridePortName { get; private set; }
        public string DefaultValue { get; private set; }
        public string UpdateValueCode { get; private set; }
        public string GetValueCode { get; private set; }
        public bool CallNotifyMethodsIfChanged { get; private set; }
        public bool CallCalculateMethodsIfChanged { get; private set; }

        // Optionally, the property can update other properties when it changes its value
        public bool UpdatesAllProperties { get; private set; }
        public List<string> UpdatesProperties { get; }

        // Generating a method for updating the property from an editor node can be disabled
        public bool GenerateUpdateFromEditorMethod { get; private set; }

        // These indicate whether serialization and equality checks are supported for the property type
        public bool GenerateSerialization { get; private set; }
        public bool GenerateEquality { get; private set; }

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

        // You can add custom code to the OnPortValueChanged method
        public List<string> AdditionalValueChangedCode_BeforeGetValue { get; }
        public List<string> AdditionalValueChangedCode_AfterGetValue { get; }
        public List<string> AdditionalValueChangedCode_AfterEqualityCheck { get; }
        public List<string> AdditionalValueChangedCode_AfterUpdate { get; }
        public List<string> AdditionalValueChangedCode_AfterCalculate { get; }
        public List<string> AdditionalValueChangedCode_AfterNotify { get; }

        private readonly GeneratedClass owner;

        public GeneratedProperty(GeneratedClass owner, PropertyDeclarationSyntax property, GeneratedPropertyKind kind) {
            this.owner = owner;
            Property = property;
            Kind = kind;
            Type = property.Type.ToString();
            Name = property.Identifier.Text;

            GenerateSerialization = true;
            GenerateEquality = true;
            GenerateUpdateFromEditorMethod = true;

            UpdatesAllProperties = kind is not GeneratedPropertyKind.OutputPort;
            UpdatesProperties = new List<string>();

            AdditionalValueChangedCode_BeforeGetValue = new List<string>();
            AdditionalValueChangedCode_AfterGetValue = new List<string>();
            AdditionalValueChangedCode_AfterEqualityCheck = new List<string>();
            AdditionalValueChangedCode_AfterUpdate = new List<string>();
            AdditionalValueChangedCode_AfterCalculate = new List<string>();
            AdditionalValueChangedCode_AfterNotify = new List<string>();
            
            DefaultValue = kind == GeneratedPropertyKind.InputPort ? "{self}" : "default({type})";
            UpdateValueCode = "{self} = {other};";
            GetValueCode = "var {other} = GetValue(connection, {default});";
            CallNotifyMethodsIfChanged = true;
            CallCalculateMethodsIfChanged = true;

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
                PortType = GetPortType(Type);
            }

            GenerateSerialization = CustomSerialization || GenerateSerialization && CanGenerateSerialization();
            GenerateEquality = CustomEquality || GenerateEquality && CanGenerateEquality();
            GenerateUpdateFromEditorMethod = GenerateUpdateFromEditorMethod && CanGenerateUpdateFromEditorMethod();

            UppercaseName = CapitalizeName(Name);
            PortName = string.IsNullOrEmpty(OverridePortName) ? "{Self}Port" : OverridePortName;
            
            // Expand Placeholders
            PortName = ExpandPlaceholders(PortName, null, "self", "Self");
            DefaultValue = ExpandPlaceholders(DefaultValue, null, "self", "portName", "type");
            GetValueCode = ExpandPlaceholders(GetValueCode, null, "self", "portName", "default");
            UpdateValueCode = ExpandPlaceholders(UpdateValueCode, null, "self", "portName", "default");
            if (CustomSerialization) {
                SerializationCode = ExpandPlaceholders(SerializationCode, null, "self", "type", "default");
                DeserializationCode = ExpandPlaceholders(DeserializationCode, new ExpandVariables{Storage = "array"}, "self", "type", "default", "storage");
            }
            if (CustomEquality) {
                EqualityCode = ExpandPlaceholders(EqualityCode, null, "self", "type", "default");
            }
            if (CustomGetter) {
                GetterCode = ExpandPlaceholders(GetterCode, null, "self", "portName", "type", "default");
            }
            for (int i = 0; i < AdditionalValueChangedCode_BeforeGetValue.Count; i++) {
                AdditionalValueChangedCode_BeforeGetValue[i] = ExpandPlaceholders(AdditionalValueChangedCode_BeforeGetValue[i], null, "self", "portName", "type", "default");
            }
            for(int i = 0; i < AdditionalValueChangedCode_AfterGetValue.Count; i++) {
                AdditionalValueChangedCode_AfterGetValue[i] = ExpandPlaceholders(AdditionalValueChangedCode_AfterGetValue[i], null, "self", "portName", "type", "default");
            }
            for(int i = 0; i < AdditionalValueChangedCode_AfterEqualityCheck.Count; i++) {
                AdditionalValueChangedCode_AfterEqualityCheck[i] = ExpandPlaceholders(AdditionalValueChangedCode_AfterEqualityCheck[i], null, "self", "portName", "type", "default");
            }
            for(int i = 0; i < AdditionalValueChangedCode_AfterUpdate.Count; i++) {
                AdditionalValueChangedCode_AfterUpdate[i] = ExpandPlaceholders(AdditionalValueChangedCode_AfterUpdate[i], null, "self", "portName", "type", "default");
            }
            for(int i = 0; i < AdditionalValueChangedCode_AfterCalculate.Count; i++) {
                AdditionalValueChangedCode_AfterCalculate[i] = ExpandPlaceholders(AdditionalValueChangedCode_AfterCalculate[i], null, "self", "portName", "type", "default");
            }
            for(int i = 0; i < AdditionalValueChangedCode_AfterNotify.Count; i++) {
                AdditionalValueChangedCode_AfterNotify[i] = ExpandPlaceholders(AdditionalValueChangedCode_AfterNotify[i], null, "self", "portName", "type", "default");
            }
        }

        #region Property Parsing

        private void CollectInputAndSettingAttributes() {
            foreach (AttributeSyntax attribute in Property.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) {
                    switch (attribute.Name.ToString()) {
                        case "UpdatesProperties": {
                            UpdatesAllProperties = false;
                            break;
                        }
                    }

                    continue;
                }
                
                SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;
                switch (attribute.Name.ToString()) {
                    case "CustomSerialization": {
                        CustomSerialization = true;

                        SerializationCode = ExtractStringFromExpression(arguments[0].Expression);
                        DeserializationCode = ExtractStringFromExpression(arguments[1].Expression);
                        break;
                    }
                    case "CustomEquality": {
                        CustomEquality = true;
                        EqualityCode = ExtractStringFromExpression(arguments[0].Expression);
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
                                string portName = ExtractStringFromExpression(argument.Expression);
                                OverridePortName = portName;
                            } else if (argName == "GenerateEquality") {
                                string argValue = argument.Expression.ToString();
                                if (argValue != "false") continue;
                                GenerateEquality = false;
                            } else if (argName == "DefaultValue") {
                                string argValue = ExtractStringFromExpression(argument.Expression);
                                DefaultValue = argValue;
                            } else if (argName == "GetValueCode") {
                                string argValue = ExtractStringFromExpression(argument.Expression);
                                GetValueCode = argValue;
                            } else if (argName == "UpdateValueCode") {
                                string argValue = ExtractStringFromExpression(argument.Expression);
                                UpdateValueCode = argValue;
                            } else if (argName == "CallNotifyMethodsIfChanged") {
                                string argValue = argument.Expression.ToString();
                                if (argValue != "false") continue;
                                CallNotifyMethodsIfChanged = false;
                            } else if (argName == "CallCalculateMethodsIfChanged") {
                                string argValue = argument.Expression.ToString();
                                if (argValue != "false") continue;
                                CallCalculateMethodsIfChanged = false;
                            }
                        }

                        break;
                    }
                    case "UpdatesProperties": {
                        UpdatesAllProperties = false;
                        foreach (AttributeArgumentSyntax argument in arguments) {
                            string variableName = ExtractNameFromExpression(argument.Expression);
                            if (!string.IsNullOrEmpty(variableName)) {
                                UpdatesProperties.Add(variableName);
                            }
                        }

                        break;
                    }
                    case "AdditionalValueChangedCode": {
                        string argValue = ExtractStringFromExpression(arguments[0].Expression);
                        if (string.IsNullOrEmpty(argValue)) continue;

                        if (arguments.Count == 1) {
                            AdditionalValueChangedCode_AfterUpdate.Add(argValue);
                        } else {
                            string location = arguments[1].Expression.ToString().Replace("AdditionalValueChangedCodeAttribute.Location.", "");
                            switch (location) {
                                case "BeforeGetValue": {
                                    AdditionalValueChangedCode_BeforeGetValue.Add(argValue);
                                    break;
                                }
                                case "AfterGetValue": {
                                    AdditionalValueChangedCode_AfterGetValue.Add(argValue);
                                    break;
                                }
                                case "AfterEqualityCheck": {
                                    AdditionalValueChangedCode_AfterEqualityCheck.Add(argValue);
                                    break;
                                }
                                case "AfterUpdate": {
                                    AdditionalValueChangedCode_AfterUpdate.Add(argValue);
                                    break;
                                }
                                case "AfterCalculate": {
                                    AdditionalValueChangedCode_AfterCalculate.Add(argValue);
                                    break;
                                }
                                case "AfterNotify": {
                                    AdditionalValueChangedCode_AfterNotify.Add(argValue);
                                    break;
                                }
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
                        string getterString = ExtractStringFromExpression(arguments[0].Expression);
                        GetterCode = getterString;
                        break;
                    }
                    case "Out": {
                        foreach (AttributeArgumentSyntax argument in arguments) {
                            if (argument.NameEquals == null) continue;

                            string argName = argument.NameEquals.Name.Identifier.Text;
                            if (argName == "PortName") {
                                string portName = ExtractStringFromExpression(argument.Expression);
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
            return $"{Indent(indentation + 2)}{GetSerializationCodeImpl()}";
        }

        public string GetDeserializationCode(int indentation, int index) {
            return $"{Indent(indentation + 1)}{GetDeserializationCodeImpl(index, indentation + 1)}";
        }

        public string GetPortPropertyDeclaration(int indentation) {
            return $"{Indent(indentation)}{string.Format(Templates.PortPropertyTemplate, PortName)}";
        }

        public string GetPortCtorDeclaration(int indentation) {
            return
                $"{Indent(indentation + 1)}{string.Format(Templates.PortCtorTemplate, PortName, PortType.ToString(), Kind == GeneratedPropertyKind.InputPort ? "Input" : "Output")}";
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
            if (IsEnum(Type, owner.ClassName)) {
                string backingType = GetEnumBackingType(Type, owner.ClassName);
                return $"({backingType}){Name},";
            }

            switch (Type) {
                case "bool": return $"{Name} ? 1 : 0,";
                case "float2": return $"JsonConvert.SerializeObject({Name}, float2Converter.Converter),";
                case "float3": return $"JsonConvert.SerializeObject({Name}, float3Converter.Converter),";

                default: return $"{Name},";
            }
        }

        private string GetDeserializationCodeImpl(int index, int indent) {
            if (CustomSerialization) {
                return $"{ExpandPlaceholders(DeserializationCode, new ExpandVariables{Index = index, Indent = Indent(indent)}, "index", "indent")};";
            }

            // Check if Type (TypeSyntax) is an enum
            if (IsEnum(Type, owner.ClassName)) {
                string backingType = GetEnumBackingType(Type, owner.ClassName);
                return $"{Name} = ({Type}) array.Value<{backingType}>({index});";
            }

            switch (Type) {
                case "bool": return $"{Name} = array.Value<int>({index}) == 1;";
                case "float2": return $"{Name} = JsonConvert.DeserializeObject<float2>(array.Value<string>({index}), float2Converter.Converter);";
                case "float3": return $"{Name} = JsonConvert.DeserializeObject<float3>(array.Value<string>({index}), float3Converter.Converter);";

                default: return $"{Name} = array.Value<{Type}>({index});";
            }
        }

        private string GetEqualityComparisonImpl(int indent, string otherVariableName) {
            if (CustomEquality) {
                return ExpandPlaceholders(EqualityCode, new ExpandVariables{Indent = Indent(indent), Other = otherVariableName}, "indent", "other");
            }

            // Check if Type (TypeSyntax) is an enum
            if (IsEnum(Type, owner.ClassName)) {
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
                equality = $"\n{Indent(indentation + 1)}if({GetEqualityComparisonImpl(indentation + 1, "newValue")}) return;";
            }

            return string.Format(Templates.UpdateFromEditorNodeTemplate, Indent(indentation), UppercaseName, Type, equality, Name, $"\n{calculate}",
                                 notify.TrimEnd());
        }

        private string GetGetValueForPortCodeImpl(int indentation) {
            return $"{Indent(indentation + 1)}if (port == {PortName}) return {Name};";
        }

        private string GetOnPortValueChangedCodeImpl(int indentation, string calculate, string notify) {
            const string otherVariableName = "newValue";
            string equality = "";
            if (GenerateEquality) {
                equality = $"\n{Indent(indentation + 2)}if({GetEqualityComparisonImpl(indentation + 2, otherVariableName)}) return;";
            }

            string extraCodeBeforeGetValue = string.Join("\n", AdditionalValueChangedCode_BeforeGetValue.Select(code => {
                code = UnescapeString(code);
                code = ExpandPlaceholders(code, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                code = AddSemicolonIfNeeded(code);
                return $"{Indent(indentation + 2)}{code}";
            }));
            if (!string.IsNullOrEmpty(extraCodeBeforeGetValue)) extraCodeBeforeGetValue = $"\n{extraCodeBeforeGetValue}";
            string extraCodeAfterGetValue = string.Join("\n", AdditionalValueChangedCode_AfterGetValue.Select(code => {
                code = UnescapeString(code);
                code = ExpandPlaceholders(code, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                code = AddSemicolonIfNeeded(code);
                return $"{Indent(indentation + 2)}{code}";
            }));
            if (!string.IsNullOrEmpty(extraCodeAfterGetValue)) extraCodeAfterGetValue = $"\n{extraCodeAfterGetValue}";
            string extraCodeAfterEqualityCheck = string.Join("\n", AdditionalValueChangedCode_AfterEqualityCheck.Select(code => {
                code = UnescapeString(code);
                code = ExpandPlaceholders(code, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                code = AddSemicolonIfNeeded(code);
                return $"{Indent(indentation + 2)}{code}";
            }));
            if (!string.IsNullOrEmpty(extraCodeAfterEqualityCheck)) extraCodeAfterEqualityCheck = $"\n{extraCodeAfterEqualityCheck}";
            string extraCodeAfterUpdate = string.Join("\n", AdditionalValueChangedCode_AfterUpdate.Select(code => {
                code = UnescapeString(code);
                code = ExpandPlaceholders(code, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                code = AddSemicolonIfNeeded(code);
                return $"{Indent(indentation + 2)}{code}";
            }));
            if (!string.IsNullOrEmpty(extraCodeAfterUpdate)) extraCodeAfterUpdate = $"\n{extraCodeAfterUpdate}";
            string extraCodeAfterCalculate = string.Join("\n", AdditionalValueChangedCode_AfterCalculate.Select(code => {
                code = UnescapeString(code);
                code = ExpandPlaceholders(code, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                code = AddSemicolonIfNeeded(code);
                return $"{Indent(indentation + 2)}{code}";
            }));
            if (!string.IsNullOrEmpty(extraCodeAfterCalculate)) extraCodeAfterCalculate = $"\n{extraCodeAfterCalculate}";
            string extraCodeAfterNotify = string.Join("\n", AdditionalValueChangedCode_AfterNotify.Select(code => {
                code = UnescapeString(code);
                code = ExpandPlaceholders(code, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                code = AddSemicolonIfNeeded(code);
                return $"{Indent(indentation + 2)}{code}";
            }));
            if (!string.IsNullOrEmpty(extraCodeAfterNotify)) extraCodeAfterNotify = $"\n{extraCodeAfterNotify}";

            calculate = calculate.TrimEnd();
            if (!string.IsNullOrEmpty(calculate)) calculate = $"\n{calculate}";
            if (!CallCalculateMethodsIfChanged) calculate = ""; 
            
            notify = notify.TrimEnd();
            if (!string.IsNullOrEmpty(notify)) notify = $"\n{notify}";
            if (!CallNotifyMethodsIfChanged) notify = ""; 

            string updateValueCode = UpdateValueCode.Trim();
            if (!string.IsNullOrEmpty(updateValueCode)) {
                updateValueCode = UnescapeString(updateValueCode);
                updateValueCode = ExpandPlaceholders(updateValueCode, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                updateValueCode = $"\n{Indent(indentation + 2)}{updateValueCode}";
                updateValueCode = AddSemicolonIfNeeded(updateValueCode);
            }
            
            string getValueCode = GetValueCode.Trim();
            if (!string.IsNullOrEmpty(getValueCode)) {
                getValueCode = UnescapeString(getValueCode);
                getValueCode = ExpandPlaceholders(getValueCode, new ExpandVariables{Indent = Indent(indentation + 2), Other = otherVariableName}, "indent", "other");
                getValueCode = $"\n{Indent(indentation + 2)}{getValueCode}";
                getValueCode = AddSemicolonIfNeeded(getValueCode);
            }

            return string.Format(Templates.OnPortValueChangedIfTemplate, Indent(indentation + 1), PortName, updateValueCode, equality, calculate,
                                 notify, extraCodeBeforeGetValue, extraCodeAfterGetValue, extraCodeAfterEqualityCheck, extraCodeAfterUpdate, 
                                 extraCodeAfterCalculate, extraCodeAfterNotify, getValueCode);
        }
        
        #endregion

        #region Checks

        private bool CanGenerateEquality() {
            if (CustomEquality) return true;
            if (Kind == GeneratedPropertyKind.OutputPort) return false;

            if (IsEnum(Type, owner.ClassName)) {
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

            if (IsEnum(Type, owner.ClassName)) {
                return true;
            }

            switch (Type) {
                case "GeometryData":
                case "List<GeometryData>":
                case "CurveData":
                    return false;

                default: return true;
            }
        }

        private bool CanGenerateSerialization() {
            if (CustomSerialization) return true;
            if (Kind == GeneratedPropertyKind.OutputPort) return false;

            if (IsEnum(Type, owner.ClassName)) {
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
        
        #region Helpers

        private string ExpandPlaceholders(string text, ExpandVariables variables, params string[] placeholders) {
            if (placeholders.Length == 0) return text;
            foreach (string placeholder in placeholders) {
                text = placeholder switch {
                    "self" => text.Replace("{self}", Name),
                    "Self" => text.Replace("{Self}", UppercaseName),
                    "portName" => text.Replace("{portName}", PortName),
                    "other" => text.Replace("{other}", variables.Other),
                    "default" => text.Replace("{default}", DefaultValue),
                    "indent" => text.Replace("{indent}", variables.Indent),
                    "type" => text.Replace("{type}", Type),
                    "storage" => text.Replace("{storage}", variables.Storage),
                    "index" => text.Replace("{index}", variables.Index.ToString()),
                    _ => text
                };
            }
            return text;
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

    internal class ExpandVariables {
        public string Other { get; set; }
        public string Storage { get; set; }
        public string Indent { get; set; }
        public int Index { get; set; }
    }
}