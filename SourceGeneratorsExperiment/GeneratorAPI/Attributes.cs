using System;

namespace SourceGeneratorsExperiment.GeneratorAPI {
    [AttributeUsage(AttributeTargets.Class)]
    public class GenerateNodeImplementationAttribute : Attribute {
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class CalculateMethodAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class InAttribute : Attribute {
        /// <summary>
        /// Whether the field is updated from the editor node.<br/>Default: <c>true</c>
        /// </summary>
        public bool UpdatedFromEditorNode { get; set; } = true;
        
        /// <summary>
        /// Whether the field should be serialized.<br/>Default: <c>true</c>
        /// </summary>
        public bool IsSerialized { get; set;  } = true;
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class OutAttribute : Attribute {
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class SettingAttribute : Attribute {
        /// <summary>
        /// Whether the field should be serialized.<br/>Default: <c>true</c>
        /// </summary>
        public bool IsSerialized { get; set;  } = true;
    }
}