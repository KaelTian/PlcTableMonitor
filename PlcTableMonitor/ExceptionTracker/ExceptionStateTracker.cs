using System.Collections.Concurrent;

namespace PlcTableMonitor.ExceptionTracker
{
    /// <summary>
    /// 跟踪每个表的异常状态和最后记录时间
    /// </summary>
    public class ExceptionStateTracker
    {
        /// <summary>
        /// 缓存每张表的异常状态和最后记录时间
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTime>> _lastRecordTimes =
               new ConcurrentDictionary<string, ConcurrentDictionary<string, DateTime>>();
        /// <summary>
        /// 缓存每张表的异常状态和最后记录ID
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _lastRecordIds =
            new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();
        /// <summary>
        /// 最小记录周期
        /// </summary>
        private readonly TimeSpan _minRecordInterval = TimeSpan.FromMinutes(10);

        public bool ShouldRecordException(string tableName, string exceptionType)
        {
            // 获取或创建内层字典
            var innerTimesDict = _lastRecordTimes.GetOrAdd(tableName,
                _ => new ConcurrentDictionary<string, DateTime>());
            var innerIdsDict = _lastRecordIds.GetOrAdd(tableName,
                _ => new ConcurrentDictionary<string, int>());

            // 如果没有记录过该异常类型，应该记录
            if (!innerTimesDict.TryGetValue(exceptionType, out var lastRecordTime))
            {
                return true;
            }

            // 如果超过了最小记录间隔，应该记录
            return DateTime.Now - lastRecordTime >= _minRecordInterval;
        }

        public void UpdateExceptionRecord(string tableName, string exceptionType, int recordId)
        {
            // 获取或创建内层字典
            var innerTimesDict = _lastRecordTimes.GetOrAdd(tableName,
                _ => new ConcurrentDictionary<string, DateTime>());
            var innerIdsDict = _lastRecordIds.GetOrAdd(tableName,
                _ => new ConcurrentDictionary<string, int>());
            // 更新最后记录时间和ID
            innerTimesDict.AddOrUpdate(exceptionType, DateTime.Now, (key, oldValue) => DateTime.Now);
            innerIdsDict.AddOrUpdate(exceptionType, recordId, (key, oldValue) => recordId);
        }

        public int? GetLastRecordId(string tableName, string exceptionType)
        {
            if (_lastRecordIds.TryGetValue(tableName, out var innerDict) &&
                innerDict.TryGetValue(exceptionType, out var recordId))
            {
                return recordId;
            }
            return null;
        }

        public void MarkExceptionRecovered(string tableName, string exceptionType)
        {
            // 清除异常状态和最后记录时间
            if (_lastRecordTimes.TryGetValue(tableName, out var innerTimesDict))
            {
                innerTimesDict.TryRemove(exceptionType, out _);
            }
            if (_lastRecordIds.TryGetValue(tableName, out var innerIdsDict))
            {
                innerIdsDict.TryRemove(exceptionType, out _);
            }
        }
    }
}
