using System;

namespace SourceGeneratorsExperiment {
    public static partial class Program {
        public static void Main(string[] args) {

            Console.WriteLine(new BetterClampedFloatNode("123").Guid);
        }
    }
}
