using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public class GeneratedClass {
        private ClassDeclarationSyntax classDeclarationSyntax;

        public string ClassName { get; }
        public string NamespaceName { get; }
        public string FilePath { get; }
        public string OutputRelativePath { get; }

        public List<GeneratedProperty> Properties { get; }
        
        public Dictionary<string, HashSet<string>> UpdateMethods { get; }
        public HashSet<string> UpdateAllMethods { get; }
        public Dictionary<string, GetterMethod> GetterMethods { get; }

        public GeneratedClass(ClassDeclarationSyntax classDeclarationSyntax) {
            this.classDeclarationSyntax = classDeclarationSyntax;

            ClassName = classDeclarationSyntax.Identifier.Text;
            NamespaceName = classDeclarationSyntax.Parent.ToString().Split(' ')[1];
            FilePath = classDeclarationSyntax.SyntaxTree.FilePath;

            Properties = new List<GeneratedProperty>();
            UpdateMethods = new Dictionary<string, HashSet<string>>();
            UpdateAllMethods = new HashSet<string>();
            GetterMethods = new Dictionary<string, GetterMethod>();
            
            // Get relative path from [GenerateRuntimeNode] attribute
            OutputRelativePath = "";
            foreach (AttributeSyntax attribute in classDeclarationSyntax.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.Name.ToString() != "GenerateRuntimeNode" || attribute.ArgumentList == null) continue;
                
                string argName = attribute.ArgumentList.Arguments[0].NameEquals.Name.Identifier.Text;
                string argValue = GeneratorUtils.ExtractStringFromExpression(attribute.ArgumentList.Arguments[0].Expression);
                if (argName == "OutputPath") {
                    OutputRelativePath = argValue;
                }
            }

            // Loops through all the properties in the class, looking for
            // attributes that indicate that they should be generated. ([In], [Out], [Setting])
            // It creates a GeneratedProperty object for each property, which then parses more
            // information about the property and alters the generated code.
            CollectProperties();
            
            // Loops through all the methods in the class, looking for
            // attributes that alter how the method-relevant code is generated.
            // The attributes it currently looks for are: CalculatesAllProperties, CalculatesProperty, and, GetterMethod.
            CollectMethods();
        }

        public string GetCode() {
            string usingStatements = GetUsingStatements().TrimEnd();
            string portDeclarations = GetPortDeclarationsCode().TrimEnd();
            string portInitializers = GetPortInitializersCode().TrimEnd();
            string updateFromEditorNodeMethods = GetUpdateFromEditorNodeCode().TrimEnd();
            string serialization = GetSerializationCode().TrimEnd();
            string deserialization = GetDeserializationCode().TrimEnd();
            string postDeserialization = GetPostDeserializationCode().TrimEnd();
            string getValueForPort = GetGetValueForPortCode().TrimEnd();
            string onPortValueChanged = GetOnPortValueChangedCode().TrimEnd();
            // string debugInfo = $"\n\n/*\n{GetDebugInfoCode()}*/";
            string debugInfo = "";

            return string.Format(Templates.ClassTemplate, usingStatements, NamespaceName, ClassName, portDeclarations, portInitializers, serialization, deserialization,
                                 postDeserialization, updateFromEditorNodeMethods, getValueForPort, onPortValueChanged, debugInfo);
        }

        #region Code Generation

        private string GetUsingStatements() {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("using System;");
            stringBuilder.AppendLine("using Newtonsoft.Json;");
            stringBuilder.AppendLine("using Newtonsoft.Json.Linq;");
            
            foreach (AttributeSyntax attribute in classDeclarationSyntax.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.Name.ToString() == "AdditionalUsingStatements") {
                    if (attribute.ArgumentList == null) {
                        continue;
                    }

                    foreach (var arg in attribute.ArgumentList.Arguments) {
                        var @namespace = GeneratorUtils.ExtractStringFromExpression(arg.Expression);
                        stringBuilder.AppendLine($"using {@namespace};");
                    }
                }
            }

            if (Properties.Any(property => property.Type is "float2" or "float3")) {
                stringBuilder.AppendLine("using GeometryGraph.Runtime.Serialization;");
            }

            return stringBuilder.ToString();
        }

        private string GetPortDeclarationsCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind != GeneratedPropertyKind.Setting)) {
                stringBuilder.AppendLine(property.GetPortPropertyDeclaration());
            }

            return stringBuilder.ToString();
        }

        private string GetPortInitializersCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind != GeneratedPropertyKind.Setting)) {
                stringBuilder.AppendLine(property.GetPortCtorDeclaration());
            }

            return stringBuilder.ToString();
        }

        private string GetUpdateFromEditorNodeCode() {
            var stringBuilder = new StringBuilder();

            foreach (GeneratedProperty property in Properties.Where(property => property.GenerateUpdateFromEditorMethod)) {
                var propertyCalculateMethods = GetCalculateMethodsFor(property, 3);
                var propertyNotifyMethods = GetNotifyMethodsFor(property, 3);
                stringBuilder.AppendLine($"{property.GetUpdateFromEditorNodeMethod(propertyCalculateMethods, propertyNotifyMethods)}\n");
            }

            return stringBuilder.ToString();
        }

        private string GetSerializationCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.GenerateSerialization)) {
                stringBuilder.AppendLine(property.GetSerializationCode());
            }

            return stringBuilder.ToString();
        }

        private string GetDeserializationCode() {
            var stringBuilder = new StringBuilder();
            int serializationIndex = 0;
            foreach (GeneratedProperty property in Properties.Where(property => property.GenerateSerialization)) {
                stringBuilder.AppendLine(property.GetDeserializationCode(serializationIndex));
                serializationIndex++;
            }

            return stringBuilder.ToString();
        }

        private string GetPostDeserializationCode() {
            var stringBuilder = new StringBuilder();
            var addedMethods = new HashSet<string>();

            // Add UpdateMethods
            foreach (string method in UpdateMethods.Where(pair => pair.Key != "").SelectMany(pair => pair.Value)) {
                if (addedMethods.Contains(method)) continue;

                stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}{method}();");
                addedMethods.Add(method);
            }

            // Add UpdateAllMethods
            foreach (string method in UpdateAllMethods) {
                if (addedMethods.Contains(method)) continue;

                stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}{method}();");
                addedMethods.Add(method);
            }

            // Notify port value changed
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind == GeneratedPropertyKind.OutputPort)) {
                stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}NotifyPortValueChanged({property.CapitalizedName}Port);");
            }

            return stringBuilder.ToString();
        }

        private string GetGetValueForPortCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind == GeneratedPropertyKind.OutputPort)) {
                if (GetterMethods.ContainsKey(property.Name)) {
                    var getterMethod = GetterMethods[property.Name];
                    stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}if (port == {property.CapitalizedName}Port) {(!getterMethod.Inline ? $"return {getterMethod.MethodName}();": getterMethod.HasExpressionBody ? $"return {getterMethod.Body};" : GeneratorUtils.Indent(getterMethod.Body, 3).TrimStart())}");
                } else if (property.CustomGetter) {
                    stringBuilder.AppendLine(
                        $"{GeneratorUtils.Indent(3)}if (port == {property.CapitalizedName}Port) {{\n{GeneratorUtils.Indent(4)}{property.GetterCode}\n{GeneratorUtils.Indent(3)}}}");
                } else {
                    stringBuilder.AppendLine(property.GetGetValueForPortCode());
                }
            }

            return stringBuilder.ToString();
        }

        private string GetOnPortValueChangedCode() {
            var stringBuilder = new StringBuilder();

            // Add if (port == outputPortA || ...) return;
            if (Properties.Any(property => property.Kind == GeneratedPropertyKind.OutputPort)) {
                stringBuilder.AppendLine(
                    $"{GeneratorUtils.Indent(3)}if ({string.Join(" || ", Properties.Where(property => property.Kind == GeneratedPropertyKind.OutputPort).Select(property => $"port == {property.CapitalizedName}Port"))}) return;");
            }

            // Add individual ifs joined by ` else `
            if (Properties.Any(property => property.Kind == GeneratedPropertyKind.InputPort)) {
                string notify = string.Join(" else ", Properties.Where(property => property.Kind == GeneratedPropertyKind.InputPort).Select(property => {
                    string propertyCalculateMethods = GetCalculateMethodsFor(property, 4);
                    string propertyNotifyMethods = GetNotifyMethodsFor(property, 4);
                    return property.GetOnPortValueChangedCode(propertyCalculateMethods, propertyNotifyMethods);
                }));
                stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}{notify}");
            }

            return stringBuilder.ToString();
        }

        private string GetDebugInfoCode() {
            var stringBuilder = new StringBuilder();

            foreach (GeneratedProperty property in Properties) {
                stringBuilder.AppendLine(property.GetDebugCode());
            }

            foreach (KeyValuePair<string, HashSet<string>> pair in UpdateMethods) {
                if (pair.Key == "") {
                    stringBuilder.AppendLine($"Methods `{string.Join(", ", pair.Value)}` update unknown property");
                } else {
                    stringBuilder.AppendLine($"Methods `{string.Join(", ", pair.Value)}` update property `{pair.Key}`");
                }
            }

            foreach (string method in UpdateAllMethods) {
                stringBuilder.AppendLine($"Method `{method}` updates all [Out] properties");
            }

            foreach (KeyValuePair<string, GetterMethod> pair in GetterMethods) {
                stringBuilder.AppendLine($"Method `{pair.Value.MethodName}` returns value of property `{pair.Key}`\nInline: {pair.Value.Inline}\nSource code:\n{pair.Value.Body}");
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region Information Collection

        private void CollectProperties() {
            List<PropertyDeclarationSyntax> propertyDeclarations = classDeclarationSyntax.Members.OfType<PropertyDeclarationSyntax>().ToList();

            // [In] properties
            Properties.AddRange(
                propertyDeclarations
                    .Where(property => property.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "In"))
                    .Select(property => new GeneratedProperty(property, GeneratedPropertyKind.InputPort))
            );

            // [Out] properties
            Properties.AddRange(
                propertyDeclarations
                    .Where(property => property.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "Out"))
                    .Select(property => new GeneratedProperty(property, GeneratedPropertyKind.OutputPort))
            );

            // [Setting] properties
            Properties.AddRange(
                propertyDeclarations
                    .Where(property => property.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "Setting"))
                    .Select(property => new GeneratedProperty(property, GeneratedPropertyKind.Setting))
            );
        }

        private void CollectMethods() {
            List<MethodDeclarationSyntax> methods = classDeclarationSyntax.Members.OfType<MethodDeclarationSyntax>().ToList();
            foreach (MethodDeclarationSyntax method in methods) {
                foreach (AttributeSyntax attribute in method.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                    string attributeName = attribute.Name.ToString();

                    if (attributeName == "CalculatesAllProperties" || attributeName == "CalculatesProperty" && attribute.ArgumentList == null) {
                        UpdateAllMethods.Add(method.Identifier.Text);
                        continue;
                    }

                    if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0) continue;
                    SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;

                    switch (attributeName) {
                        case "CalculatesProperty": {
                            string variableName = GeneratorUtils.ExtractNameFromExpression(arguments[0].Expression);
                            if (string.IsNullOrEmpty(variableName)) continue;

                            if (!UpdateMethods.ContainsKey(variableName)) {
                                UpdateMethods.Add(variableName, new HashSet<string>());
                            }

                            UpdateMethods[variableName].Add(method.Identifier.Text);
                            break;
                        }
                        case "GetterMethod": {
                            string propertyName = GeneratorUtils.ExtractNameFromExpression(arguments[0].Expression);
                            if (string.IsNullOrEmpty(propertyName)) {
                                continue;
                            }
                            
                            bool inline = false;
                            if (arguments.Count > 1) {
                                NameEqualsSyntax nameEqualsSyntax = arguments[1].NameEquals;
                                if (nameEqualsSyntax != null && nameEqualsSyntax.Name.Identifier.Text == "Inline") {
                                    inline = arguments[1].Expression.ToString() == "true";
                                }
                            }
                            
                            string body = GeneratorUtils.InlineMethod(method);
                            GetterMethods[propertyName] = new GetterMethod(method.Identifier.Text, inline, body, method.ExpressionBody != null);

                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Utility methods

        private string GetCalculateMethodsFor(GeneratedProperty property, int indent) {
            string indentString = GeneratorUtils.Indent(indent);
            var stringBuilder = new StringBuilder();

            foreach (string method in UpdateAllMethods) {
                stringBuilder.AppendLine($"{indentString}{method}();");
            }

            if (property.UpdatesAllProperties) {
                foreach (KeyValuePair<string, HashSet<string>> pair in UpdateMethods) {
                    if (pair.Key == "") continue;
                    foreach (string method in pair.Value) {
                        stringBuilder.AppendLine($"{indentString}{method}();");
                    }
                }

                return stringBuilder.ToString();
            }

            foreach (string updatedProperty in property.UpdatesProperties) {
                if (!UpdateMethods.ContainsKey(updatedProperty)) continue;

                foreach (string method in UpdateMethods[updatedProperty]) {
                    stringBuilder.AppendLine($"{indentString}{method}();");
                }
            }

            return stringBuilder.ToString();
        }

        private string GetNotifyMethodsFor(GeneratedProperty property, int indent) {
            string indentString = GeneratorUtils.Indent(indent);
            var stringBuilder = new StringBuilder();

            foreach (GeneratedProperty notifiedProperty in Properties.Where(otherProperty => otherProperty.Kind == GeneratedPropertyKind.OutputPort)) {
                if (property.UpdatesAllProperties || property.UpdatesProperties.Contains(notifiedProperty.Name)) {
                    stringBuilder.AppendLine($"{indentString}NotifyPortValueChanged({notifiedProperty.CapitalizedName}Port);");
                }
            }

            return stringBuilder.ToString();
        }

        #endregion
    }
}