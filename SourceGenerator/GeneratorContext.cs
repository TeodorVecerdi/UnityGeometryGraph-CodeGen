using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public static class GeneratorContext {
        public static ConcurrentBag<ClassDeclarationSyntax> NodeTypes;
        public static ConcurrentBag<EnumDeclarationSyntax> EnumTypes;
    }
}