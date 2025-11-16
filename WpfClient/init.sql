-- Создание базы данных
CREATE DATABASE IF NOT EXISTS paintdb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE paintdb;

-- Таблица для сессий рисования
CREATE TABLE IF NOT EXISTS sessions (
    id INT AUTO_INCREMENT PRIMARY KEY,
    started_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    ended_at DATETIME(3) NULL,
    drawing_key VARCHAR(50) NOT NULL,
    INDEX idx_started_at (started_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Таблица для действий пользователя
CREATE TABLE IF NOT EXISTS actions (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    session_id INT NOT NULL,
    action_type VARCHAR(20) NOT NULL COMMENT 'cursor_move, color_select, fill, clear_figure, next_picture, clear_all',
    timestamp_ms BIGINT NOT NULL COMMENT 'Время с начала сессии в миллисекундах',
    occurred_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    cursor_x DOUBLE NULL COMMENT 'Координата X курсора',
    cursor_y DOUBLE NULL COMMENT 'Координата Y курсора',
    canvas_x DOUBLE NULL COMMENT 'Координата X на canvas',
    canvas_y DOUBLE NULL COMMENT 'Координата Y на canvas',
    color_index INT NULL COMMENT 'Индекс выбранного цвета',
    color_hex VARCHAR(7) NULL COMMENT 'Hex код цвета',
    figure_name VARCHAR(100) NULL COMMENT 'Название фигуры (для fill/clear_figure)',
    button_pressed VARCHAR(1) NULL COMMENT 'Нажатая кнопка (A, B, C, D, E, F)',
    raw_x INT NULL COMMENT 'Сырое значение X с джойстика',
    raw_y INT NULL COMMENT 'Сырое значение Y с джойстика',
    additional_data TEXT NULL COMMENT 'Дополнительные данные в JSON формате',
    INDEX idx_session_id (session_id),
    INDEX idx_timestamp_ms (session_id, timestamp_ms),
    INDEX idx_occurred_at (occurred_at),
    FOREIGN KEY (session_id) REFERENCES sessions(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Создание пользователя для подключения (уже создается через переменные окружения)
GRANT ALL PRIVILEGES ON paintdb.* TO 'paintuser'@'%';
FLUSH PRIVILEGES;



