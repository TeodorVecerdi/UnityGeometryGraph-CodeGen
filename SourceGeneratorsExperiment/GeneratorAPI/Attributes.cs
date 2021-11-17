using System;

namespace SourceGeneratorsExperiment {
    [AttributeUsage(AttributeTargets.Class)]
    public class GenerateRuntimeNodeAttribute : Attribute {
        /// <summary>
        /// Specifies where the generated file should be placed relative to the original file.
        /// </summary>
        public string OutputPath { get; set; }
    }

    public class SourceClassAttribute : Attribute {
        public SourceClassAttribute(string name, string path) { }
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    public class AdditionalUsingStatementsAttribute : Attribute {
        public string[] Namespaces { get; }
        
        public AdditionalUsingStatementsAttribute(params string[] namespaces) {
            Namespaces = namespaces;
        }
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class CalculatesPropertyAttribute : Attribute {
        public string Property { get; }

        public CalculatesPropertyAttribute() {
            
        }

        /// <summary>Initializes a new instance of the <see cref="T:System.Attribute" /> class.</summary>
        /// <footer><a href="https://docs.microsoft.com/en-us/dotnet/api/System.Attribute?view=netcore-5.0">`Attribute` on docs.microsoft.com</a></footer>
        public CalculatesPropertyAttribute(string property) {
            Property = property;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class CalculatesAllPropertiesAttribute : Attribute {
    }


    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class UpdatesPropertiesAttribute : Attribute {
        public string[] Properties { get; }
        
        /// <summary>Initializes a new instance of the <see cref="T:System.Attribute" /> class.</summary>
        /// <footer><a href="https://docs.microsoft.com/en-us/dotnet/api/System.Attribute?view=netcore-5.0">`Attribute` on docs.microsoft.com</a></footer>
        public UpdatesPropertiesAttribute(params string[] properties) {
            Properties = properties;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InAttribute : Attribute {
        /// <summary>
        /// Whether the property is updated from the editor node.<br/>Default: <c>true</c>
        /// </summary>
        public bool UpdatedFromEditorNode { get; set; } = true;
        
        /// <summary>
        /// Whether the property should be serialized.<br/>Default: <c>true</c>
        /// </summary>
        public bool IsSerialized { get; set;  } = true;
    }
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class OutAttribute : Attribute {
    }
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class SettingAttribute : Attribute {
        /// <summary>
        /// Whether the property should be serialized.<br/>Default: <c>true</c>
        /// </summary>
        public bool IsSerialized { get; set;  } = true;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class CustomSerializationAttribute : Attribute {
        public string SerializationCode { get; }
        public string DeserializationCode { get; }

        public CustomSerializationAttribute(string serializationCode, string deserializationCode) {
            SerializationCode = serializationCode;
            DeserializationCode = deserializationCode;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class CustomEqualityAttribute : Attribute {
        public string EqualityCode { get; }

        public CustomEqualityAttribute(string equalityCode) {
            EqualityCode = equalityCode;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class GetterAttribute : Attribute {
        public string GetterCode { get; }
        
        public GetterAttribute(string getterCode) {
            GetterCode = getterCode;
        }
    }
    
    [AttributeUsage(AttributeTargets.Method)]
    public class GetterMethodAttribute : Attribute {
        public string Property { get; }
        public bool Inline { get; set; }
        
        public GetterMethodAttribute(string property) {
            Property = property;
        }
    }
    
}