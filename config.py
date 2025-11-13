#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Конфигурационный файл с настройками приложения
"""

# Настройки COM-порта
BAUD_RATE = 115200

# Размеры экрана и элементов
SCREEN_WIDTH = 1000
SCREEN_HEIGHT = 700
CANVAS_WIDTH = 600
CANVAS_HEIGHT = 600
REFERENCE_SIZE = 200
COLOR_PANEL_X = SCREEN_WIDTH - 100

# Режим отладки
DEBUG_MODE = True

# Калибровка джойстика (значения по умолчанию)
JOY_X_MIN = 0
JOY_X_MAX = 4095
JOY_Y_MIN = 0
JOY_Y_MAX = 4095
JOY_X_CENTER = 2048
JOY_Y_CENTER = 2048
JOY_DEAD_ZONE = 100
JOY_SPEED_DIVIDER = 100.0
JOY_MAX_SPEED = 10.0

