﻿namespace SourceGenerator {
    internal static class Templates {
        public const string AutoGeneratedHeader = @"// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the GeometryGraph Code Generator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------";
        
        // 0: using, 1: namespace, 2: class name, 3: port fields, 4: port initializers, 5: serialization, 6: deserialization,
        // 7: update from editor node methods, 8: GetValueForPort ifs, 9: OnPortValueChanged code
        public static string ClassTemplate = AutoGeneratedHeader + @"

{0}

namespace {1} {{
    [SourceClass(""{{SOURCE_NAME}}"")]
    public partial class {2} : RuntimeNode {{
{3}

        public {2}(string guid) : base(guid) {{
{4}
        }}{7}

        protected override object GetValueForPort(RuntimePort port) {{
{8}
            return null;
        }}

        protected override void OnPortValueChanged(Connection connection, RuntimePort port) {{
{9}
        }}{5}{6}
    }}
}}";
        
        public static string ClassTemplateNoNamespace = AutoGeneratedHeader + @"

{0}

[SourceClass(""{{SOURCE_NAME}}"")]
public partial class {1} : RuntimeNode {{
{2}

    public {1}(string guid) : base(guid) {{
{3}
    }}{6}

    protected override object GetValueForPort(RuntimePort port) {{
{7}
        return null;
    }}

    protected override void OnPortValueChanged(Connection connection, RuntimePort port) {{
{8}
    }}{4}{5}
}}";

        // 0: indent, 1: serialization
        public static string SerializationTemplate = @"{0}public override string GetCustomData() {{
{0}    return new JArray {{
{1}
{0}    }}.ToString(Formatting.None);
{0}}}";
        
        // 0: indent, 1: deserialization, 2: update/notify
        public static string DeserializationTemplate = @"{0}public override void SetCustomData(string data) {{{1}
{2}
{0}}}";

        // 0: indent, 1: deserialization
        public static string DeserializationLoadTemplate = @"{0}    JArray array = JArray.Parse(data);
{1}";

        // 0: port name
        public static string PortPropertyTemplate = @"public RuntimePort {0} {{ get; }}";
        
        // 0: port name, 1: port type, 2: port direction
        public static string PortCtorTemplate =     @"{0} = RuntimePort.Create(PortType.{1}, PortDirection.{2}, this);"; 
        
        // 0: indent, 1: pascal case name, 2: type, 3: equality if, 4: field name, 5: calculate methods, 6: notify methods
        public static string UpdateFromEditorNodeTemplate = @"{0}public void Update{1}({2} newValue) {{{3}
{0}    {4} = newValue;{5}{6}
{0}}}";

        // 0: indent, 1: pascal case name, 2: field name, 3: equality, 4: calculate methods, 5: notify methods
        // extraCodeBeforeGetValue,  extraCodeAfterGetValue,  extraCodeAfterEqualityCheck,  extraCodeAfterUpdate,  extraCodeAfterCalculate,  extraCodeAfterNotify
        public static string OnPortValueChangedIfTemplate = @"if (port == {1}) {{{6}
{0}    var newValue = GetValue(connection, {2});{7}{3}{8}
{0}    {2} = newValue;{9}{4}{10}{5}{11}
{0}}}";
    }
}