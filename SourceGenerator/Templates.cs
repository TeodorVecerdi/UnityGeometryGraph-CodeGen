﻿namespace SourceGenerator {
    internal static class Templates {
        public const string AutoGeneratedHeader = @"// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the GeometryGraph Node Generator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------";
        
        // 0: using, 1: namespace, 2: class name, 3: port fields, 4: port initializers, 5: serialization, 6: deserialization,
        // 7: calculate/notify after deserialize, 8: update from editor node methods, 9: GetValueForPort ifs, 10: OnPortValueChanged code
        // 11: debugging info
        public static string ClassTemplate = AutoGeneratedHeader + @"

{0}{11}

namespace {1} {{
    [SourceClass(""{{SOURCE_NAME}}"", ""{{SOURCE_PATH}}"")]
    public partial class {2} : RuntimeNode {{
{3}

        public {2}(string guid) : base(guid) {{
{4}
        }}

{8}

        protected override object GetValueForPort(RuntimePort port) {{
{9}
            return null;
        }}

        protected override void OnPortValueChanged(Connection connection, RuntimePort port) {{
{10}
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
        public static string PortPropertyTemplate = @"public RuntimePort {0}Port {{ get; }}";
        
        // 0: port name, 1: port type, 2: port direction
        public static string PortCtorTemplate =     @"{0}Port = RuntimePort.Create(PortType.{1}, PortDirection.{2}, this);"; 
        
        // 0: port name
        public static string NotifyResultTemplate = @"NotifyPortValueChanged({0}Port);";
        
        // 0: indent, 1: pascal case name, 2: type, 3: equality if, 4: field name, 5: calculate methods, 6: notify methods
        public static string UpdateFromEditorNodeTemplate = @"{0}public void Update{1}({2} newValue) {{{3}
{0}    {4} = newValue;{5}{6}
{0}}}";

        // 0: indent, 1: pascal case name, 2: field name, 3: equality, 4: calculate methods, 5: notify methods
        public static string OnPortValueChangedIfTemplate = @"if (port == {1}Port) {{
{0}    var newValue = GetValue({1}Port, {2});{3}

{0}    {2} = newValue;{4}{5}
{0}}}";
    }
}