using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SourceGeneratorsExperiment {
    public partial class BetterClampedFloatNode : RuntimeNode {
        public RuntimePort InputPort { get; private set; }
        public RuntimePort MinPort { get; private set; }
        public RuntimePort MaxPort { get; private set; }
        public RuntimePort ResultPort { get; private set; }

        public BetterClampedFloatNode(string guid) : base(guid) {
            InputPort = RuntimePort.Create(PortType.Float, PortDirection.Input, this);
            MinPort = RuntimePort.Create(PortType.Float, PortDirection.Input, this);
            MaxPort = RuntimePort.Create(PortType.Float, PortDirection.Input, this);
            ResultPort = RuntimePort.Create(PortType.Float, PortDirection.Output, this);

        }

        protected override object GetValueForPort(RuntimePort port) {
            return null;
        }

        protected override void OnPortValueChanged(Connection connection, RuntimePort port) {
            // empty;
        }

        public override string GetCustomData() {
            return new JArray {
                input,
                min,
                max
            }.ToString(Formatting.None);
        }

        public override void SetCustomData(string data) {
            var array = JArray.Parse(data);
            input = array.Value<float>(0);
        }
    }
}