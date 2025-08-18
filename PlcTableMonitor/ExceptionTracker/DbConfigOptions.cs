namespace PlcTableMonitor.ExceptionTracker
{
    // 数据库配置选项
    public class DbConfigOptions
    {
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
