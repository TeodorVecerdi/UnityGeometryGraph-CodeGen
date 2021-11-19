namespace SourceGenerator {
    public class GetterMethod {
        public string MethodName { get; }
        public bool Inline { get; }
        public string Body { get; }
        public bool HasExpressionBody { get; }

        public GetterMethod(string methodName, bool inline, string body, bool hasExpressionBody) {
            MethodName = methodName;
            Inline = inline;
            Body = body;
            HasExpressionBody = hasExpressionBody;
        }
    }
}