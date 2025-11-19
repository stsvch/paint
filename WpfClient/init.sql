IF DB_ID(N'paintdb') IS NULL
BEGIN
    CREATE DATABASE [paintdb];
END;
GO

USE [paintdb];
GO

IF OBJECT_ID(N'dbo.sessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.sessions (
        id INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        started_at DATETIME2(3) NOT NULL CONSTRAINT DF_sessions_started_at DEFAULT SYSUTCDATETIME(),
        ended_at DATETIME2(3) NULL,
        drawing_key VARCHAR(50) NOT NULL
    );
END;
GO

IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = N'idx_sessions_started_at'
    )
BEGIN
    CREATE INDEX idx_sessions_started_at ON dbo.sessions (started_at);
END;
GO

IF OBJECT_ID(N'dbo.actions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.actions (
        id BIGINT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        session_id INT NOT NULL,
        action_type VARCHAR(20) NOT NULL,
        timestamp_ms BIGINT NOT NULL,
        occurred_at DATETIME2(3) NOT NULL CONSTRAINT DF_actions_occurred_at DEFAULT SYSUTCDATETIME(),
        cursor_x FLOAT NULL,
        cursor_y FLOAT NULL,
        canvas_x FLOAT NULL,
        canvas_y FLOAT NULL,
        color_index INT NULL,
        color_hex VARCHAR(7) NULL,
        figure_name VARCHAR(100) NULL,
        button_pressed CHAR(1) NULL,
        raw_x INT NULL,
        raw_y INT NULL,
        additional_data NVARCHAR(MAX) NULL,
        CONSTRAINT FK_actions_sessions FOREIGN KEY (session_id) REFERENCES dbo.sessions (id) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = N'idx_actions_session_id'
    )
BEGIN
    CREATE INDEX idx_actions_session_id ON dbo.actions (session_id);
END;
GO

IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = N'idx_actions_session_timestamp'
    )
BEGIN
    CREATE INDEX idx_actions_session_timestamp ON dbo.actions (session_id, timestamp_ms);
END;
GO

IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE name = N'idx_actions_occurred_at'
    )
BEGIN
    CREATE INDEX idx_actions_occurred_at ON dbo.actions (occurred_at);
END;
GO

IF DB_ID(N'paintdb') IS NOT NULL AND SUSER_ID(N'paintuser') IS NOT NULL
BEGIN
    EXEC ('USE [paintdb]; GRANT CONTROL, ALTER, DELETE, EXECUTE, INSERT, REFERENCES, SELECT, UPDATE ON DATABASE::[paintdb] TO [paintuser];');
END;





