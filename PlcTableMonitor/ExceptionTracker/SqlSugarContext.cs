using SqlSugar;

namespace PlcTableMonitor.ExceptionTracker
{
    /// <summary>
    /// 数据库类型枚举
    /// </summary>
    public enum DatabaseType
    {
        MySQL,
        SQLite
    }

    public class SqlSugarContext
    {
        // 单例实例
        private static readonly Lazy<SqlSugarContext> _instance = new Lazy<SqlSugarContext>(() => new SqlSugarContext());

        // 数据库连接配置
        private ConnectionConfig? _mySqlConfig;
        private ConnectionConfig? _sqliteConfig;

        // SqlSugar 实例
        private SqlSugarScope? _sqlSugar;

        // 初始化标志
        private bool _isInitialized = false;
        private readonly object _lockObj = new object();

        //  私有构造函数
        private SqlSugarContext() { }

        // 单例实例访问器
        public static SqlSugarContext Instance => _instance.Value;

        // 初始化配置 (拱外部调用)
        public void Initialize(DbConfigOptions options)
        {
            if (_isInitialized) return;

            lock (_lockObj)
            {
                if (_isInitialized) return;
                // 初始化 MySQL 配置
                if (!string.IsNullOrEmpty(options.MySqlConnectionString))
                {
                    _mySqlConfig = new ConnectionConfig
                    {
                        ConnectionString = options.MySqlConnectionString,
                        DbType = DbType.MySql,
                        IsAutoCloseConnection = true,
                        ConfigureExternalServices = new ConfigureExternalServices(),
                        ConfigId ="MySQLConnection1"
                    };
                }
                // 初始化 SQLite 配置
                if (!string.IsNullOrEmpty(options.SqliteConnectionString))
                {
                    _sqliteConfig = new ConnectionConfig
                    {
                        ConnectionString = options.SqliteConnectionString,
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true,
                        ConfigId="SQLiteConnection1"
                    };
                }

                // 初始化 SqlSugar 实例
                _sqlSugar = new SqlSugarScope(new List<ConnectionConfig> { _mySqlConfig!, _sqliteConfig! }, db =>
                {
                    //// 配置AOP
                    //// to do
                    //db.Aop.OnLogExecuting = (sql, pars) =>
                    //{
                    //    Console.WriteLine($"SQL: {sql}");
                    //};

                    // 初始化SQLite表结构
                    if (db.CurrentConnectionConfig.DbType == DbType.Sqlite)
                    {
                        InitSqliteTables(db);
                    }
                });

                _isInitialized = true;
            }
        }

        // 获取指定类型的数据库连接 (线程安全)
        public SqlSugarClient GetDbClient(DatabaseType type)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("请先调用Initialize方法初始化配置");
            }

            // 多线程环境下使用CopyNew()创建新实例，避免并发问题
            return type switch
            {
                DatabaseType.MySQL => _sqlSugar!.GetConnection(_mySqlConfig!.ConfigId).CopyNew(),
                DatabaseType.SQLite => _sqlSugar!.GetConnection(_sqliteConfig!.ConfigId).CopyNew(),
                _ => throw new ArgumentException("不支持的数据库类型", nameof(type))
            };
        }

        // 初始化SQLite表和索引
        private void InitSqliteTables(SqlSugarClient db)
        {
            db.CodeFirst.InitTables(typeof(ExceptionRecord));

            // 创建索引（如果不存在）
            if (!db.DbMaintenance.IsAnyIndex("IX_ExceptionRecords_TableName"))
            {
                db.DbMaintenance.CreateIndex("ExceptionRecords",
                    new[] { "TableName" }, "IX_ExceptionRecords_TableName");
            }
            if (!db.DbMaintenance.IsAnyIndex("IX_ExceptionRecords_RecordTime"))
            {
                db.DbMaintenance.CreateIndex("ExceptionRecords",
                    new[] { "RecordTime" }, "IX_ExceptionRecords_RecordTime");
            }
            if (!db.DbMaintenance.IsAnyIndex("IX_ExceptionRecords_Table_Type_Status"))
            {
                db.DbMaintenance.CreateIndex("ExceptionRecords",
                    new[] { "TableName", "ExceptionType", "IsRecovered" }, "IX_ExceptionRecords_Table_Type_Status");
            }
        }
    }
}
