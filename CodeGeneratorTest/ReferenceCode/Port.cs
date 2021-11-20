using System;
using System.Collections.Generic;

namespace GeometryGraph.Runtime.Graph {
    public class RuntimePort {
        public string Guid;
        public PortType Type;
        public PortDirection Direction;
        public RuntimeNode Node;
        public List<Connection> Connections;

        public bool IsConnected => Connections?.Count > 0;

        private RuntimePort() { }

        private RuntimePort(PortType type, PortDirection direction, RuntimeNode owner) {
            Type = type;
            Direction = direction;
            Node = owner;
            Connections = new List<Connection>();
        }

        public static RuntimePort Create(PortType type, PortDirection direction, RuntimeNode owner) {
            var port = new RuntimePort(type, direction, owner);
            owner.Ports.Add(port);
            return port;
        }
    }
    
    public enum PortType {
        Integer,
        Float,
        Vector,
        Boolean,
        Geometry,
        Collection,
        String,
        Curve,
        
        Any = int.MaxValue
    }

    public enum PortDirection {
        Input,
        Output
    }
    
    public static class PortValueConverter {
        public static object Convert(object value, PortType sourceType, PortType targetType) {
            if (targetType == PortType.Float) {
                switch (sourceType) {
                    case PortType.Integer: return (float)(int)value;
                    case PortType.Boolean: return (bool)value ? 1.0f : 0.0f;
                }
            } else if (targetType == PortType.Integer) {
                switch (sourceType) {
                    case PortType.Float: return (int)(float)value;
                    case PortType.Boolean: return (bool)value ? 1 : 0;
                }
            } else if (targetType == PortType.Boolean) {
                switch (sourceType) {
                    case PortType.Integer: return (int)value != 0;
                    case PortType.Float: return (float)value != 0.0f;
                }
            }

            return value;
        }
    }
    
    public static class PortTypeUtility {
        public static bool IsUnmanagedType(PortType type) {
            switch (type) {
                case PortType.Integer:
                case PortType.Float:
                case PortType.Vector:
                case PortType.Boolean:
                    return true;
                
                case PortType.Geometry:
                case PortType.Collection:
                case PortType.String:
                case PortType.Curve:
                case PortType.Any:
                    return false;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
    }
}