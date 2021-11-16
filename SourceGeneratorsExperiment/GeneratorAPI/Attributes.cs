using System;

namespace SourceGeneratorsExperiment.GeneratorAPI {
    [AttributeUsage(AttributeTargets.Class)]
    public class GenerateRuntimeNodeAttribute : Attribute {
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public class AdditionalUsingStatementsAttribute : Attribute {
        public string[] Namespaces { get; }
        
        public AdditionalUsingStatementsAttribute(params string[] namespaces) {
            Namespaces = namespaces;
        }
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class CalculatesFieldAttribute : Attribute {
        public string Field { get; }

        public CalculatesFieldAttribute() {
            
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Attribute" /> class.</summary>
        /// <footer><a href="https://docs.microsoft.com/en-us/dotnet/api/System.Attribute?view=netcore-5.0">`Attribute` on docs.microsoft.com</a></footer>
        public CalculatesFieldAttribute(string field) {
            Field = field;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class CalculatesAllFieldsAttribute : Attribute {
    }


    [AttributeUsage(AttributeTargets.Field)]
    public class UpdatesFieldsAttribute : Attribute {
        public string[] Fields { get; }
        
        /// <summary>Initializes a new instance of the <see cref="T:System.Attribute" /> class.</summary>
        /// <footer><a href="https://docs.microsoft.com/en-us/dotnet/api/System.Attribute?view=netcore-5.0">`Attribute` on docs.microsoft.com</a></footer>
        public UpdatesFieldsAttribute(params string[] fields) {
            Fields = fields;
        }
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

    [AttributeUsage(AttributeTargets.Field)]
    public class CustomSerializationAttribute : Attribute {
        public string SerializationCode { get; }
        public string DeserializationCode { get; }

        public CustomSerializationAttribute(string serializationCode, string deserializationCode) {
            SerializationCode = serializationCode;
            DeserializationCode = deserializationCode;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class CustomEqualityAttribute : Attribute {
        public string EqualityCode { get; }

        public CustomEqualityAttribute(string equalityCode) {
            EqualityCode = equalityCode;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class GetterAttribute : Attribute {
        public string GetterCode { get; }
        
        public GetterAttribute(string getterCode) {
            GetterCode = getterCode;
        }
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class GetterMethodAttribute : Attribute {
        public string Field { get; }
        public bool Inline { get; set; }
        
        public GetterMethodAttribute(string field) {
            Field = field;
        }
    }
    
}