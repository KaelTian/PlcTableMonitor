Winform小工具查看数据库表的采集数据状况。

1.正常情况下按秒采集数据，则MySQL数据库的表中应该持续有数据插入
2.普通表 通过MAX(CreateTime) 与当前时间比较，判断数据库采集状态是否正常(5分钟内有数据 正常；30分钟内没有数据 警报；超过30分钟内没有用数据 异常)
3.报警类的表与普通表逻辑相反 通过MAX(CreateTime) 与当前时间比较，判断数据库采集状态是否正常(10分钟内有数据 异常；60分钟内有数据 警报；超过60分钟内没有用数据 正常)
![alt text](44909076-bb44-4c18-8b0a-f7876350db86.png)
4.通过SQLite数据库记录表的运行状况
![alt text](adbc6632-6b51-4b39-b60d-2593440d898a.png)
5.由于MySQL表ID自增，因此通过MAX(ID)属性记录当前table的行数，提高查询效率
6.支持通过CancellationTokenSource 取消监控，原理是通过CancellationTokenSource 取消Task的执行
7.未来考虑将SQLite记录的数据也显示到界面上，方便查看
