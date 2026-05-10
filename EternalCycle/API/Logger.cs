using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EternalCycle
{
    public enum CycleLogLevel { Info, Success, Warn, Error, Debug }

    // 前端调用的实例类
    public class ECLogger
    {
        // ==============================================================
        // 1. 静态后台引擎 (全服唯一，所有实例共享这个队列和线程)
        // ==============================================================
        private static readonly BlockingCollection<LogEntry> _logQueue = new();
        private static readonly CancellationTokenSource _cts = new();
        private static string _logFilePath;
        private static bool _engineStarted = false;
        private static readonly object _lock = new();

        private readonly struct LogEntry
        {
            public readonly string Module;
            public readonly CycleLogLevel Level;
            public readonly string Message;
            public readonly ConsoleColor Color;
            public readonly DateTime Time;
            public readonly bool ShowModuleName;

            public LogEntry(string module, CycleLogLevel level, string msg, ConsoleColor color, bool showModuleName)
            {
                Module = module; Level = level; Message = msg; Color = color; Time = DateTime.Now; ShowModuleName = showModuleName;
            }
        }

        // 初始化静态引擎（只在第一次 new VulcanLogger 的时候触发一次）
        private static void EnsureEngineStarted()
        {
            if (_engineStarted) return;
            lock (_lock)
            {
                if (_engineStarted) return;

                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user", "logs", "EternalCycle");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                // 动态给静态变量赋值路径
                _logFilePath = Path.Combine(logDir, $"EternalCycle_{DateTime.Now:yyyy-MM-dd}.log");

                Task.Factory.StartNew(ProcessQueue, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                _engineStarted = true;
            }
        }

        private static void ProcessQueue()
        {
            var debug = false;
            foreach (var entry in _logQueue.GetConsumingEnumerable(_cts.Token))
            {
                string timeStr = debug ? $"[{entry.Time.ToString("HH:mm:ss")}]" : "";
                //string levelStr = entry.Level.ToString().ToUpper().PadRight(7);
                string outputText = entry.ShowModuleName ? $"{timeStr}[{entry.Module}]{entry.Message}" : $"{timeStr}{entry.Message}";

                lock (Console.Out)
                {
                    Console.ForegroundColor = entry.Color;
                    Console.WriteLine(outputText);
                    Console.ResetColor();
                }

                try { File.AppendAllText(_logFilePath, outputText + Environment.NewLine); }
                catch { /* 忽略文件占用错误 */ }
            }
        }

        private static void Enqueue(string module, CycleLogLevel level, string msg, ConsoleColor color, bool showModuleName)
        {
            if (!_logQueue.IsAddingCompleted) _logQueue.Add(new LogEntry(module, level, msg, color, showModuleName));
        }


        // ==============================================================
        // 2. 实例部分 (每个类自己 new 一个带名字的专属 Logger)
        // ==============================================================

        private readonly string _moduleName;
        private readonly bool _showModuleName;

        // 构造函数：只需传入模块名
        public ECLogger(string moduleName, bool showModuleName)
        {
            EnsureEngineStarted(); // 确保后台线程在干活
            _moduleName = moduleName;
            _showModuleName = showModuleName;
        }

        // 实例方法：自带模块名，爽快调用
        public void Info(string msg) => Enqueue(_moduleName, CycleLogLevel.Info, msg, ConsoleColor.DarkCyan, _showModuleName);
        public void Success(string msg) => Enqueue(_moduleName, CycleLogLevel.Success, msg, ConsoleColor.DarkGreen, _showModuleName);
        public void Warn(string msg) => Enqueue(_moduleName, CycleLogLevel.Warn, msg, ConsoleColor.DarkYellow, _showModuleName);
        public void Error(string msg, Exception ex = null)
        {
            string finalMsg = ex == null ? msg : $"{msg}\n异常: {ex}";
            Enqueue(_moduleName, CycleLogLevel.Error, finalMsg, ConsoleColor.DarkRed, _showModuleName);
        }
    }
}