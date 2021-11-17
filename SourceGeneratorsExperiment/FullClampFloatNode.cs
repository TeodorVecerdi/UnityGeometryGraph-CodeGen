using SourceGeneratorsExperiment.GeneratorAPI;

namespace SourceGeneratorsExperiment {
    [GenerateRuntimeNode]
    public partial class FullClampFloatNode : RuntimeNode {
        [In]
        private float input;
        [In, UpdatesProperties(nameof(result))]
        private float min;
        [In, UpdatesProperties(nameof(otherResult))]
        private float max;
        
        [Out, Getter(@"return 1.0f + {self};")]
        private float result;
        [Out]
        private float otherResult;
        
        [GetterMethod(nameof(otherResult))]
        private float GetOtherResult() {
            return otherResult;
        }

        [CalculatesProperty(nameof(result))]
        public void CalculateResult() {
            if (input < min) {
                result = min;
            } else if (input > max) {
                result = max;
            } else {
                result = input;
            }
        }
        
        [CalculatesProperty("result")]
        public void CalculateResult2() {
            if (input < min) {
                result = min;
            } else if (input > max) {
                result = max;
            } else {
                result = input;
            }
        }

        [CalculatesProperty, CalculatesAllProperties]
        public void CalculateAll() {
            CalculateResult();
        }
    }
}