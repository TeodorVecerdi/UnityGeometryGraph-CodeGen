using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public static class GeneratorContext {
        public static ConcurrentBag<GeneratedClass> NodeTypes;
        public static ConcurrentBag<EnumDeclarationSyntax> EnumTypes;
        public static ConcurrentBag<GeneratedFile> GeneratedFiles;
        public static ConcurrentDictionary<string, GeneratedFile> GeneratedFilesByName;
    }
}