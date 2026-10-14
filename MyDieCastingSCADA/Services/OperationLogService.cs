using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using MySCADA.Services;
namespace MyDieCastingSCADA.Services
{
    public sealed class OperationLogEntry
    {
        public DateTime Time { get; init; }
        public string Level { get; init; } = "信息";
        public string Operation { get; init; } = string.Empty;
        public string Result { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string TimeText => Time.ToString("yyyy-MM-dd HH:mm:ss");
    }
        public static class OperationLogService
    {
        private const int MaxEntries = 1000;
        private static readonly object FileLock = new();
        private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        public static ObservableCollection<OperationLogEntry> Entries { get; } = new();
        static OperationLogService()
        {
            Directory.CreateDirectory(LogDirectory);
            LoadToday();
        }
        public static void Info(string operation, string result, string detail = "") =>
            Add("信息", operation, result, detail);
        public static void Success(string operation, string result, string detail = "") =>
            Add("成功", operation, result, detail);
        public static void Warning(string operation, string result, string detail = "") =>
            Add("警告", operation, result, detail);
        public static void Error(string operation, string result, string detail = "") =>
            Add("错误", operation, result, detail);
        public static void Clear()
        {
            RunOnUiThread(() => Entries.Clear());
        }
        public static IReadOnlyList<OperationLogEntry> ReadDate(DateTime date)
        {
            string file=Path.Combine(LogDirectory,$"{date:yyyy-MM-dd}.log");
            if(!File.Exists(file)) return Array.Empty<OperationLogEntry>();
            var result=new List<OperationLogEntry>();
            try
            {
                lock(FileLock)
                {
                    foreach(string line in File.ReadLines(file).Where(x=>!string.IsNullOrWhiteSpace(x)).TakeLast(MaxEntries).Reverse())
                    {
                        var entry=JsonSerializer.Deserialize<OperationLogEntry>(line);
                        if(entry!=null) result.Add(entry);
                    }
                }
            }
            catch { }
            return result;
        }
        private static void Add(string level, string operation, string result, string detail)
        {
            var entry = new OperationLogEntry
            {
                Time = DateTime.Now,
                Level = level,
                Operation = operation,
                Result = result,
                Detail = detail,
                UserName = AuthorizationService.CurrentUser?.Username ?? "SYSTEM"
            };
            RunOnUiThread(() =>
            {
                Entries.Insert(0, entry);
                while (Entries.Count > MaxEntries)
                    Entries.RemoveAt(Entries.Count - 1);
            });
            try
            {
                lock (FileLock)
                    File.AppendAllText(GetTodayFile(), JsonSerializer.Serialize(entry) + Environment.NewLine);
            }
            catch
            {
            }
        }
        private static void LoadToday()
        {
            string file = GetTodayFile();
            if (!File.Exists(file)) return;
            try
            {
                foreach (string line in File.ReadLines(file).Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(MaxEntries).Reverse())
                {
                    OperationLogEntry? entry = JsonSerializer.Deserialize<OperationLogEntry>(line);
                    if (entry != null) Entries.Add(entry);
                }
            }
            catch
            {
            }
        }
        private static string GetTodayFile() => Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
    }
}
