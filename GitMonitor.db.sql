BEGIN TRANSACTION;
DROP TABLE IF EXISTS "ClassRooms";
CREATE TABLE "ClassRooms" (
	"Id"	INTEGER,
	"TeacherId"	INTEGER NOT NULL,
	"Name"	TEXT NOT NULL,
	"IsActive"	BOOLEAN NOT NULL DEFAULT 1,
	"CreatedAt"	DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
	PRIMARY KEY("Id" AUTOINCREMENT),
	CONSTRAINT "FK_ClassRooms_Teachers" FOREIGN KEY("TeacherId") REFERENCES "Teachers"("Id") ON DELETE CASCADE
);
DROP TABLE IF EXISTS "StudentGroups";
CREATE TABLE StudentGroups (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ClassRoomId INTEGER NOT NULL,        -- Nhóm này thuộc Lớp nào
    GroupName TEXT NOT NULL,             -- Tên nhóm (vd: Nhóm 1, Team Apollo)
    RepositoryUrl TEXT NOT NULL,         -- URL Repo GitHub do đại diện nhóm nộp
    Token TEXT,                          -- (Tùy chọn) Nếu Repo nộp là Private, sinh viên cần cấp Fine-grained Token của Repo đó cho thầy
    Status INTEGER DEFAULT 0,            -- Dùng Enum (0: Active, 1: RepoNotFound, 2: Unauthorized)
    LastErrorMessage TEXT,               -- Lưu chi tiết lỗi khi quét (ví dụ: "Token expired")
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT FK_StudentGroups_ClassRooms FOREIGN KEY (ClassRoomId) REFERENCES ClassRooms (Id) ON DELETE CASCADE
);
DROP TABLE IF EXISTS "Students";
CREATE TABLE Students (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    GroupId INTEGER NOT NULL,            -- Sinh viên này thuộc Nhóm nào
    StudentCode TEXT NOT NULL,           -- Mã SV
    Name TEXT NOT NULL,                  -- Tên SV
    GitHubUsername TEXT NOT NULL,        -- Username GitHub để thầy quét đóng góp trong Repo của Nhóm
    AvatarUrl TEXT,                      -- Lưu URL ảnh đại diện từ GitHub
    Email TEXT,                          
    IsLeader BOOLEAN NOT NULL DEFAULT 0, -- (Tùy chọn) Dấu hiệu biết ai là Trưởng nhóm/Người nộp bài
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT FK_Students_StudentGroups FOREIGN KEY (GroupId) REFERENCES StudentGroups (Id) ON DELETE CASCADE
);
DROP TABLE IF EXISTS "SyncHistory";
CREATE TABLE SyncHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BatchId TEXT NOT NULL,               -- Dùng mã định danh duy nhất cho mỗi lần thầy nhấn nút "Quét toàn bộ"
    StudentId INTEGER NOT NULL,          
    SyncTime DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP, 
    CommitCount INTEGER NOT NULL DEFAULT 0, 
    PullRequestCount INTEGER NOT NULL DEFAULT 0, 
    IssuesCount INTEGER NOT NULL DEFAULT 0,      
    LinesAdded INTEGER NOT NULL DEFAULT 0,       
    LinesDeleted INTEGER NOT NULL DEFAULT 0,     
    LastCommitDate DATETIME,                     
    RawDataJson TEXT,                            -- (Optional) Lưu toàn bộ JSON kết quả từ API
    
    CONSTRAINT FK_SyncHistory_Students FOREIGN KEY (StudentId) REFERENCES Students (Id) ON DELETE CASCADE
);
DROP TABLE IF EXISTS "Teachers";
CREATE TABLE Teachers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,       
    Name TEXT NOT NULL,                  
    Email TEXT,                          
    DefaultGitHubToken TEXT,             -- Token mặc định của thầy
    LastLogin DATETIME,                  -- Theo dõi lần cuối thầy sử dụng app
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);
DROP INDEX IF EXISTS "IX_ClassRooms_TeacherId";
CREATE INDEX IX_ClassRooms_TeacherId ON ClassRooms(TeacherId);
DROP INDEX IF EXISTS "IX_StudentGroups_ClassRoomId";
CREATE INDEX IX_StudentGroups_ClassRoomId ON StudentGroups(ClassRoomId);
DROP INDEX IF EXISTS "IX_Students_GitHubUsername";
CREATE INDEX IX_Students_GitHubUsername ON Students(GitHubUsername);
DROP INDEX IF EXISTS "IX_Students_GroupId";
CREATE INDEX IX_Students_GroupId ON Students(GroupId);
DROP INDEX IF EXISTS "IX_SyncHistory_StudentId_SyncTime";
CREATE INDEX IX_SyncHistory_StudentId_SyncTime ON SyncHistory(StudentId, SyncTime DESC);
COMMIT;
