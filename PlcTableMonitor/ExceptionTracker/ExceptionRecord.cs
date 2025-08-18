using SqlSugar;

namespace PlcTableMonitor.ExceptionTracker
{
    [SugarTable("ExceptionRecords")]
    public class ExceptionRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(Length = 100)]
        public string? TableName { get; set; }

        [SugarColumn(Length = 50)]
        public string? ExceptionType { get; set; }

        [SugarColumn(Length = 500)]
        public string? ExceptionDetail { get; set; }

        //[SugarColumn(IsOnlyIgnoreInsert = true)]
        public DateTime? RecordTime { get; set; } = DateTime.Now;

        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? AdditionalInfo { get; set; }

        [SugarColumn]
        public bool IsRecovered { get; set; }

        [SugarColumn(IsNullable = true)]
        public int? PreviousRecordId { get; set; }
    }
}
