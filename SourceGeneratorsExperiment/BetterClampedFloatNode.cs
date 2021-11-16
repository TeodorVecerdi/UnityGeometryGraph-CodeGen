using SourceGeneratorsExperiment.GeneratorAPI;

namespace SourceGeneratorsExperiment {
    [GenerateNodeImplementation]
    public partial class BetterClampedFloatNode : RuntimeNode {
        [In]  private float input;
        [In]  private float min;
        [In(IsSerialized = true, UpdatedFromEditorNode = true)]  private float max;
        [Out] private float result;

        private float GetValueForResult() => result;
        
        [CalculateMethod]
        public void Calculate() {
            if (input < min) {
                result = min;
            } else if (input > max) {
                result = max;
            } else {
                result = input;
            }
        }
    }
}