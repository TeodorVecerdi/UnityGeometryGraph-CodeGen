using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGenerator {
    public static class GeneratorContext {
        public static ConcurrentBag<ClassDeclarationSyntax> NodeTypes;
        public static ConcurrentBag<GeneratedClass> GeneratedClasses;
        public static ConcurrentBag<GeneratedFile> GeneratedFiles;
        public static ConcurrentDictionary<string, string> EnumTypes;
        public static ConcurrentDictionary<string, GeneratedFile> GeneratedFilesByName;
    }
}