using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SourceGenerator {
    public static class Logger {
        public static bool Enabled = false;
        private static bool isInitialized;
        private static string logFilePath;
        private static StreamWriter logFile;

        public static void LogFull(string message, [CallerMemberName] string m = "", [CallerFilePath] string fp = "", [CallerLineNumber] int l = 0) {
            if (!Enabled) return;
            if (!isInitialized) {
                isInitialized = true;
                InitializeLogger();
            }
            
            logFile.WriteLine($"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {m}: {fp}:{l} {message}");
        }
        
        public static void Log(string message) {
            if (!Enabled) return;
            if (!isInitialized) {
                isInitialized = true;
                InitializeLogger();
            }
            
            logFile.WriteLine($"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {message}");
        }

        private static void InitializeLogger() {
            if (!Enabled) return;
            // logFilePath = Path.Combine("C:/dev/dotnet/SourceGeneratorsExperiment/logs", $"{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.log");
            logFilePath = Path.Combine("C:/dev/dotnet/SourceGeneratorsExperiment/logs", $"log.log");
            logFile = File.CreateText(logFilePath);
            logFile.AutoFlush = true;
        }
    }
}