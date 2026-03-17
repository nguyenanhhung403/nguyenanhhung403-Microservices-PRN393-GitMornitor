using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        using var connection = new SqliteConnection(@"Data Source=e:\.Net\FP2\GitMonitor.db");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS SyncHistories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BatchId TEXT NOT NULL,
                StudentId INTEGER NOT NULL,
                SyncTime TEXT NOT NULL,
                CommitCount INTEGER NOT NULL,
                PullRequestCount INTEGER NOT NULL,
                IssuesCount INTEGER NOT NULL,
                LinesAdded INTEGER NOT NULL,
                LinesDeleted INTEGER NOT NULL,
                LastCommitDate TEXT,
                RawDataJson TEXT
            );
        ";
        command.ExecuteNonQuery();
        Console.WriteLine("Table created successfully!");
    }
}
