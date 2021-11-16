using System;

namespace SourceGeneratorsExperiment {
    public static class Program {
        public static void Main(string[] args) {
            // Console.WriteLine(new BetterClampedFloatNode("123").Guid);
            // BetterClampFloatNodeStatic.Create("123");
            // new BetterClampedFloatNode("123").UpdateMin(1.5f);
            var betterClampedFloatNode = new FullClampFloatNode("123");
            
        }
    }
}
