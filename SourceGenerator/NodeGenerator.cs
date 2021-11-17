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
            GeneratorContext.EnumTypes = new ConcurrentBag<EnumDeclarationSyntax>();
            GeneratorContext.GeneratedFiles = new ConcurrentBag<GeneratedFile>();
            GeneratorContext.GeneratedFilesByName = new ConcurrentDictionary<string, GeneratedFile>();

            context.RegisterForSyntaxNotifications(() => new TypeCollectorSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context) {
            Generate(context);
            CleanupOldFiles();
        }

        private void Generate(GeneratorExecutionContext context) {
            foreach (GeneratedClass generatedClass in GeneratorContext.NodeTypes) {
                generatedClass.AssemblyName = context.Compilation.AssemblyName;
                string sourceCode = generatedClass.GetCode();

                // Output path
                string filePath = generatedClass.FilePath;
                string directory = filePath.Substring(0, filePath.LastIndexOf('\\') + 1);
                string destinationDirectory = Path.Combine(directory, generatedClass.OutputRelativePath);
                string destinationPath = Path.Combine(destinationDirectory, $"{generatedClass.ClassName}.gen.cs");

                if (!Directory.Exists(destinationDirectory)) {
                    Directory.CreateDirectory(destinationDirectory);
                }

                string qualifiedName = GeneratorUtils.GetQualifiedClassName(generatedClass);

                if (GeneratorContext.GeneratedFilesByName.ContainsKey(qualifiedName)) {
                    var existingFile = GeneratorContext.GeneratedFilesByName[qualifiedName];
                    if (existingFile.GeneratedFilePath != destinationPath) {
                        File.Delete(existingFile.GeneratedFilePath);
                        if (File.Exists($"{existingFile.GeneratedFilePath}.meta")) {
                            File.Delete($"{existingFile.GeneratedFilePath}.meta");
                        }
                        
                        string removedDirectory = existingFile.GeneratedFilePath.Substring(0, existingFile.GeneratedFilePath.LastIndexOf('\\'));
                        if (!Directory.EnumerateFileSystemEntries(removedDirectory).Any()) {
                            Directory.Delete(removedDirectory);
                        }
                    }
                }

                sourceCode = sourceCode.Replace("[SourceClass(\"{SOURCE_NAME}\")]", $"[SourceClass(\"{qualifiedName}\")]");
                sourceCode = sourceCode.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                File.WriteAllText(destinationPath, sourceCode);
                // context.AddSource($"{generatedClass.ClassName}.gen.cs", SourceText.From(sourceCode, Encoding.UTF8));
            }
        }

        private void CleanupOldFiles() {
            var generatedFiles = new HashSet<string>();
            foreach (GeneratedClass generatedClass in GeneratorContext.NodeTypes) {
                generatedFiles.Add(GeneratorUtils.GetQualifiedClassName(generatedClass));
            }
            
            foreach (GeneratedFile generatedFile in GeneratorContext.GeneratedFiles) {
                if (!generatedFiles.Contains(generatedFile.SourceClassName)) {
                    File.Delete(generatedFile.GeneratedFilePath);
                    if (File.Exists($"{generatedFile.GeneratedFilePath}.meta")) {
                        File.Delete($"{generatedFile.GeneratedFilePath}.meta");
                    }
                    
                    string removedDirectory = generatedFile.GeneratedFilePath.Substring(0, generatedFile.GeneratedFilePath.LastIndexOf('\\'));
                    if (!Directory.EnumerateFileSystemEntries(removedDirectory).Any()) {
                        Directory.Delete(removedDirectory);
                    }
                }
            }
        }
    }

    public class TypeCollectorSyntaxReceiver : ISyntaxReceiver {
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode) {
            switch (syntaxNode) {
                case EnumDeclarationSyntax eds: {
                    GeneratorContext.EnumTypes.Add(eds);
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