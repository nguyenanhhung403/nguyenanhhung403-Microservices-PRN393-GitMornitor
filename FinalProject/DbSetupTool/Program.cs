using Microsoft.Data.Sqlite;
using System.IO;

var dbFile = @"e:\.Net\FinalProject\GitStudentMonitorApi\GitMonitor.db";
var sqlFile = @"e:\.Net\FinalProject\GitMonitor.db.sql";

using var connection = new SqliteConnection($"Data Source={dbFile}");
connection.Open();

var sql = File.ReadAllText(sqlFile);
var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);

foreach(var stmt in statements)
{
    if (string.IsNullOrWhiteSpace(stmt)) continue;
    using var command = new SqliteCommand(stmt, connection);
    command.ExecuteNonQuery();
}

System.Console.WriteLine("Database created successfully from SQL.");
