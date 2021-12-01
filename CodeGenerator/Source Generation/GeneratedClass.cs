using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static SourceGenerator.GeneratorUtils;

namespace SourceGenerator {
    public class GeneratedClass {
        public ClassDeclarationSyntax ClassDeclarationSyntax { get; }

        public string ClassName { get; }
        public string NamespaceName { get; }
        public string FilePath { get; }
        public string AssemblyName { get; set; }
        public SortedSet<string> Usings { get; }
        public SortedSet<(string Alias, string AliasOf)> AliasUsings { get; }

        public string OutputRelativePath { get; private set; }
        public bool GenerateSerialization { get; private set; }
        public bool CalculateDuringDeserialization { get; private set; }

        public List<GeneratedProperty> Properties { get; }
        
        public Dictionary<string, HashSet<string>> UpdateMethods { get; }
        public HashSet<string> UpdateAllMethods { get; }
        public Dictionary<string, GetterMethod> GetterMethods { get; }

        private readonly int indentation = 1;

        public GeneratedClass(ClassDeclarationSyntax classDeclarationSyntax) {
            ClassDeclarationSyntax = classDeclarationSyntax;

            ClassName = classDeclarationSyntax.Identifier.Text;
            FilePath = classDeclarationSyntax.SyntaxTree.FilePath;
            
            Usings = new SortedSet<string>(GeneratorContext.GlobalSettings.AdditionalUsingStatements.Select(CleanupUsingStatement));
            AliasUsings = new SortedSet<(string Alias, string AliasOf)>(Comparer<(string Alias, string AliasOf)>.Create((a, b) => {
                int compareAlias = string.Compare(a.Alias, b.Alias, StringComparison.InvariantCulture);
                return compareAlias != 0 ? compareAlias : string.Compare(a.AliasOf, b.AliasOf, StringComparison.InvariantCulture);
            }));

            if (classDeclarationSyntax.Parent is NamespaceDeclarationSyntax) {
                NamespaceName = classDeclarationSyntax.Parent.ToString().Split(' ')[1];
                indentation = 2;
            }
            
            Properties = new List<GeneratedProperty>();
            UpdateMethods = new Dictionary<string, HashSet<string>>();
            UpdateAllMethods = new HashSet<string>();
            GetterMethods = new Dictionary<string, GetterMethod>();

            // Loops through all the properties in the class, looking for
            // attributes that indicate that they should be generated. ([In], [Out], [Setting])
            // It creates a GeneratedProperty object for each property, which then parses more
            // information about the property and alters the generated code.
            CollectProperties();
            
            // Loops through all the methods in the class, looking for
            // attributes that alter how the method-relevant code is generated.
            // The attributes it currently looks for are: CalculatesAllProperties, CalculatesProperty, and, GetterMethod.
            CollectMethods();
            
            // Gather information about the class, such as using statements
            CollectUsings();
            
            OutputRelativePath = GeneratorContext.GlobalSettings.OutputRelativePath;
            GenerateSerialization = GeneratorContext.GlobalSettings.GenerateSerialization;
            CalculateDuringDeserialization = GeneratorContext.GlobalSettings.CalculateDuringDeserialization;
            CollectGeneratorSettings();
        }

        public string GetCode() {
            string usingStatements = GetUsingStatements().TrimEnd();
            string portDeclarations = GetPortDeclarationsCode().TrimEnd();
            string portInitializers = GetPortInitializersCode().TrimEnd();
            string updateFromEditorNodeMethods = GetUpdateFromEditorNodeCode().TrimEnd();
            string serialization = GetSerializationCode().TrimEnd();
            string deserialization = GetDeserializationCode().TrimEnd();
            string afterDeserialization = GetAfterDeserializationCode().TrimEnd();
            string getValueForPort = GetGetValueForPortCode().TrimEnd();
            string onPortValueChanged = GetOnPortValueChangedCode().TrimEnd();
            
            if (string.IsNullOrEmpty(NamespaceName)) {
                return string.Format(Templates.ClassTemplateNoNamespace, usingStatements, ClassName, portDeclarations, portInitializers, serialization, 
                                     deserialization, updateFromEditorNodeMethods, getValueForPort, onPortValueChanged, afterDeserialization);
            }
            return string.Format(Templates.ClassTemplate, usingStatements, NamespaceName, ClassName, portDeclarations, portInitializers, serialization, 
                                 deserialization, updateFromEditorNodeMethods, getValueForPort, onPortValueChanged, afterDeserialization);
        }

