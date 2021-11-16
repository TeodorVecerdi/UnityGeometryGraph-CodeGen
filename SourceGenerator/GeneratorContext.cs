using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public static class GeneratorContext {
        public static readonly List<ClassDeclarationSyntax> NodeTypes = new List<ClassDeclarationSyntax>();
        public static readonly List<EnumDeclarationSyntax> EnumTypes = new List<EnumDeclarationSyntax>();
    }
}