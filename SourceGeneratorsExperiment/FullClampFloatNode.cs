using SourceGeneratorsExperiment.GeneratorAPI;

namespace SourceGeneratorsExperiment {
    [GenerateNodeImplementation]
    public partial class FullClampFloatNode : RuntimeNode {
        [In]
        private float input;
        [In, UpdatesFields(nameof(result))]
        private float min;
        [In, UpdatesFields(nameof(otherResult))]
        private float max;
        
        [Out, Getter(@"return 1.0f + {field};")]
        private float result;
        [Out]
        private float otherResult;
        
        [GetterMethod(nameof(otherResult))]
        private float GetOtherResult() {
            return otherResult;
        }

        [CalculatesField(nameof(result))]
        public void CalculateResult() {
            if (input < min) {
                result = min;
            } else if (input > max) {
                result = max;
            } else {
                result = input;
            }
        }
        
        [CalculatesField("result")]
        public void CalculateResult2() {
            if (input < min) {
                result = min;
            } else if (input > max) {
                result = max;
            } else {
                result = input;
            }
        }

        [CalculatesField, CalculatesAllFields]
        public void CalculateAll() {
            CalculateResult();
        }
    }
}