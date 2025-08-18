using SqlSugar;

namespace PlcTableMonitor.ExceptionTracker
{
    public static class ExceptionLogger
    {
        private static readonly ExceptionStateTracker _stateTracker = new ExceptionStateTracker();
        /// <summary>
        /// 获取记录异常的SQLite数据库客户端实例
        /// </summary>
        /// <returns></returns>
        private static SqlSugarClient GetDb() =>
            SqlSugarContext.Instance.GetDbClient(DatabaseType.SQLite);

        public static async Task LogExceptionIfNeededAsync(string tableName, string exceptionType, string detail, string? additionalInfo = null)
        {
            if (!_stateTracker.ShouldRecordException(tableName, exceptionType))
            {
                return; // 不记录异常
            }

            var previousRecordId = _stateTracker.GetLastRecordId(tableName, exceptionType);

            var record = new ExceptionRecord
            {
                TableName = tableName,
                ExceptionType = exceptionType,
                ExceptionDetail = detail,
                AdditionalInfo = additionalInfo,
                IsRecovered = false,
                PreviousRecordId = previousRecordId
            };

            var newRecordId = await GetDb().Insertable(record).ExecuteReturnIdentityAsync();
            _stateTracker.UpdateExceptionRecord(tableName, exceptionType, newRecordId);
        }
        public static async Task LogExceptionRecoveryAsync(string tableName, string exceptionType, string recoveryDetail)
        {
            var previousRecordId = _stateTracker.GetLastRecordId(tableName, exceptionType);
            if (!previousRecordId.HasValue)
            {
                return; // 没有记录过该异常
            }

            // 记录恢复信息
            var recoveryRecord = new ExceptionRecord
            {
                TableName = tableName,
                ExceptionType = exceptionType,
                ExceptionDetail = recoveryDetail,
                IsRecovered = true,
                PreviousRecordId = previousRecordId
            };

            await GetDb().Insertable(recoveryRecord).ExecuteCommandAsync();

            // 标记原异常记录为已恢复
            await GetDb().Updateable<ExceptionRecord>()
                 .SetColumns(r => r.IsRecovered, true)
                 .Where(r => r.Id == previousRecordId)
                 .ExecuteCommandAsync();

            _stateTracker.MarkExceptionRecovered(tableName, exceptionType);
        }
        public static async Task<bool> HasUncoveredExceptionAsync(string tableName, string exceptionType)
        {
            return await GetDb().Queryable<ExceptionRecord>()
                .Where(r => r.TableName == tableName && r.ExceptionType == exceptionType && !r.IsRecovered)
                .AnyAsync();
        }
        public static async Task<List<ExceptionRecord>> GetActiveExceptionsAsync()
        {
            return await GetDb().Queryable<ExceptionRecord>()
                .Where(r => !r.IsRecovered)
                .OrderBy(r => r.RecordTime, SqlSugar.OrderByType.Desc)
                .ToListAsync();
        }
        public static async Task<List<ExceptionRecord>> GetRecentExceptionsAsync(int hours = 24)
        {
            var cutoffTime = DateTime.Now.AddHours(-hours);
            return await GetDb().Queryable<ExceptionRecord>()
                .Where(r => r.RecordTime >= cutoffTime)
                .OrderBy(r => r.RecordTime, SqlSugar.OrderByType.Desc)
                .ToListAsync();
        }

        public static void LogExceptionIfNeeded(string tableName, string exceptionType, string detail, string? additionalInfo = null)
        {
            if (!_stateTracker.ShouldRecordException(tableName, exceptionType))
            {
                return; // 不记录异常
            }

            var previousRecordId = _stateTracker.GetLastRecordId(tableName, exceptionType);

            var record = new ExceptionRecord
            {
                TableName = tableName,
                ExceptionType = exceptionType,
                ExceptionDetail = detail,
                AdditionalInfo = additionalInfo,
                IsRecovered = false,
                PreviousRecordId = previousRecordId
            };

            var newRecordId = GetDb().Insertable(record).ExecuteReturnIdentity();
            _stateTracker.UpdateExceptionRecord(tableName, exceptionType, newRecordId);
        }
        public static void LogExceptionRecovery(string tableName, string exceptionType, string recoveryDetail)
        {
            var previousRecordId = _stateTracker.GetLastRecordId(tableName, exceptionType);
            if (!previousRecordId.HasValue)
            {
                return; // 没有记录过该异常
            }

            // 记录恢复信息
            var recoveryRecord = new ExceptionRecord
            {
                TableName = tableName,
                ExceptionType = exceptionType,
                ExceptionDetail = recoveryDetail,
                IsRecovered = true,
                PreviousRecordId = previousRecordId
            };

            GetDb().Insertable(recoveryRecord).ExecuteCommand();

            // 标记原异常记录为已恢复
            GetDb().Updateable<ExceptionRecord>()
                .SetColumns(r => r.IsRecovered, true)
                .Where(r => r.Id == previousRecordId)
                .ExecuteCommand();

            _stateTracker.MarkExceptionRecovered(tableName, exceptionType);
        }
        public static bool HasUncoveredException(string tableName, string exceptionType)
        {
            return GetDb().Queryable<ExceptionRecord>()
                .Where(r => r.TableName == tableName && r.ExceptionType == exceptionType && !r.IsRecovered)
                .Any();
        }
        public static List<ExceptionRecord> GetActiveExceptions()
        {
            return GetDb().Queryable<ExceptionRecord>()
                .Where(r => !r.IsRecovered)
                .OrderBy(r => r.RecordTime, SqlSugar.OrderByType.Desc)
                .ToList();
        }
        public static List<ExceptionRecord> GetRecentExceptions(int hours = 24)
        {
            var cutoffTime = DateTime.Now.AddHours(-hours);
            return GetDb().Queryable<ExceptionRecord>()
                .Where(r => r.RecordTime >= cutoffTime)
                .OrderBy(r => r.RecordTime, SqlSugar.OrderByType.Desc)
                .ToList();
        }
    }
}
