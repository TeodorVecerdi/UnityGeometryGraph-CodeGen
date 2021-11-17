using System.Collections.Concurrent;
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
            GeneratorContext.EnumTypes = new ConcurrentBag<EnumDeclarationSyntax>();

            context.RegisterForSyntaxNotifications(() => new TypeCollectorSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context) {
            foreach (ClassDeclarationSyntax nodeClass in GeneratorContext.NodeTypes) {
                var generatedClass = new GeneratedClass(nodeClass);
                string sourceCode = generatedClass.GetCode();
                
                // Output path
                string filePath = nodeClass.SyntaxTree.FilePath;
                string directory = filePath.Substring(0, filePath.LastIndexOf('\\') + 1);
                string destinationDirectory = Path.Combine(directory, generatedClass.RelativePath);
                string destinationPath = Path.Combine(destinationDirectory, $"{generatedClass.ClassName}.gen.cs");
                
                if (!Directory.Exists(destinationDirectory)) {
                    Directory.CreateDirectory(destinationDirectory);
                }

                sourceCode = sourceCode.Replace("[SourceClass(\"{SOURCE_NAME}\", \"{SOURCE_PATH}\")]", $"[SourceClass(\"{generatedClass.ClassName}\", \"{filePath.Replace("\\", "\\\\")}\")]");
                
                File.WriteAllText(destinationPath, sourceCode);
                // context.AddSource($"{generatedClass.ClassName}.gen.cs", SourceText.From(sourceCode, Encoding.UTF8));
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
                        GeneratorContext.NodeTypes.Add(cd);
                    break;
                }
            }
        }
    }
}