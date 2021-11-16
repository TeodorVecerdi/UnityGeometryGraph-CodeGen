using SourceGeneratorsExperiment;
using SourceGeneratorsExperiment.GeneratorAPI;

namespace GeometryGraph.Runtime.Graph {
    [GenerateRuntimeNode]
    [AdditionalUsingStatements("SourceGeneratorsExperiment", "GeometryGraph.Runtime.Serialization")]
    public partial class FloatBranchNode : RuntimeNode {
        [In] private bool condition;
        [In] private float ifTrue;
        [In] private float ifFalse;
        [Out] private float result;

        [GetterMethod(nameof(result), Inline = true)]
        private float GetResult() {
            return condition ? ifTrue : ifFalse;
        }
    }
}