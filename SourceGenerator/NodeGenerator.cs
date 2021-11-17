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
            GeneratorContext.NodeTypes = new ConcurrentBag<GeneratedClass>();
            GeneratorContext.EnumTypes = new ConcurrentDictionary<string, string>();
            GeneratorContext.GeneratedFiles = new ConcurrentBag<GeneratedFile>();
            GeneratorContext.GeneratedFilesByName = new ConcurrentDictionary<string, GeneratedFile>();

            context.RegisterForSyntaxNotifications(() => new TypeCollectorSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context) {
            Generate(context);
            CleanupOldFiles();
        }

        private static void Generate(GeneratorExecutionContext context) {
            foreach (GeneratedClass generatedClass in GeneratorContext.NodeTypes) {
                generatedClass.AssemblyName = context.Compilation.AssemblyName;

                // Output path
                string filePath = generatedClass.FilePath;
                string directory = filePath.Substring(0, filePath.LastIndexOf('\\') + 1);
                string destinationDirectory = Path.Combine(directory, generatedClass.OutputRelativePath);
                string destinationPath = Path.Combine(destinationDirectory, $"{generatedClass.ClassName}.gen.cs");

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
            var generatedFiles = new HashSet<string>();
            foreach (GeneratedClass generatedClass in GeneratorContext.NodeTypes) {
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
                case EnumDeclarationSyntax eds: {
                    string baseType = "int";
                    if (eds.BaseList is { Types: { Count: > 0 } }) {
                        baseType = eds.BaseList.Types[0].Type.ToString();
                    }
                    GeneratorContext.EnumTypes.TryAdd(eds.Identifier.Text, baseType);
                    break;
                }
                case ClassDeclarationSyntax cd: {
                    if (cd.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "GenerateRuntimeNode")))
                        GeneratorContext.NodeTypes.Add(new GeneratedClass(cd));
                    else if (cd.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "SourceClass"))) {
                        var generatedFile = new GeneratedFile(cd);
                        GeneratorContext.GeneratedFiles.Add(generatedFile);
                        GeneratorContext.GeneratedFilesByName.TryAdd(generatedFile.SourceClassName, generatedFile);
                    }
                    break;
                }
            }
        }
    }
}