        #region Code Generation

        private string GetUsingStatements() {
            string usingStatements = string.Join("\n", Usings.Select(@using => $"using {@using};"));
            string aliasUsingStatements = string.Join("\n", AliasUsings.Select(@using => $"using {@using.Alias} = {@using.AliasOf};"));
            if (!string.IsNullOrEmpty(usingStatements) && !string.IsNullOrEmpty(aliasUsingStatements)) {
                aliasUsingStatements = $"\n{aliasUsingStatements}";
            }
            return string.Join("\n", usingStatements, aliasUsingStatements);
        }

        private string GetPortDeclarationsCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind != GeneratedPropertyKind.Setting)) {
                stringBuilder.AppendLine(property.GetPortPropertyDeclaration(indentation));
            }

            return stringBuilder.ToString();
        }

        private string GetPortInitializersCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind != GeneratedPropertyKind.Setting)) {
                stringBuilder.AppendLine(property.GetPortCtorDeclaration(indentation));
            }

            return stringBuilder.ToString();
        }

        private string GetUpdateFromEditorNodeCode() {
            var stringBuilder = new StringBuilder();

            foreach (GeneratedProperty property in Properties.Where(property => property.GenerateUpdateFromEditorMethod)) {
                var propertyCalculateMethods = GetCalculateMethodsFor(property, indentation + 1);
                var propertyNotifyMethods = GetNotifyMethodsFor(property, indentation + 1);
                stringBuilder.AppendLine($"{property.GetUpdateFromEditorNodeMethod(indentation, propertyCalculateMethods, propertyNotifyMethods)}\n");
            }

            string code = stringBuilder.ToString();
            if (string.IsNullOrEmpty(code)) return "";

            return $"\n\n{code}";
        }

        private string GetSerializationCode() {
            if (!GenerateSerialization) return "";
            
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.GenerateSerialization)) {
                stringBuilder.AppendLine(property.GetSerializationCode(indentation));
            }
            
            string code = stringBuilder.ToString().TrimEnd();
            if (string.IsNullOrEmpty(code)) return "";

            return $"\n\n{string.Format(Templates.SerializationTemplate, Indent(indentation), code)}";
        }

        private string GetDeserializationCode() {
            if (!GenerateSerialization) return "";

            var stringBuilder = new StringBuilder();
            int serializationIndex = 0;
            foreach (GeneratedProperty property in Properties.Where(property => property.GenerateSerialization)) {
                stringBuilder.AppendLine(property.GetDeserializationCode(indentation, serializationIndex));
                serializationIndex++;
            }
            
            string code = stringBuilder.ToString().TrimEnd();
            if (string.IsNullOrEmpty(code)) return "";

            string deserializationLoadCode = $"\n{string.Format(Templates.DeserializationLoadTemplate, Indent(indentation), code)}";
            return $"\n\n{string.Format(Templates.DeserializationTemplate, Indent(indentation), deserializationLoadCode)}";
        }

        private string GetAfterDeserializationCode() {
            if (!GenerateSerialization) return "";
            
            string postDeserializationCode = GetPostDeserializationCode().TrimEnd();
            if (string.IsNullOrEmpty(postDeserializationCode)) return "";
            postDeserializationCode = $"\n{postDeserializationCode}";
            return $"\n\n{string.Format(Templates.OnAfterDeserializeTemplate, Indent(indentation), postDeserializationCode)}";
        }

        private string GetPostDeserializationCode() {
            var stringBuilder = new StringBuilder();
            var addedMethods = new HashSet<string>();

            if (CalculateDuringDeserialization) {
                // Add UpdateMethods
                foreach (string method in UpdateMethods.Where(pair => pair.Key != "").SelectMany(pair => pair.Value)) {
                    if (addedMethods.Contains(method)) continue;

                    stringBuilder.AppendLine($"{Indent(indentation + 1)}{method}();");
                    addedMethods.Add(method);
                }

                // Add UpdateAllMethods
                foreach (string method in UpdateAllMethods) {
                    if (addedMethods.Contains(method)) continue;

                    stringBuilder.AppendLine($"{Indent(indentation + 1)}{method}();");
                    addedMethods.Add(method);
                }
            }

            // Notify port value changed
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind == GeneratedPropertyKind.OutputPort)) {
                stringBuilder.AppendLine($"{Indent(indentation + 1)}NotifyPortValueChanged({property.PortName});");
            }

            return stringBuilder.ToString();
        }

        private string GetGetValueForPortCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedProperty property in Properties.Where(property => property.Kind == GeneratedPropertyKind.OutputPort)) {
                if (GetterMethods.ContainsKey(property.Name)) {
                    var getterMethod = GetterMethods[property.Name];
                    stringBuilder.AppendLine($"{Indent(indentation + 1)}if (port == {property.PortName}) {(!getterMethod.Inline ? $"return {getterMethod.MethodName}();": getterMethod.HasExpressionBody ? $"return {getterMethod.Body};" : Indent(getterMethod.Body, indentation + 1).TrimStart())}");
                } else if (property.CustomGetter) {
                    stringBuilder.AppendLine($"{Indent(indentation + 1)}if (port == {property.PortName}) {{\n{Indent(indentation + 2)}{property.GetterCode.Replace("{indent}", Indent(indentation + 2))}\n{Indent(indentation + 1)}}}");
                } else {
                    stringBuilder.AppendLine(property.GetGetValueForPortCode(indentation));
                }
            }

            return stringBuilder.ToString();
        }

        private string GetOnPortValueChangedCode() {
            var stringBuilder = new StringBuilder();

            // Add if (port == outputPortA || ...) return;
            if (Properties.Any(property => property.Kind == GeneratedPropertyKind.OutputPort)) {
                stringBuilder.AppendLine($"{Indent(indentation + 1)}if ({string.Join(" || ", Properties.Where(property => property.Kind == GeneratedPropertyKind.OutputPort).Select(property => $"port == {property.PortName}"))}) return;");
            }

            // Add individual ifs joined by ` else `
            if (Properties.Any(property => property.Kind == GeneratedPropertyKind.InputPort)) {
                string notify = string.Join(" else ", Properties.Where(property => property.Kind == GeneratedPropertyKind.InputPort).Select(property => {
                    string propertyCalculateMethods = GetCalculateMethodsFor(property, indentation + 2);
                    string propertyNotifyMethods = GetNotifyMethodsFor(property, indentation + 2);
                    return property.GetOnPortValueChangedCode(indentation, propertyCalculateMethods, propertyNotifyMethods);
                }));
                stringBuilder.AppendLine($"{Indent(indentation + 1)}{notify}");
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
            List<PropertyDeclarationSyntax> propertyDeclarations = ClassDeclarationSyntax.Members.OfType<PropertyDeclarationSyntax>().ToList();

            // [In] properties
            Properties.AddRange(
                propertyDeclarations
                    .Where(property => property.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "In"))
                    .Select(property => new GeneratedProperty(this, property, GeneratedPropertyKind.InputPort))
            );

            // [Out] properties
            Properties.AddRange(
                propertyDeclarations
                    .Where(property => property.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "Out"))
                    .Select(property => new GeneratedProperty(this, property, GeneratedPropertyKind.OutputPort))
            );

            // [Setting] properties
            Properties.AddRange(
                propertyDeclarations
                    .Where(property => property.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "Setting"))
                    .Select(property => new GeneratedProperty(this, property, GeneratedPropertyKind.Setting))
            );
        }

        private void CollectMethods() {
            List<MethodDeclarationSyntax> methods = ClassDeclarationSyntax.Members.OfType<MethodDeclarationSyntax>().ToList();
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
                            string variableName = ExtractNameFromExpression(arguments[0].Expression);
                            if (string.IsNullOrEmpty(variableName)) continue;

                            if (!UpdateMethods.ContainsKey(variableName)) {
                                UpdateMethods.Add(variableName, new HashSet<string>());
                            }

                            UpdateMethods[variableName].Add(method.Identifier.Text);
                            break;
                        }
                        case "GetterMethod": {
                            string propertyName = ExtractNameFromExpression(arguments[0].Expression);
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
                            
                            string body = InlineMethod(method);
                            GetterMethods[propertyName] = new GetterMethod(method.Identifier.Text, inline, body, method.ExpressionBody != null);

                            break;
                        }
                    }
                }
            }
        }

        private void CollectUsings() {
            var compilationUnit = ClassDeclarationSyntax.FirstAncestorOrSelf<SyntaxNode>(node => node is CompilationUnitSyntax);
            if (compilationUnit is CompilationUnitSyntax compilation) {
                foreach (UsingDirectiveSyntax usingDirective in compilation.Usings) {
                    if (usingDirective.Alias != null) {
                        AliasUsings.Add((usingDirective.Alias.Name.ToString(), usingDirective.Name.ToString()));
                    } else {
                        Usings.Add(usingDirective.Name.ToString());
                    }
                }
            }

            if (Properties.Any(property => property.Type is "float2" or "float3")) {
                Usings.Add("GeometryGraph.Runtime.Serialization");
            }
            
            if (Properties.Any(property => property.Type is "float" or "string")) {
                Usings.Add("System");
            }
            
            if (Properties.Any(property => property.GenerateSerialization)) {
                Usings.Add("Newtonsoft.Json");
                Usings.Add("Newtonsoft.Json.Linq");
            }

            foreach (AttributeSyntax attribute in ClassDeclarationSyntax.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.Name.ToString() != "AdditionalUsingStatements" || attribute.ArgumentList == null) continue;

                foreach (string @namespace in attribute.ArgumentList.Arguments.Select(arg => ExtractStringFromExpression(arg.Expression))) {
                    Usings.Add(@namespace);
                }
            }
            
            Usings.Remove("JetBrains.Annotations");
        }

        private void CollectGeneratorSettings() {
            foreach (AttributeSyntax attribute in ClassDeclarationSyntax.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attribute.Name.ToString() != "GeneratorSettings" || attribute.ArgumentList == null) continue;

                foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments) {
                    if (argument.NameEquals == null) continue;
                    string argName = argument.NameEquals.Name.Identifier.Text;
                    switch (argName) {
                        case "OutputRelativePath": {
                            string argValue = ExtractStringFromExpression(argument.Expression);
                            OutputRelativePath = argValue;
                            break;
                        }
                        case "GenerateSerialization": {
                            string argValue = argument.Expression.ToString();
                            if (argValue == "true") continue;
                            GenerateSerialization = false;
                            break;
                        }
                        case "CalculateDuringDeserialization": {
                            string argValue = argument.Expression.ToString();
                            if (argValue == "true") continue;
                            CalculateDuringDeserialization = false;
                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Utility methods

        private string GetCalculateMethodsFor(GeneratedProperty property, int indent) {
            string indentString = Indent(indent);
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
            string indentString = Indent(indent);
            var stringBuilder = new StringBuilder();

            foreach (GeneratedProperty notifiedProperty in Properties.Where(otherProperty => otherProperty.Kind == GeneratedPropertyKind.OutputPort)) {
                if (property.UpdatesAllProperties || property.UpdatesProperties.Contains(notifiedProperty.Name)) {
                    stringBuilder.AppendLine($"{indentString}NotifyPortValueChanged({notifiedProperty.PortName});");
                }
            }

            return stringBuilder.ToString();
        }

        #endregion
    }
}