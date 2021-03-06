using GeometryGraph.Runtime.Attributes;

namespace GeometryGraph.Runtime.Graph {
    [GenerateRuntimeNode]
    [AdditionalUsingStatements("SourceGeneratorsExperiment", "GeometryGraph.Runtime.Serialization")]
    public partial class FloatBranchNode {
        [In] public bool Condition { get; private set; }
        [In] public float IfTrue { get; private set; }
        [In] public float IfFalse { get; private set; }
        [Out] public float Result { get; private set; }

        [GetterMethod(nameof(Result), Inline = true)] 
        private float GetResult() {
            return Condition ? IfTrue : IfFalse;
        }
        
        [CalculatesProperty(nameof(Result))]
        private void Calculate() {}
    }
}
