using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGenerator {
    [Generator]
    public class NodeGenerator : ISourceGenerator {
        public void Initialize(GeneratorInitializationContext context) {
        }

        public void Execute(GeneratorExecutionContext context) {
            foreach (SyntaxNode syntaxNode in context.Compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes())) {
                if (syntaxNode is EnumDeclarationSyntax eds) {
                    GeneratorContext.EnumTypes.Add(eds);
                }
            }

            foreach (SyntaxNode syntaxNode in context.Compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes())) {
                if (syntaxNode is ClassDeclarationSyntax { BaseList: { } } nodeClass &&
                    nodeClass.AttributeLists.Any(a => a.Attributes.Any(a2 => a2.Name.ToString() == "GenerateNodeImplementation"))
                ) {
                    var generatedClass = new GeneratedClass(nodeClass);
                    var sourceCode = generatedClass.GetCode();
                    context.AddSource($"{generatedClass.ClassName}.gen.cs", SourceText.From(sourceCode, Encoding.UTF8));
                }
            }
        }
    }
}