namespace SourceGenerator {
    public static class Templates {
        // 0: using, 1: namespace, 2: class name, 3: port fields, 4: port initializers, 5: serialization, 6: deserialization, 7: calculate/notify after deserialize
        public static string ClassTemplate = @"{0}

namespace {1} {{
    public partial class {2} : RuntimeNode {{
{3}
        public {2}(string guid) : base(guid) {{
{4}
        }}

        protected override object GetValueForPort(RuntimePort port) {{
            return null;
        }}

        protected override void OnPortValueChanged(Connection connection, RuntimePort port) {{
            // empty;
        }}

        public override string GetCustomData() {{
            return new JArray {{
{5}
            }}.ToString(Formatting.None);
        }}

        public override void SetCustomData(string data) {{
            JArray array = JArray.Parse(data);
{6}
{7}
        }}
    }}
}}";

        // 0: port name
        public static string PortPropertyTemplate = @"public RuntimePort {0}Port {{ get; private set; }}";
        
        // 0: port name, 1: port type, 2: port direction
        public static string PortCtorTemplate =     @"{0}Port = RuntimePort.Create(PortType.{1}, PortDirection.{2}, this);"; 
        
        // 0: port name
        public static string NotifyResultTemplate = @"NotifyPortValueChanged({0}Port);";
    }
}