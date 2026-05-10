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

            public LogEntry(string module, CycleLogLevel level, string msg, ConsoleColor color)
            {
                Module = module; Level = level; Message = msg; Color = color; Time = DateTime.Now;
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
            foreach (var entry in _logQueue.GetConsumingEnumerable(_cts.Token))
            {
                string timeStr = entry.Time.ToString("HH:mm:ss");
                string levelStr = entry.Level.ToString().ToUpper().PadRight(7);
                string outputText = $"[{timeStr}] {levelStr} [{entry.Module}] {entry.Message}";

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

        private static void Enqueue(string module, CycleLogLevel level, string msg, ConsoleColor color)
        {
            if (!_logQueue.IsAddingCompleted) _logQueue.Add(new LogEntry(module, level, msg, color));
        }


        // ==============================================================
        // 2. 实例部分 (每个类自己 new 一个带名字的专属 Logger)
        // ==============================================================

        private readonly string _moduleName;

        // 构造函数：只需传入模块名
        public ECLogger(string moduleName)
        {
            EnsureEngineStarted(); // 确保后台线程在干活
            _moduleName = moduleName;
        }

        // 实例方法：自带模块名，爽快调用
        public void Info(string msg) => Enqueue(_moduleName, CycleLogLevel.Info, msg, ConsoleColor.Cyan);
        public void Success(string msg) => Enqueue(_moduleName, CycleLogLevel.Success, msg, ConsoleColor.Green);
        public void Warn(string msg) => Enqueue(_moduleName, CycleLogLevel.Warn, msg, ConsoleColor.Yellow);
        public void Error(string msg, Exception ex = null)
        {
            string finalMsg = ex == null ? msg : $"{msg}\n异常: {ex}";
            Enqueue(_moduleName, CycleLogLevel.Error, finalMsg, ConsoleColor.Red);
        }
    }
}