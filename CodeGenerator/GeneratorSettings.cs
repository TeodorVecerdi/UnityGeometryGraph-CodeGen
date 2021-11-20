using System.Collections.Generic;

namespace SourceGenerator {
    public class GeneratorSettings {
        public string OutputRelativePath { get; set; }           = "";
        public bool GenerateSerialization { get; set; }          = true;
        public bool CalculateDuringDeserialization { get; set; } = true;
        public List<string> AdditionalUsingStatements { get; }   = new();
    }
}