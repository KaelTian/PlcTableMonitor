namespace PlcTableMonitor.Configs
{
    public class AppSettings
    {
        public List<string>? TableNames { get; set; }
        public List<string>? AlarmTables { get; set; }
        public int QueryMaxCount { get; set; }
        public int TimerInterval { get; set; }
        /// <summary>
        /// 分析源数据的MySQL数据库连接字符串
        /// </summary>
        public string? MySqlConnectionString { get; set; }
        /// <summary>
        /// 记录异常的SQLite数据库连接字符串
        /// </summary>
        public string? SqliteConnectionString { get; set; }
    }
}
