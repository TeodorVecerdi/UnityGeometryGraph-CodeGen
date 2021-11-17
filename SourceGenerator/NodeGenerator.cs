using System.Collections.Concurrent;
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
                var sourceCode = generatedClass.GetCode();
                context.AddSource($"{generatedClass.ClassName}.gen.cs", SourceText.From(sourceCode, Encoding.UTF8));
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