using System.Collections.Concurrent;

namespace SourceGenerator {
    public static class GeneratorContext {
        public static ConcurrentBag<GeneratedClass> NodeTypes;
        public static ConcurrentBag<GeneratedFile> GeneratedFiles;
        public static ConcurrentDictionary<string, string> EnumTypes;
        public static ConcurrentDictionary<string, GeneratedFile> GeneratedFilesByName;
    }
}