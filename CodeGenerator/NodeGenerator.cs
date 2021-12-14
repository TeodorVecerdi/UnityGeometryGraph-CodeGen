using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerator {
    [Generator]
    public class NodeGenerator : ISourceGenerator {
        public void Initialize(GeneratorInitializationContext context) {
            GeneratorContext.NodeTypes = new ConcurrentBag<ClassDeclarationSyntax>();
            GeneratorContext.EnumTypes = new ConcurrentDictionary<string, string>();
            GeneratorContext.GeneratedFiles = new ConcurrentBag<GeneratedFile>();
            GeneratorContext.GeneratedFilesByName = new ConcurrentDictionary<string, GeneratedFile>();
            GeneratorContext.GlobalSettings = new GeneratorSettings();

            context.RegisterForSyntaxNotifications(() => new TypeCollectorSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context) {
            Generate(context);
            CleanupOldFiles();
        }

        private static void Generate(GeneratorExecutionContext context) {
            GeneratorContext.GeneratedClasses = new ConcurrentBag<GeneratedClass>();
            foreach (ClassDeclarationSyntax nodeType in GeneratorContext.NodeTypes) {
                GeneratedClass generatedClass = new GeneratedClass(nodeType);
                generatedClass.AssemblyName = context.Compilation.AssemblyName;
                GeneratorContext.GeneratedClasses.Add(generatedClass);

                // Output path
                string filePath = generatedClass.FilePath;
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string extension = Path.GetExtension(filePath);
                if (!string.IsNullOrEmpty(extension)) extension = extension.Substring(1);

                string directory = new FileInfo(filePath).Directory!.FullName;
                string destinationDirectory = Path.Combine(directory, generatedClass.OutputRelativePath);
                string destinationFileName = GeneratorContext.GlobalSettings.OutputFileNamePattern;
                destinationFileName = destinationFileName
                                      .Replace("{fileName}", fileName)
                                      .Replace("{className}", generatedClass.ClassName)
                                      .Replace("{extension}", extension)
                                      .Replace("{namespace}", generatedClass.NamespaceName);
                destinationFileName = GeneratorUtils.RemoveInvalidPathCharacters(destinationFileName);

                if (destinationFileName == "" || (destinationFileName == Path.GetFileName(filePath) && directory == destinationDirectory)) {
                    destinationFileName = $"{fileName}.gen.{extension}";
                }

                if (Path.GetExtension(destinationFileName) == "") {
                    destinationFileName += extension;
                }

                string destinationPath = Path.Combine(destinationDirectory, destinationFileName);

                if (!Directory.Exists(destinationDirectory)) {
                    Directory.CreateDirectory(destinationDirectory);
                }

                string qualifiedName = GeneratorUtils.GetQualifiedClassName(generatedClass);
                CleanupOldFile(qualifiedName, destinationPath);

                string sourceCode = generatedClass.GetCode();
                sourceCode = sourceCode.Replace("[SourceClass(\"{SOURCE_NAME}\")]", $"[SourceClass(\"{qualifiedName}\")]");
                sourceCode = sourceCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                File.WriteAllText(destinationPath, sourceCode);
            }
        }

        private static void CleanupOldFile(string classQualifiedName, string generatedFilePath) {
            if (!GeneratorContext.GeneratedFilesByName.ContainsKey(classQualifiedName)) return;

            GeneratedFile generatedFile = GeneratorContext.GeneratedFilesByName[classQualifiedName];
            if (generatedFile.GeneratedFilePath == generatedFilePath) return;

            DeleteSourceAndDirectory(generatedFile);
        }

        private static void CleanupOldFiles() {
            HashSet<string> generatedFiles = new HashSet<string>();
            foreach (GeneratedClass generatedClass in GeneratorContext.GeneratedClasses) {
                generatedFiles.Add(GeneratorUtils.GetQualifiedClassName(generatedClass));
            }

            foreach (GeneratedFile generatedFile in GeneratorContext.GeneratedFiles) {
                if (generatedFiles.Contains(generatedFile.SourceClassName)) continue;

                DeleteSourceAndDirectory(generatedFile);
            }
        }

        private static void DeleteSourceAndDirectory(GeneratedFile generatedFile) {
            File.Delete(generatedFile.GeneratedFilePath);
            if (File.Exists($"{generatedFile.GeneratedFilePath}.meta")) File.Delete($"{generatedFile.GeneratedFilePath}.meta");

            string removedDirectory = generatedFile.GeneratedFilePath.Substring(0, generatedFile.GeneratedFilePath.LastIndexOf('\\'));
            if (Directory.EnumerateFileSystemEntries(removedDirectory).Any()) return;

            Directory.Delete(removedDirectory);
            if (File.Exists($"{removedDirectory}.meta")) File.Delete($"{removedDirectory}.meta");
        }
    }

    public class TypeCollectorSyntaxReceiver : ISyntaxReceiver {
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
            switch (syntaxNode) {
                case CompilationUnitSyntax cus: {
                    foreach (AttributeSyntax attribute in
                        cus.ChildNodes()
                           .OfType<AttributeListSyntax>()
                           .Where(syntax => syntax.Target is { Identifier: { Text: "assembly" } } && syntax.Attributes.Count > 0)
                           .SelectMany(syntax => syntax.Attributes)) {
                        if (attribute.ArgumentList is not { Arguments: { Count: not 0 } }) continue;
                        SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attribute.ArgumentList.Arguments;
                        string attributeName = attribute.Name.ToString();
                        switch (attributeName) {
                            case "GlobalSettings": {
                                foreach (AttributeArgumentSyntax argument in arguments.Where(arg => arg.NameEquals is { })) {
                                    string argumentName = argument.NameEquals!.Name.ToString();
                                    switch (argumentName) {
                                        case "OutputRelativePath": {
                                            string argumentValue = GeneratorUtils.ExtractStringFromExpression(argument.Expression);
                                            GeneratorContext.GlobalSettings.OutputRelativePath = argumentValue;
                                            break;
                                        }
                                        case "GenerateSerialization": {
                                            bool argumentValue = argument.Expression.ToString() == "true";
                                            GeneratorContext.GlobalSettings.GenerateSerialization = argumentValue;
                                            break;
                                        }
                                        case "CalculateDuringDeserialization": {
                                            bool argumentValue = argument.Expression.ToString() == "true";
                                            GeneratorContext.GlobalSettings.CalculateDuringDeserialization = argumentValue;
                                            break;
                                        }
                                        case "OutputFileNamePattern": {
                                            string argumentValue = GeneratorUtils.ExtractStringFromExpression(argument.Expression);
                                            GeneratorContext.GlobalSettings.OutputFileNamePattern = argumentValue;
                                            break;
                                        }
                                    }
                                }

                                break;
                            }
                            case "AdditionalUsingStatements": {
                                foreach (AttributeArgumentSyntax argument in arguments) {
                                    string argumentValue = GeneratorUtils.ExtractStringFromExpression(argument.Expression);
                                    GeneratorContext.GlobalSettings.AdditionalUsingStatements.Add(argumentValue);
                                }

                                break;
                            }
                        }
                    }

                    break;
                }
                case EnumDeclarationSyntax eds: {
                    string baseType = "int";
                    if (eds.BaseList is { Types: { Count: > 0 } }) {
                        baseType = eds.BaseList.Types[0].Type.ToString();
                    }

                    string enumName = eds.Identifier.Text;
                    if (eds.Parent is ClassDeclarationSyntax cd) {
                        enumName = $"{cd.Identifier.Text}.{enumName}";
                    } else if (eds.Parent is StructDeclarationSyntax sd) {
                        enumName = $"{sd.Identifier.Text}.{enumName}";
                    } else if (eds.Parent is InterfaceDeclarationSyntax id) {
                        enumName = $"{id.Identifier.Text}.{enumName}";
                    }

                    GeneratorContext.EnumTypes.TryAdd(enumName, baseType);
                    break;
                }
                case ClassDeclarationSyntax cd: {
                    if (cd.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "GenerateRuntimeNode")))
                        GeneratorContext.NodeTypes.Add(cd);
                    else if (cd.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "SourceClass"))) {
                        GeneratedFile generatedFile = new GeneratedFile(cd);
                        GeneratorContext.GeneratedFiles.Add(generatedFile);
                        GeneratorContext.GeneratedFilesByName.TryAdd(generatedFile.SourceClassName, generatedFile);
                    }

                    break;
                }
            }
        }
    }
}