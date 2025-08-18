DROP TABLE IF EXISTS ExceptionRecords;
CREATE TABLE IF NOT EXISTS ExceptionRecords (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TableName TEXT NOT NULL,
    ExceptionType TEXT NOT NULL,
    ExceptionDetail TEXT,
    RecordTime DATETIME DEFAULT CURRENT_TIMESTAMP,
    AdditionalInfo TEXT,
    IsRecovered INTEGER DEFAULT 0,  -- 0:未恢复 1:已恢复
    PreviousRecordId INTEGER  -- 关联前一条同类型异常记录
);

CREATE INDEX IF NOT EXISTS IX_ExceptionRecords_TableName ON ExceptionRecords(TableName);
CREATE INDEX IF NOT EXISTS IX_ExceptionRecords_RecordTime ON ExceptionRecords(RecordTime);
CREATE INDEX IF NOT EXISTS IX_ExceptionRecords_Table_Type_Status ON ExceptionRecords(TableName, ExceptionType, IsRecovered);