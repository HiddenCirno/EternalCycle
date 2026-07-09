using System.Collections.Concurrent;

namespace EternalCycleServer
{
    /// <summary>
    /// 定义日志级别
    /// </summary>
    public enum CycleLogLevel { Info, Success, Warn, Error, Debug }
    /// <summary>
    /// 使用时调用的实例类型
    /// </summary>
    public class ECLogger
    {
        /// <summary>
        /// 静态后台引擎, 全服唯一, 所有实例共享同一个队列和线程
        /// </summary>
        private static readonly BlockingCollection<LogEntry> _logQueue = new();
        private static readonly CancellationTokenSource _cts = new();
        private static string _logFilePath;
        private static bool _engineStarted = false;
        private static readonly object _lock = new();
        /// <summary>
        /// 日志结构定义
        /// </summary>
        private readonly struct LogEntry
        {
            public readonly string Module;
            public readonly CycleLogLevel Level;
            public readonly string Message;
            public readonly ConsoleColor Color;
            public readonly DateTime Time;
            public readonly bool ShowModuleName;
            /// <summary>
            /// 日志结构构造函数
            /// </summary>
            /// <param name="module">模块名</param>
            /// <param name="level">日志等级</param>
            /// <param name="msg">消息</param>
            /// <param name="color">颜色</param>
            /// <param name="showModuleName">是否显示模块名字</param>
            public LogEntry(string module, CycleLogLevel level, string msg, ConsoleColor color, bool showModuleName)
            {
                Module = module; Level = level; Message = msg; Color = color; Time = DateTime.Now; ShowModuleName = showModuleName;
            }
        }
        /// <summary>
        /// 初始化静态引擎, 仅一次
        /// </summary>
        private static void EnsureEngineStarted()
        {
            if (_engineStarted) return;
            lock (_lock)
            {
                if (_engineStarted) return;
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user", "logs", "EternalCycle");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                //动态给静态变量赋值路径
                _logFilePath = Path.Combine(logDir, $"EternalCycle_{DateTime.Now:yyyy-MM-dd}.log");
                Task.Factory.StartNew(ProcessQueue, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                _engineStarted = true;
            }
        }
        /// <summary>
        /// 处理队列
        /// </summary>
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
        /// <summary>
        /// 将日志条目加入队列
        /// </summary>
        /// <param name="module">模块名</param>
        /// <param name="level">日志等级</param>
        /// <param name="msg">消息</param>
        /// <param name="color">颜色</param>
        /// <param name="showModuleName">是否显示模块名字</param>
        private static void Enqueue(string module, CycleLogLevel level, string msg, ConsoleColor color, bool showModuleName)
        {
            if (!_logQueue.IsAddingCompleted) _logQueue.Add(new LogEntry(module, level, msg, color, showModuleName));
        }
        //实例定义
        private readonly string _moduleName;
        private readonly bool _showModuleName;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="moduleName">日志模块名字</param>
        /// <param name="showModuleName">是否显示模块名字</param>
        public ECLogger(string moduleName, bool showModuleName)
        {
            EnsureEngineStarted();
            _moduleName = moduleName;
            _showModuleName = showModuleName;
        }
        //实例方法
        /// <summary>
        /// 正常信息 (青色)
        /// </summary>
        /// <param name="msg">传入日志</param>
        public void Info(string msg) => Enqueue(_moduleName, CycleLogLevel.Info, msg, ConsoleColor.DarkCyan, _showModuleName);
        /// <summary>
        /// 成功信息 (绿色)
        /// </summary>
        /// <param name="msg">传入日志</param>
        public void Success(string msg) => Enqueue(_moduleName, CycleLogLevel.Success, msg, ConsoleColor.DarkGreen, _showModuleName);
        /// <summary>
        /// 警告信息 (黄色)
        /// </summary>
        /// <param name="msg">传入日志</param>
        public void Warn(string msg) => Enqueue(_moduleName, CycleLogLevel.Warn, msg, ConsoleColor.DarkYellow, _showModuleName);
        /// <summary>
        /// 错误信息 (红色)
        /// </summary>
        /// <param name="msg">传入日志</param>
        /// <param name="ex">错误详情, 可为空, 默认为null即无错误</param>
        public void Error(string msg, Exception ex = null)
        {
            string finalMsg = ex == null ? msg : $"{msg}\n异常: {ex}";
            Enqueue(_moduleName, CycleLogLevel.Error, finalMsg, ConsoleColor.DarkRed, _showModuleName);
        }
        /// <summary>
        /// 调试信息 (灰色)
        /// </summary>
        /// <param name="msg">传入日志</param>
        public void Debug(string msg)
        {
            //预留Debug开关
            if(true) Enqueue(_moduleName, CycleLogLevel.Error, msg, ConsoleColor.DarkGray, _showModuleName);
        }
    }
}