namespace SourceGeneratorsExperiment {
    [GenerateRuntimeNode(OutputPath = "")]
    public partial class GenClampFloatNode : RuntimeNode {
        [In] private float value;
        [In] private float min;
        [In] private float max;
        [Out] private float result;


        [CalculatesProperty(nameof(result))]
        private void Calculate() {
            if (value < min) {
                result = min;
            } else if (value > max) {
                result = max;
            } else {
                result = value;
            }
        }
    }
}