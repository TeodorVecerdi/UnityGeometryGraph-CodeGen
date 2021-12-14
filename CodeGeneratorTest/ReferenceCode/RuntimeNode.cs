using System.Collections.Generic;
using System.Linq;

namespace GeometryGraph.Runtime.Graph {
    public abstract class RuntimeNode {
        public string Guid;
        public readonly List<RuntimePort> Ports;

        protected RuntimeNode(string guid) {
            Guid = guid;
            Ports = new List<RuntimePort>();
        }

        protected abstract object GetValueForPort(RuntimePort port);
        protected abstract void OnPortValueChanged(Connection connection, RuntimePort port);

        public virtual IEnumerable<object> GetValuesForPort(RuntimePort port, int count) {
            if (count <= 0) {
                yield break;
            }

            // Here I'm just returning the same value `count` times.
            // Nodes that override this method can return whatever they want.
            object value = GetValueForPort(port);
            for (int i = 0; i < count; i++) {
                yield return value;
            }
        }

        protected virtual void OnConnectionCreated(Connection connection, RuntimePort port) {
        }

        protected virtual void OnConnectionRemoved(Connection connection, RuntimePort port) {
        }

        public virtual string Serialize() => string.Empty;
        public virtual void Deserialize(string json) { }
        public virtual void OnAfterDeserialize() { }

        public void NotifyConnectionCreated(Connection connection, RuntimePort port) {
            if (port.Direction != PortDirection.Input) return;
            OnConnectionCreated(connection, port);
            OnPortValueChanged(connection, port);
        }

        public void NotifyConnectionRemoved(Connection connection, RuntimePort port) {
            OnConnectionRemoved(connection, port);
        }

        public void NotifyPortValueChanged(RuntimePort port) {
            if (port.Direction != PortDirection.Output) return;

            foreach (Connection connection in port.Connections) {
                connection.Input.Node.OnPortValueChanged(connection, connection.Input);
            }
        }

        protected T GetValue<T>(Connection connection, T defaultValue) {
            RuntimePort outputPort = connection.Output;
            if (outputPort == null) {
                return defaultValue;
            }

            object value = outputPort.Node.GetValueForPort(outputPort);

            if (PortTypeUtility.IsUnmanagedType(outputPort.Type)) {
                if (value is T tValueUnmanaged) return tValueUnmanaged;
            } else {
                T tValue = (T)value;
                if (tValue != null) return tValue;
            }

            return (T)PortValueConverter.Convert(value, outputPort.Type, connection.Input.Type);
        }

        protected T GetValue<T>(RuntimePort port, T defaultValue) {
            Connection? firstConnection = port.Connections.FirstOrDefault();
            if (firstConnection == null) {
                return defaultValue;
            }

            return GetValue(firstConnection, defaultValue);
        }

        protected IEnumerable<T> GetValues<T>(RuntimePort port, int count, T defaultValue) {
            if (count <= 0) yield break;
            Connection? firstConnection = port.Connections.FirstOrDefault();
            if (firstConnection == null) {
                for (int i = 0; i < count; i++) {
                    yield return defaultValue;
                }

                yield break;
            }

            RuntimePort outputPort = firstConnection.Output;
            if (outputPort == null) {
                for (int i = 0; i < count; i++) {
                    yield return defaultValue;
                }

                yield break;
            }

            IEnumerable<object> values = outputPort.Node.GetValuesForPort(outputPort, count);
            foreach (object value in values) {
                if (PortTypeUtility.IsUnmanagedType(outputPort.Type) && value is T tValueUnmanaged) {
                    yield return tValueUnmanaged;
                    continue;
                }

                T tValue = (T)value;
                if (tValue != null) {
                    yield return tValue;
                    continue;
                }

                yield return (T)PortValueConverter.Convert(value, outputPort.Type, firstConnection.Input.Type);
            }
        }

        protected IEnumerable<T> GetValues<T>(RuntimePort port, T defaultValue) {
            if (port.Connections.Count == 0) yield return defaultValue;
            foreach (Connection connection in port.Connections) {
                object value = connection.Output.Node.GetValueForPort(connection.Output);
                if (PortTypeUtility.IsUnmanagedType(connection.Output.Type) && value is T tValueUnmanaged) {
                    yield return tValueUnmanaged;
                    continue;
                }

                T tValue = (T)value;
                if (tValue != null) {
                    yield return tValue;
                    continue;
                }

                yield return (T)PortValueConverter.Convert(value, connection.Output.Type, connection.Input.Type);
            }
        }

        // I probably don't need this method unless I add event methods like `OnConnectionRemoved/Created` but for nodes.
        // Also, I'm pretty sure nodes will already be notified that the connection is removed since deleting a
        // node also deletes the connections to that node so there's no need to do it here again.
        public void OnNodeRemoved() {
            foreach (RuntimePort port in Ports) {
                foreach (Connection connection in port.Connections) {
                    RuntimePort otherPort = port.Direction == PortDirection.Input ? connection.Output : connection.Input;
                    otherPort.Node.NotifyConnectionRemoved(connection, port);
                }
            }
        }
    }
}