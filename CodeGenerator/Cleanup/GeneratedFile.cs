using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public class GeneratedFile {
        public string SourceClassName { get; }
        public string GeneratedFilePath { get; }

        public GeneratedFile(ClassDeclarationSyntax classDeclarationSyntax) {
            GeneratedFilePath = classDeclarationSyntax.SyntaxTree.FilePath;
            foreach (AttributeSyntax attributeSyntax in classDeclarationSyntax.AttributeLists.SelectMany(attrs => attrs.Attributes)) {
                if (attributeSyntax.Name.ToString() != "SourceClass")continue;
                
                SeparatedSyntaxList<AttributeArgumentSyntax> arguments = attributeSyntax.ArgumentList.Arguments;
                SourceClassName = GeneratorUtils.ExtractStringFromExpression(arguments[0].Expression);
                break;
            }
        }
    }
}