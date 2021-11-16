using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public class GeneratedClass {
        private ClassDeclarationSyntax classDeclarationSyntax;

        public string ClassName { get; }
        public string NamespaceName { get; }

        public List<GeneratedField> Fields { get; }
        public Dictionary<string, HashSet<string>> UpdateMethods { get; }
        public HashSet<string> UpdateAllMethods { get; }
        public Dictionary<string, string> GetterMethods { get; }

        public GeneratedClass(ClassDeclarationSyntax classDeclarationSyntax) {
            this.classDeclarationSyntax = classDeclarationSyntax;

            NamespaceName = classDeclarationSyntax.Parent.ToString().Split(' ')[1];
            ClassName = classDeclarationSyntax.Identifier.Text;

            Fields = new List<GeneratedField>();
            UpdateMethods = new Dictionary<string, HashSet<string>>();
            UpdateAllMethods = new HashSet<string>();
            GetterMethods = new Dictionary<string, string>();

            CollectFields();
            CollectMethods();
        }

        public string GetCode() {
            const string usingStatements = @"using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;";
            string portDeclarations = GetPortDeclarationsCode();
            string portInitializers = GetPortInitializersCode();
            string updateFromEditorNodeMethods = GetUpdateFromEditorNodeCode();
            string serialization = GetSerializationCode();
            string deserialization = GetDeserializationCode();
            string postDeserialization = GetPostDeserializationCode();
            string getValueForPort = GetGetValueForPortCode();
            string onPortValueChanged = GetOnPortValueChangedCode();
            string debugInfo = GetDebugInfoCode();

            return string.Format(Templates.ClassTemplate, usingStatements, NamespaceName, ClassName, portDeclarations, portInitializers.TrimEnd(),
                                 serialization.TrimEnd(), deserialization.TrimEnd(), postDeserialization.TrimEnd(),
                                 updateFromEditorNodeMethods.TrimEnd(), getValueForPort.TrimEnd(),
                                 onPortValueChanged.TrimEnd(), debugInfo.TrimEnd());
        }

        #region Code Generation

        private string GetPortDeclarationsCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedField field in Fields.Where(field => field.Kind != GeneratedFieldKind.Setting)) {
                stringBuilder.AppendLine(field.GetPortPropertyDeclaration());
            }

            return stringBuilder.ToString();
        }

        private string GetPortInitializersCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedField field in Fields.Where(field => field.Kind != GeneratedFieldKind.Setting)) {
                stringBuilder.AppendLine(field.GetPortCtorDeclaration());
            }

            return stringBuilder.ToString();
        }

        private string GetUpdateFromEditorNodeCode() {
            var stringBuilder = new StringBuilder();

            foreach (GeneratedField field in Fields.Where(field => field.GenerateUpdateFromEditorMethod)) {
                var fieldCalculateMethods = GetCalculateMethodsFor(field, 3);
                var fieldNotifyMethods = GetNotifyMethodsFor(field, 3);
                stringBuilder.AppendLine($"{field.GetUpdateFromEditorNodeMethod(fieldCalculateMethods, fieldNotifyMethods)}\n");
            }

            return stringBuilder.ToString();
        }

        private string GetSerializationCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedField field in Fields.Where(field => field.GenerateSerialization)) {
                stringBuilder.AppendLine(field.GetSerializationCode());
            }

            return stringBuilder.ToString();
        }

        private string GetDeserializationCode() {
            var stringBuilder = new StringBuilder();
            int serializationIndex = 0;
            foreach (GeneratedField field in Fields.Where(field => field.GenerateSerialization)) {
                stringBuilder.AppendLine(field.GetDeserializationCode(serializationIndex));
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
            foreach (GeneratedField field in Fields.Where(field => field.Kind == GeneratedFieldKind.OutputPort)) {
                stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}NotifyPortValueChanged({field.PascalCaseName}Port);");
            }

            return stringBuilder.ToString();
        }

        private string GetGetValueForPortCode() {
            var stringBuilder = new StringBuilder();
            foreach (GeneratedField field in Fields.Where(field => field.Kind == GeneratedFieldKind.OutputPort)) {
                if (GetterMethods.ContainsKey(field.Name)) {
                    stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}if (port == {field.PascalCaseName}Port) return {GetterMethods[field.Name]}();");
                } else if (field.CustomGetter) {
                    stringBuilder.AppendLine(
                        $"{GeneratorUtils.Indent(3)}if (port == {field.PascalCaseName}Port) {{\n{GeneratorUtils.Indent(4)}{field.GetterCode}\n{GeneratorUtils.Indent(3)}}}");
                } else {
                    stringBuilder.AppendLine(field.GetGetValueForPortCode());
                }
            }

            return stringBuilder.ToString();
        }

        private string GetOnPortValueChangedCode() {
            var stringBuilder = new StringBuilder();

            // Add if (port == outputPortA || ...) return;
            if (Fields.Any(field => field.Kind == GeneratedFieldKind.OutputPort)) {
                stringBuilder.AppendLine(
                    $"{GeneratorUtils.Indent(3)}if ({string.Join(" || ", Fields.Where(field => field.Kind == GeneratedFieldKind.OutputPort).Select(field => $"port == {field.PascalCaseName}Port"))}) return;");
            }

            // Add individual ifs joined by ` else `
            if (Fields.Any(field => field.Kind == GeneratedFieldKind.InputPort)) {
                string notify = string.Join(" else ", Fields.Where(field => field.Kind == GeneratedFieldKind.InputPort).Select(field => {
                    string fieldCalculateMethods = GetCalculateMethodsFor(field, 4);
                    string fieldNotifyMethods = GetNotifyMethodsFor(field, 4);
                    return field.GetOnPortValueChangedCode(fieldCalculateMethods, fieldNotifyMethods);
                }));
                stringBuilder.AppendLine($"{GeneratorUtils.Indent(3)}{notify}");
            }

            return stringBuilder.ToString();
        }

        private string GetDebugInfoCode() {
            var stringBuilder = new StringBuilder();

            foreach (GeneratedField field in Fields) {
                stringBuilder.AppendLine(field.GetDebugCode());
            }

            foreach (KeyValuePair<string, HashSet<string>> pair in UpdateMethods) {
                if (pair.Key == "") {
                    stringBuilder.AppendLine($"Methods `{string.Join(", ", pair.Value)}` update unknown field");
                } else {
                    stringBuilder.AppendLine($"Methods `{string.Join(", ", pair.Value)}` update field `{pair.Key}`");
                }
            }

            foreach (string method in UpdateAllMethods) {
                stringBuilder.AppendLine($"Method `{method}` updates all [Out] fields");
            }

            foreach (KeyValuePair<string, string> pair in GetterMethods) {
                stringBuilder.AppendLine($"Method `{pair.Value}` returns value of field `{pair.Key}`");
            }

            return stringBuilder.ToString();
        }

        #endregion

        #region Information Collection

        private void CollectFields() {
            List<FieldDeclarationSyntax> fieldDeclarations = classDeclarationSyntax.Members.OfType<FieldDeclarationSyntax>().ToList();
            
            // [In] fields
            Fields.AddRange(
                fieldDeclarations
                    .Where(field => field.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "In"))
                    .Select(field => new GeneratedField(field, GeneratedFieldKind.InputPort))
            );
            
            // [Out] fields
            Fields.AddRange(
                fieldDeclarations
                    .Where(field => field.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "Out"))
                    .Select(field => new GeneratedField(field, GeneratedFieldKind.OutputPort))
            );
            
            // [Setting] fields
            Fields.AddRange(
                fieldDeclarations
                    .Where(field => field.AttributeLists
                                         .SelectMany(attrs => attrs.Attributes)
                                         .Any(attr => attr.Name.ToString() == "Setting"))
                    .Select(field => new GeneratedField(field, GeneratedFieldKind.Setting))
            );
        }

        private void CollectMethods() {
            List<MethodDeclarationSyntax> methods = classDeclarationSyntax.Members.OfType<MethodDeclarationSyntax>().ToList();
            foreach (MethodDeclarationSyntax method in methods) {
                foreach (AttributeSyntax attribute in method.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                    string attributeName = attribute.Name.ToString();

                    if (attributeName == "CalculatesAllFields" || attributeName == "CalculatesField" && attribute.ArgumentList == null) {
                        UpdateAllMethods.Add(method.Identifier.Text);
                        continue;
                    }

                    if (attribute.ArgumentList == null) continue;
                    List<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments.ToList();
                    if (arguments.Count == 0) continue;

                    switch (attributeName) {
                        case "CalculatesField": {
                            string variableName = GeneratorUtils.ExtractFieldNameFromExpression(arguments[0].Expression);
                            if (string.IsNullOrEmpty(variableName)) continue;

                            if (!UpdateMethods.ContainsKey(variableName)) {
                                UpdateMethods.Add(variableName, new HashSet<string>());
                            }

                            UpdateMethods[variableName].Add(method.Identifier.Text);
                            break;
                        }
                        case "GetterMethod": {
                            string fieldName = GeneratorUtils.ExtractFieldNameFromExpression(arguments[0].Expression);
                            if (string.IsNullOrEmpty(fieldName)) {
                                continue;
                            }

                            GetterMethods[fieldName] = method.Identifier.Text;

                            break;
                        }
                    }
                }
            }
        }

        #endregion

        #region Utility methods

        private string GetCalculateMethodsFor(GeneratedField field, int indent) {
            string indentString = GeneratorUtils.Indent(indent);
            var stringBuilder = new StringBuilder();

            foreach (string method in UpdateAllMethods) {
                stringBuilder.AppendLine($"{indentString}{method}();");
            }

            if (field.UpdatesAllFields) {
                foreach (KeyValuePair<string, HashSet<string>> pair in UpdateMethods) {
                    if (pair.Key == "") continue;
                    foreach (string method in pair.Value) {
                        stringBuilder.AppendLine($"{indentString}{method}();");
                    }
                }

                return stringBuilder.ToString();
            }

            foreach (string updatedField in field.UpdatesFields) {
                if (!UpdateMethods.ContainsKey(updatedField)) continue;

                foreach (string method in UpdateMethods[updatedField]) {
                    stringBuilder.AppendLine($"{indentString}{method}();");
                }
            }

            return stringBuilder.ToString();
        }

        private string GetNotifyMethodsFor(GeneratedField field, int indent) {
            string indentString = GeneratorUtils.Indent(indent);
            var stringBuilder = new StringBuilder();

            foreach (GeneratedField notifiedField in Fields.Where(otherField => otherField.Kind == GeneratedFieldKind.OutputPort)) {
                if (field.UpdatesAllFields || field.UpdatesFields.Contains(notifiedField.Name)) {
                    stringBuilder.AppendLine($"{indentString}NotifyPortValueChanged({notifiedField.PascalCaseName}Port);");
                }
            }

            return stringBuilder.ToString();
        }

        #endregion
    }
}