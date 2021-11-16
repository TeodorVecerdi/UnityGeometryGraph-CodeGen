﻿using SourceGeneratorsExperiment.GeneratorAPI;

namespace SourceGeneratorsExperiment {
    [GenerateRuntimeNode]
    public partial class SimpleClampFloatNode : RuntimeNode {
        [In] private float value;
        [In] private float min;
        [In] private float max;
        [Out] private float result;


        [CalculatesField(nameof(result))]
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