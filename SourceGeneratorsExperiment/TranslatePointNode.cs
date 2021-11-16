using SourceGeneratorsExperiment.GeneratorAPI;

namespace SourceGeneratorsExperiment {
    public class GeometryData {}
    
    [GenerateRuntimeNode]
    public partial class TranslatePointNode : RuntimeNode {
        [In] private GeometryData geometry;
        [In] private float3 translation;
        [In] private string attribute;
        [Setting] private TranslatePointNode_Mode mode;
        [Out] private GeometryData result;

        [CalculatesField(nameof(result))]
        private void Calculate() {
            // TODO: Implement
        }

        public enum TranslatePointNode_Mode { Vector = 0, Attribute = 1 }
    }
}
