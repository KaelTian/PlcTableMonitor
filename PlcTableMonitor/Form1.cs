using PlcTableMonitor.Configs;
using PlcTableMonitor.ExceptionTracker;
using SqlSugar;
using System.Collections.Concurrent;
using System.Data;
using System.Text;
using DbType = SqlSugar.DbType;

namespace PlcTableMonitor
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// 定时器对象，用于定时查询数据库表信息
        /// </summary>
        private System.Windows.Forms.Timer? _timer;
        /// <summary>
        /// 缓存行数计数字典，记录每张表的行数
        /// </summary>
        private Dictionary<string, long> _cacheCountDic = new Dictionary<string, long>();
        /// <summary>
        /// 查询任务列表，存储每个表的查询任务
        /// </summary>
        private ConcurrentBag<Task<(DataRow?, TimeSpan, string)>> _queryTasks = new ConcurrentBag<Task<(DataRow?, TimeSpan, string)>>();
        /// <summary>
        /// 定时器取消令牌源，用于控制定时器的取消
        /// </summary>
        private CancellationTokenSource? _cts;
        /// <summary>
        /// 监控的表名列表
        /// </summary>
        private readonly List<string> _tableNames;
        //    = new List<string>
        //{
        //    "jj_alarm_records",
        //    "jj_plc_alarm",
        //    "jj_plc_curve_chart",
        //    "jj_plc_eq_io",
        //    "jj_plc_glass_qr_code",
        //    "jj_plc_product",
        //    "jj_plc_request_control",
        //    "jj_plc_summary",
        //    "jj_plc_system_settings",
        //    "xyh_plc_basic",
        //    "xyh_plc_curvechart",
        //    "xyh_plc_glass_qr_code",
        //    "xyh_plc_loading_and_unloading",
        //    "xyh_plc_product",
        //    "xyh_plc_system_settings",
        //    "xyh_alarm_records"
        //};
        /// <summary>
        /// 特殊处理的报警类型的表名列表
        /// </summary>
        private readonly List<string> _alarmTables;
        //    = new List<string>
        //{
        //    "jj_alarm_records",
        //    "jj_plc_alarm",
        //    "xyh_alarm_records"
        //};
        /// <summary>
        /// 并发执行最大批次数量，避免过多并发查询导致性能问题
        /// </summary>
        private readonly int _queryMaxCount;
        //= 5;
        /// <summary>
        /// Timer轮询周期，单位为毫秒
        /// </summary>
        private readonly int _timerInterval;
        //= 10_000;// 轮询周期
        /// <summary>
        /// 数据库连接字符串，使用 MySQL 数据库
        /// </summary>
        private readonly string _mySqlConnectionString;
        /// <summary>
        /// SQLite 数据库连接字符串，用于记录异常信息
        /// </summary>
        private readonly string _sqliteConnectionString;
        //= "Server=192.168.0.189;Database=005_mes;Uid=root;Pwd=root;charset=utf8;sslMode=None;pooling=true;minpoolsize=1;maxpoolsize=1024;ConnectionLifetime=30;DefaultCommandTimeout=600;AllowUserVariables=true;";
        public Form1()
        {
            // 从配置文件加载设置
            var appSettings = ConfigurationHelper.GetAppSettings();
            _tableNames = appSettings.TableNames!;
            _alarmTables = appSettings.AlarmTables!;
            _queryMaxCount = appSettings.QueryMaxCount;
            _timerInterval = appSettings.TimerInterval;
            _mySqlConnectionString = appSettings.MySqlConnectionString!;
            _sqliteConnectionString = appSettings.SqliteConnectionString!;

            InitializeComponent();
            InitCacheSettings();
            InitDb();
            InitGrid();
            InitTimer();
            SetButtonsEnabledByMonitorState(false);
            pictureBoxStatus.Image = CreateStatusLight(Color.Gray);       // 未激活状态
            toolStripStatusLabel4.Text = $"轮询周期: {FormatPollingInterval(_timerInterval)}";
            toolStripStatusLabel4.BackColor = Color.LightGreen;
        }

        private void InitCacheSettings()
        {
            // to do: 通过配置文件读取表名，数据库连接字符串，轮询周期时间
            if (_cacheCountDic.Count > 0)
            {
                _cacheCountDic.Clear();
            }
            foreach (var key in _tableNames)
            {
                _cacheCountDic.Add(key, 0);
            }
        }
        /// <summary>
        /// 初始化数据库
        /// </summary>
        private void InitDb()
        {
            SqlSugarContext.Instance.Initialize(new DbConfigOptions
            {
                MySqlConnectionString = _mySqlConnectionString,
                SqliteConnectionString = _sqliteConnectionString
            });
        }
        /// <summary>
        /// 获取源端数据库连接
        /// </summary>
        /// <returns></returns>
        private SqlSugarClient GetDb()
        {
            // 获取 MySQL 数据库连接
            return SqlSugarContext.Instance.GetDbClient(DatabaseType.MySQL);
        }

        private void InitGrid()
        {
            // 清除现有列（防止重复初始化）
            dataGridView1.Columns.Clear();

            // 添加列
            dataGridView1.Columns.Add("TableName", "表名");
            dataGridView1.Columns.Add("RowCount", "当前行数");
            dataGridView1.Columns.Add("IncreaseCount", "增长行数");
            dataGridView1.Columns.Add("LastTime", "最新数据时间");
            dataGridView1.Columns.Add("DiffSpan", "距离当前耗时");
            dataGridView1.Columns.Add("QuerySpan", "查询耗时");
            dataGridView1.Columns.Add("CurrentTime", "当前时间");

            // 禁止手动添加行
            dataGridView1.AllowUserToAddRows = false;

            // 禁止编辑单元格
            dataGridView1.ReadOnly = true;

            // 禁止排序（可选）
            foreach (DataGridViewColumn column in dataGridView1.Columns)
            {
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            // 设置列宽比例（百分比）
            dataGridView1.Columns["TableName"].FillWeight = 20;    // 表名占25%
            dataGridView1.Columns["RowCount"].FillWeight = 10;     // 当前行数占15%
            dataGridView1.Columns["IncreaseCount"].FillWeight = 10; // 增长行数占15%
            dataGridView1.Columns["LastTime"].FillWeight = 20;     // 最新时间占25%
            dataGridView1.Columns["DiffSpan"].FillWeight = 10;         // 距离当前时间占20%
            dataGridView1.Columns["QuerySpan"].FillWeight = 10;         // 距离当前时间占20%
            dataGridView1.Columns["CurrentTime"].FillWeight = 20;         // 距离当前时间占20%

            // 自动调整列宽模式
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // ✅ 启用行头显示（行号）
            dataGridView1.RowHeadersVisible = true;

            // ✅ 订阅 RowPostPaint 事件，绘制行号
            dataGridView1.RowPostPaint += DataGridView1_RowPostPaint;
        }

        private void InitTimer()
        {
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = _timerInterval; // 10秒
            _timer.Tick += async (s, e) =>
            {
                _timer.Stop();
                InitCancellation();
                try
                {
                    await RefreshTableInfosAsync(_cts!.Token);
                }
                finally
                {
                    _timer.Start();
                }
            };
        }

        private void DataGridView1_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
        {
            // 获取当前行索引（从1开始）
            var rowIndex = (e.RowIndex + 1).ToString();

            // 设置行号文本格式
            var centerFormat = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // 获取行头区域
            var headerBounds = new Rectangle(
                e.RowBounds.Left,
                e.RowBounds.Top,
                dataGridView1.RowHeadersWidth,
                e.RowBounds.Height);

            // 绘制行号文本
            e.Graphics.DrawString(
                rowIndex,
                dataGridView1.Font,
                SystemBrushes.ControlText,
                headerBounds,
                centerFormat);
        }

        private async Task StartMonitoringAsync()
        {
            InitCancellation();
            // 启动立即执行一次
            await RefreshTableInfosAsync(_cts!.Token);
            // 启动timer
            StartTimer();
        }
        /// <summary>
        /// 刷新数据方法，异步查询每张表的行数和最新创建时间，并更新DataGridView
        /// </summary>
        /// <returns></returns>
        private async Task RefreshTableInfosAsync(CancellationToken token)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // 并行查询每张表
                List<(DataRow?, TimeSpan, string)> queryResults = new List<(DataRow?, TimeSpan, string)>();
                foreach (var tableName in _tableNames)
                {
                    token.ThrowIfCancellationRequested(); // 每次循环前检查是否取消
                    _queryTasks.Add(QueryTableInfoAsync(tableName, token));
                    if (_queryTasks.Count == _queryMaxCount)
                    {
                        // 等待当前批次完成，但如果取消则退出
                        queryResults.AddRange(await Task.WhenAll(_queryTasks));
                        _queryTasks.Clear();
                    }
                }
                // 等待剩余任务完成
                if (_queryTasks.Any())
                {
                    queryResults.AddRange(await Task.WhenAll(_queryTasks));
                    _queryTasks.Clear();
                }
                // 仅当未取消时更新 UI
                if (!token.IsCancellationRequested && queryResults.Any())
                {
                    if (dataGridView1.InvokeRequired)
                        dataGridView1.Invoke(new Action(async () => await UpdateDataGridSourceAsync(queryResults)));
                    else
                        await UpdateDataGridSourceAsync(queryResults);
                }
            }
            catch (OperationCanceledException)
            {
                // 取消是正常行为，无需处理
                // to do: add logs for cancellation
                ClearDataGridSource();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新数据失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                stopwatch.Stop();
                // 更新UI显示执行时间
                UpdateExecutionTime(stopwatch.Elapsed);
            }
        }
        /// <summary>
        /// 查询单个表的行数和最新创建时间，并返回查询结果和耗时
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private async Task<(DataRow?, TimeSpan, string)> QueryTableInfoAsync(string tableName, CancellationToken token)
        {
            var queryStartTime = DateTime.Now;
            var sql = $"SELECT '{tableName}' AS 表名, MAX(ID) AS 行数, MAX(CreateTime) AS 最新创建时间 FROM {tableName}";

            try
            {
                // 虽然 GetDataTableAsync 不支持取消，但可以用 Task.Run 包裹以便响应外部取消
                //var dt = await Task.Run(() =>
                //{
                //    token.ThrowIfCancellationRequested(); // 检查是否已取消
                //    return GetDb().Ado.GetDataTable(sql); // 同步查询（无法取消，但可以提前退出 Task）
                //}, token);
                var dt = await GetDb().Ado.GetDataTableAsync(sql);

                return (dt?.Rows.Count > 0 ? dt.Rows[0] : null, DateTime.Now - queryStartTime, tableName);
            }
            catch (OperationCanceledException)
            {
                return (null, DateTime.Now - queryStartTime, tableName); // 返回空结果，但记录耗时
            }
        }
        /// <summary>
        /// 更新DataGridView的数据源
        /// </summary>
        /// <param name="results"></param>
        private async Task UpdateDataGridSourceAsync(IEnumerable<(DataRow?, TimeSpan, string)>? results)
        {
            ClearDataGridSource();

            if (results == null) return;

            foreach (var (row, queryTime, tableName) in results.OrderBy(a => a.Item3))
            {
                if (row == null) continue;

                string table = row["表名"].ToString()!;
                long rowCount = Convert.ToInt64(row["行数"]);
                long diffCount = rowCount - (_cacheCountDic.ContainsKey(table) ? _cacheCountDic[table] : 0);
                _cacheCountDic[table] = rowCount; // 更新缓存行数
                DateTime? lastTime = row["最新创建时间"] as DateTime?;

                DateTime currentTime = DateTime.Now;
                // 修正时间差计算：减去查询耗时
                var diff = lastTime.HasValue
                    ? (currentTime - queryTime) - lastTime.Value
                    : TimeSpan.MaxValue;

                int rowIndex = dataGridView1.Rows.Add(
                    table,
                    rowCount,
                    diffCount,
                    lastTime?.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    FormatElapsedTime(diff),
                    FormatElapsedTime(queryTime),
                    currentTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                if (IsNormalTable(tableName))
                {
                    SetNormalRowStyle(dataGridView1.Rows[rowIndex], diff);
                    await TryToRecordExceptionNormalTableAsync(tableName, diff, lastTime);
                }
                else if (IsAlarmTable(tableName))
                {
                    SetAlarmRowStyle(dataGridView1.Rows[rowIndex], diff);
                    await TryToRecordExceptionAlarmTableAsync(tableName, diff, lastTime);
                }
            }

            if (dataGridView1.Rows.Count == 0)
            {
                MessageBox.Show("没有数据", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        /// <summary>
        /// 设置普通表行样式
        /// 状态颜色，根据时间差设置行的背景色
        /// </summary>
        /// <param name="row"></param>
        /// <param name="diff"></param>
        private void SetNormalRowStyle(DataGridViewRow row, TimeSpan diff)
        {
            Color color = Color.White;
            if (diff.TotalMinutes < 5)
                color = Color.LightGreen;
            else if (diff.TotalMinutes < 30)
                color = Color.Khaki;
            else
                color = Color.LightCoral;
            row.DefaultCellStyle.BackColor = color;
        }
        private async Task TryToRecordExceptionNormalTableAsync(string tableName, TimeSpan diff, DateTime? lastTime)
        {
            if (diff.TotalMinutes < 5)
            {
                await ExceptionLogger.LogExceptionRecoveryAsync(tableName, "NoWrite", "表已恢复写入");
            }
            else if (diff.TotalMinutes < 30)
            {
                await ExceptionLogger.LogExceptionIfNeededAsync(
                    tableName: tableName,
                    exceptionType: "NoWrite",
                    detail: "该表超过5分钟没有新数据写入",
                    additionalInfo: $"最后写入时间: {lastTime?.ToString("yyyy-MM-dd HH:mm:ss")}");
            }
            else
            {
                await ExceptionLogger.LogExceptionIfNeededAsync(
                    tableName: tableName,
                    exceptionType: "NoWrite",
                    detail: "该表超过30分钟没有新数据写入",
                    additionalInfo: $"最后写入时间: {lastTime?.ToString("yyyy-MM-dd HH:mm:ss")}");
            }
        }
        /// <summary>
        /// 设置报警表行样式
        /// 状态颜色，根据时间差设置行的背景色
        /// </summary>
        /// <param name="row"></param>
        /// <param name="diff"></param>
        private void SetAlarmRowStyle(DataGridViewRow row, TimeSpan diff)
        {
            Color color = Color.White;
            if (diff.TotalMinutes < 10)
                color = Color.LightCoral;
            else if (diff.TotalMinutes < 60)
                color = Color.Khaki;
            else
                color = Color.LightGreen;
            row.DefaultCellStyle.BackColor = color;
        }
        private async Task TryToRecordExceptionAlarmTableAsync(string tableName, TimeSpan diff, DateTime? lastTime)
        {
            if (diff.TotalMinutes >= 60)
            {
                await ExceptionLogger.LogExceptionRecoveryAsync(
                    tableName,
                    "Write",
                    "报警表超过一小时不再写入数据");
            }
            else if (diff.TotalMinutes >= 10)
            {
                await ExceptionLogger.LogExceptionRecoveryAsync(
                     tableName: tableName,
                     exceptionType: "Write",
                     recoveryDetail: "该报警表超过10分钟没有新数据写入");
            }
            else
            {
                await ExceptionLogger.LogExceptionIfNeededAsync(
                     tableName: tableName,
                     exceptionType: "Write",
                     detail: "该报警表10分钟内有新数据写入",
                     additionalInfo: $"最后写入时间: {lastTime?.ToString("yyyy-MM-dd HH:mm:ss")}");
            }
        }

        private bool IsNormalTable(string tableName)
        {
            // 判断是否为普通表（非报警表）
            return !_alarmTables.Contains(tableName);
        }

        private bool IsAlarmTable(string tableName)
        {
            // 判断是否为报警表
            return _alarmTables.Contains(tableName);
        }

        /// <summary>
        /// 清空DataGridView的数据源
        /// </summary>
        private void ClearDataGridSource()
        {
            // 清空原有数据源
            // ✅ 先检查句柄是否已创建
            if (dataGridView1.IsHandleCreated &&
                dataGridView1.InvokeRequired &&
                dataGridView1.Rows.Count > 0)
            {
                dataGridView1.Invoke(new Action(() => dataGridView1.Rows.Clear()));
            }
            else if (!dataGridView1.InvokeRequired &&
                dataGridView1.Rows.Count > 0)
            {
                dataGridView1.Rows.Clear();
            }
        }
        /// <summary>
        /// 启动定时器方法，开始监控数据变化
        /// </summary>
        private void StartTimer()
        {
            _timer?.Start();
        }
        /// <summary>
        /// 停止定时器方法，停止监控数据变化
        /// </summary>
        private void StopTimer()
        {
            _timer?.Stop();
        }

        private void CancelledTasks()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        private void InitCancellation()
        {
            if (_cts != null)
            {
                _cts.Dispose();
            }
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// 设置按钮的启用状态，根据是否正在监控来决定
        /// </summary>
        /// <param name="isMonitor"></param>
        private void SetButtonsEnabledByMonitorState(bool isMonitor)
        {
            if (startMonitorBtn.Enabled == isMonitor)
                startMonitorBtn.Enabled = !isMonitor;
            if (stopMonitorBtn.Enabled != isMonitor)
                stopMonitorBtn.Enabled = isMonitor;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 停止定时器
            StopTimer();
            // 取消所有查询任务
            CancelledTasks();
            // 释放数据库连接
            GetDb()?.Dispose();
        }

        // 创建一个动态绘制的方法
        private Bitmap CreateStatusLight(Color color, int size = 32)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(color), 0, 0, size - 1, size - 1);
                g.DrawEllipse(Pens.DarkGray, 0, 0, size - 1, size - 1);
            }
            return bmp;
        }
        /// <summary>
        /// 启动监控按钮点击事件处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void startMonitorBtn_Click(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "状态: 运行中...";
            toolStripStatusLabel1.BackColor = Color.LightGreen;
            pictureBoxStatus.Image = CreateStatusLight(Color.LimeGreen);  // 运行状态
            SetButtonsEnabledByMonitorState(true);
            await StartMonitoringAsync();
        }

        /// <summary>
        /// 停止监控按钮点击事件处理程序
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void stopMonitorBtn_Click(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "状态: 已停止";
            toolStripStatusLabel1.BackColor = Color.LightCoral;
            pictureBoxStatus.Image = CreateStatusLight(Color.Red);        // 停止状态
            StopTimer();
            CancelledTasks();
            ClearDataGridSource();
            SetButtonsEnabledByMonitorState(false);
        }

        private void UpdateExecutionTime(TimeSpan elapsed)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<TimeSpan>(UpdateExecutionTime), elapsed);
                return;
            }

            // 刷新时间
            toolStripStatusLabel2.Text = $"最后刷新: {DateTime.Now:HH:mm:ss}";

            // 耗时
            toolStripStatusLabel3.Text = $"耗时: {FormatElapsedTime(elapsed)}";
        }

        /// <summary>
        /// 格式化经过的时间间隔为易读的字符串表示
        /// </summary>
        /// <param name="elapsed">要格式化的时间间隔</param>
        /// <param name="showMilliseconds">是否显示毫秒级精度（默认显示）</param>
        /// <param name="alwaysShowAllUnits">是否强制显示所有时间单位（如"0天 0小时 1分钟"）</param>
        /// <param name="trimZeroUnits">是否自动移除零值单位（与alwaysShowAllUnits冲突时后者优先）</param>
        /// <returns>格式化后的时间字符串，可能包含负号</returns>
        private string FormatElapsedTime(
            TimeSpan elapsed,
            bool showMilliseconds = true,
            bool alwaysShowAllUnits = false,
            bool trimZeroUnits = true)
        {
            // 处理负值情况：先提取绝对值，最后再添加负号
            bool isNegative = elapsed.Ticks < 0;
            TimeSpan absoluteTime = isNegative ? elapsed.Negate() : elapsed;

            // 使用StringBuilder构建结果，优于字符串直接拼接（特别在复杂拼接场景）
            var sb = new StringBuilder();

            // 局部函数：智能添加时间单位（处理零值单位显示逻辑）
            void AppendUnit(int value, string unit)
            {
                // 当数值非零，或强制显示所有单位时，才添加该单位
                if (value != 0 || alwaysShowAllUnits)
                {
                    // 非首个单位前添加空格分隔
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(value).Append(unit);
                }
            }

            /* 分级显示逻辑：
             * 1. 大于等于1天：显示天、小时、分钟、秒
             * 2. 大于等于1小时：显示小时、分钟、秒
             * 3. 大于等于1分钟：显示分钟、秒
             * 4. 其他情况：根据参数决定显示秒（带毫秒）或毫秒
             * 注意：alwaysShowAllUnits会覆盖此分级逻辑
             */
            if (absoluteTime.TotalDays >= 1 || alwaysShowAllUnits)
            {
                AppendUnit(absoluteTime.Days, "天");
                AppendUnit(absoluteTime.Hours, "小时");
                AppendUnit(absoluteTime.Minutes, "分钟");
                AppendUnit(absoluteTime.Seconds, "秒");
            }
            else if (absoluteTime.TotalHours >= 1)
            {
                AppendUnit(absoluteTime.Hours, "小时");
                AppendUnit(absoluteTime.Minutes, "分钟");
                AppendUnit(absoluteTime.Seconds, "秒");
            }
            else if (absoluteTime.TotalMinutes >= 1)
            {
                AppendUnit(absoluteTime.Minutes, "分钟");
                AppendUnit(absoluteTime.Seconds, "秒");
            }
            else
            {
                // 小于1分钟的特殊处理
                if (absoluteTime.TotalSeconds >= 1)
                {
                    // 根据参数决定是否显示毫秒精度
                    if (showMilliseconds)
                        sb.Append(absoluteTime.TotalSeconds.ToString("F3")).Append("秒"); // 保留3位小数
                    else
                        sb.Append(Math.Round(absoluteTime.TotalSeconds)).Append("秒"); // 四舍五入到整秒
                }
                else
                {
                    // 小于1秒的处理
                    if (showMilliseconds)
                        sb.Append(absoluteTime.TotalMilliseconds.ToString("F0")).Append("毫秒"); // 完整毫秒数
                    else
                        sb.Append("0秒"); // 当不需要毫秒且时间很短时的默认显示
                }
            }

            // 处理所有单位均为零的情况（如TimeSpan.Zero）
            if (sb.Length == 0)
            {
                return "0秒";
            }

            // 注意：当前trimZeroUnits参数尚未完全实现，因为AppendUnit已基本处理了零值逻辑
            // 如需更复杂的零值修剪，可在此处添加额外处理

            // 最终处理负值情况
            return isNegative ? $"-{sb}" : sb.ToString();
        }

        /// <summary>
        /// 格式化轮询周期为最合适的时间单位字符串
        /// </summary>
        /// <param name="intervalMs">轮询间隔（毫秒）</param>
        /// <param name="showMillisecondsForSmallValues">对于小于1秒的值是否强制显示毫秒单位</param>
        /// <returns>格式化后的周期字符串（自动选择最佳单位）</returns>
        private string FormatPollingInterval(int intervalMs, bool showMillisecondsForSmallValues = true)
        {
            // 处理非法输入
            if (intervalMs < 0)
            {
                return "0毫秒";
            }

            // 定义单位阈值（毫秒）
            const int msPerSecond = 1000;
            const int msPerMinute = 60 * msPerSecond;
            const int msPerHour = 60 * msPerMinute;
            const int msPerDay = 24 * msPerHour;

            // 根据间隔大小选择最合适的单位
            if (intervalMs >= msPerDay)
            {
                int days = intervalMs / msPerDay;
                int remainingMs = intervalMs % msPerDay;
                return FormatCompoundInterval(days, remainingMs, "天", msPerHour, "小时");
            }
            else if (intervalMs >= msPerHour)
            {
                int hours = intervalMs / msPerHour;
                int remainingMs = intervalMs % msPerHour;
                return FormatCompoundInterval(hours, remainingMs, "小时", msPerMinute, "分钟");
            }
            else if (intervalMs >= msPerMinute)
            {
                int minutes = intervalMs / msPerMinute;
                int remainingMs = intervalMs % msPerMinute;
                return FormatCompoundInterval(minutes, remainingMs, "分钟", msPerSecond, "秒");
            }
            else if (intervalMs >= msPerSecond || !showMillisecondsForSmallValues)
            {
                // 大于等于1秒 或 配置为不显示毫秒时
                double seconds = intervalMs / (double)msPerSecond;
                return $"{seconds:0.##}秒"; // 最多保留两位小数
            }
            else
            {
                // 小于1秒且需要显示毫秒
                return $"{intervalMs}毫秒";
            }
        }

        /// <summary>
        /// 辅助方法：格式化复合时间间隔（如"1天2小时"）
        /// </summary>
        private string FormatCompoundInterval(int primaryValue, int remainingMs,
            string primaryUnit, int secondaryUnitMs, string secondaryUnit)
        {
            var sb = new StringBuilder();
            sb.Append(primaryValue).Append(primaryUnit);

            int secondaryValue = remainingMs / secondaryUnitMs;
            if (secondaryValue > 0)
            {
                sb.Append(secondaryValue).Append(secondaryUnit);
            }

            return sb.ToString();
        }
    }
}
