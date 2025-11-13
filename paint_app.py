#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Основной класс приложения Paint
"""

import pygame
import sys
import re
from config import *
from colors import *
from drawings import DRAWINGS
from serial_handler import SerialHandler


class PaintApp:
    """Класс приложения для рисования с управлением через джойстик"""
    
    def __init__(self):
        pygame.init()
        self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
        pygame.display.set_caption("Paint - Joystick Control")
        self.clock = pygame.time.Clock()
        
        # Canvas для основного рисунка (по центру)
        self.canvas = pygame.Surface((CANVAS_WIDTH, CANVAS_HEIGHT))
        self.canvas.fill(WHITE)
        
        # Начинаем с человечка
        self.picture_type = 'human'
        print(f"Выбрана картинка: {self.picture_type}")
        
        # Референсная картинка (справа сверху) - ЗАКРАШЕННАЯ
        self.reference_image = pygame.Surface((REFERENCE_SIZE, REFERENCE_SIZE))
        self.reference_image.fill(WHITE)
        self._draw_reference()
        
        # Хранилище залитых фигур {имя_фигуры: цвет}
        self.filled_figures = {}
        
        # Изображение для раскрашивания (по центру) - БЕЗ ЦВЕТОВ
        self._draw_canvas_outline()
        
        # Состояние курсора и инструментов
        self.cursor_x = CANVAS_WIDTH // 2
        self.cursor_y = CANVAS_HEIGHT // 2
        self.selected_color = BLACK
        self.color_index = 0
        self.brush_size = 3
        
        # Обработчик COM-порта
        self.serial_handler = SerialHandler()
        
        # Калибровка джойстика
        self.joy_x_center = JOY_X_CENTER
        self.joy_y_center = JOY_Y_CENTER
        
        # Инициализация шрифтов
        self.font = pygame.font.Font(None, 24)
        self.small_font = pygame.font.Font(None, 18)
    
    def _draw_reference(self):
        """Рисует референсное изображение (закрашенное)"""
        drawing_class = DRAWINGS[self.picture_type]
        drawing_class.draw(self.reference_image, filled=True)
    
    def _draw_canvas_outline(self, clear=True):
        """Рисует контуры на основном canvas (без цветов)"""
        if clear:
            self.canvas.fill(WHITE)
        
        # Сначала рисуем все залитые фигуры
        scale_x = CANVAS_WIDTH / REFERENCE_SIZE
        scale_y = CANVAS_HEIGHT / REFERENCE_SIZE
        
        drawing_class = DRAWINGS[self.picture_type]
        
        # Рисуем заливки
        for figure_name, color in self.filled_figures.items():
            drawing_class.draw_filled_figure(self.canvas, figure_name, color, scale_x, scale_y)
        
        # Теперь рисуем контуры поверх заливок
        drawing_class.draw_outlines(self.canvas, scale_x, scale_y)
    
    def get_figure_at_position(self, x, y):
        """Определяет, какая фигура находится в позиции (x, y)"""
        scale_x = CANVAS_WIDTH / REFERENCE_SIZE
        scale_y = CANVAS_HEIGHT / REFERENCE_SIZE
        
        if DEBUG_MODE:
            print(f"[DEBUG] Проверка позиции: ({x}, {y}), тип: {self.picture_type}")
        
        drawing_class = DRAWINGS[self.picture_type]
        return drawing_class.get_figure_at(x, y, scale_x, scale_y)
    
    def fill_figure(self, figure_name):
        """Заливает фигуру выбранным цветом"""
        if DEBUG_MODE:
            print(f"[DEBUG] Заливка фигуры: {figure_name} цветом {self.selected_color}")
        
        # Сохраняем информацию о заливке
        self.filled_figures[figure_name] = self.selected_color
        
        # Перерисовываем canvas с учетом всех заливок
        self._draw_canvas_outline(clear=True)
        
        if DEBUG_MODE:
            print(f"[DEBUG] Фигура {figure_name} залита успешно")
            print(f"[DEBUG] Залитые фигуры: {self.filled_figures}")
    
    def get_color_at_panel(self, x, y):
        """Определяет, какой цвет выбран в панели цветов"""
        panel_start_y = 250
        color_size = 40
        color_spacing = 10
        
        # Проверяем, находится ли курсор в области панели цветов
        if COLOR_PANEL_X <= x <= SCREEN_WIDTH - 20:
            for i, color in enumerate(COLOR_PALETTE):
                color_y = panel_start_y + i * (color_size + color_spacing)
                if color_y <= y <= color_y + color_size:
                    return i
        
        return None
    
    def normalize_joystick_x(self, raw_value):
        """Нормализация значения X джойстика (с плавностью)"""
        if abs(raw_value - self.joy_x_center) < JOY_DEAD_ZONE:
            return self.cursor_x
        
        # Плавное относительное движение
        if raw_value < self.joy_x_center:
            # Джойстик влево -> X уменьшается
            delta = self.joy_x_center - raw_value
            speed = min(delta / JOY_SPEED_DIVIDER, JOY_MAX_SPEED)
            normalized = max(0, self.cursor_x - speed)
        else:
            # Джойстик вправо -> X увеличивается
            delta = raw_value - self.joy_x_center
            speed = min(delta / JOY_SPEED_DIVIDER, JOY_MAX_SPEED)
            normalized = min(SCREEN_WIDTH - 1, self.cursor_x + speed)
        
        return normalized
    
    def normalize_joystick_y(self, raw_value):
        """Нормализация значения Y джойстика (с инверсией)"""
        if abs(raw_value - self.joy_y_center) < JOY_DEAD_ZONE:
            return self.cursor_y
        
        # Инвертировано: если raw_value меньше центра, курсор идет вниз (Y увеличивается)
        if raw_value < self.joy_y_center:
            # Джойстик вверх -> курсор движется вниз (Y увеличивается)
            delta = self.joy_y_center - raw_value
            speed = min(delta / JOY_SPEED_DIVIDER, JOY_MAX_SPEED)
            normalized = min(SCREEN_HEIGHT - 1, self.cursor_y + speed)
        else:
            # Джойстик вниз -> курсор движется вверх (Y уменьшается)
            delta = raw_value - self.joy_y_center
            speed = min(delta / JOY_SPEED_DIVIDER, JOY_MAX_SPEED)
            normalized = max(0, self.cursor_y - speed)
        
        return normalized
    
    def update_cursor(self, x, y):
        """Обновление позиции курсора"""
        self.cursor_x = int(x)
        self.cursor_y = int(y)
    
    def reset_game(self):
        """Перезагрузка игры - следующая картинка и очистка всего"""
        # Выбираем следующую картинку по порядку
        if self.picture_type == 'human':
            self.picture_type = 'flower'
        else:
            self.picture_type = 'human'
        print(f"Выбрана новая картинка: {self.picture_type}")
        
        # Пересоздаем референсное изображение
        self._draw_reference()
        
        # Очищаем все заливки
        self.filled_figures = {}
        
        # Перерисовываем canvas с новыми контурами
        self._draw_canvas_outline(clear=True)
        
        # Сбрасываем курсор в центр
        self.cursor_x = CANVAS_WIDTH // 2
        self.cursor_y = CANVAS_HEIGHT // 2
        
        # Сбрасываем выбранный цвет
        self.selected_color = BLACK
        self.color_index = 0
    
    def handle_button(self, button):
        """Обработка нажатий кнопок"""
        if button == "A" or button == "D":
            # Выбор цвета из панели (работают обе кнопки A и D)
            color_idx = self.get_color_at_panel(self.cursor_x, self.cursor_y)
            if color_idx is not None:
                self.color_index = color_idx
                self.selected_color = COLOR_PALETTE[color_idx]
                print(f"Выбран цвет: {color_idx} - {COLOR_PALETTE[color_idx]}")
        elif button == "B":
            # Заливка фигуры на canvas
            canvas_screen_x = (SCREEN_WIDTH - CANVAS_WIDTH) // 2
            canvas_screen_y = (SCREEN_HEIGHT - CANVAS_HEIGHT) // 2
            
            if DEBUG_MODE:
                print(f"[DEBUG] Кнопка B нажата. Курсор на экране: ({self.cursor_x}, {self.cursor_y})")
                print(f"[DEBUG] Canvas на экране: ({canvas_screen_x}, {canvas_screen_y}) размером {CANVAS_WIDTH}x{CANVAS_HEIGHT}")
            
            if canvas_screen_x <= self.cursor_x <= canvas_screen_x + CANVAS_WIDTH and \
               canvas_screen_y <= self.cursor_y <= canvas_screen_y + CANVAS_HEIGHT:
                # Координаты относительно canvas
                canvas_x = self.cursor_x - canvas_screen_x
                canvas_y = self.cursor_y - canvas_screen_y
                
                if DEBUG_MODE:
                    print(f"[DEBUG] Курсор на canvas: ({canvas_x}, {canvas_y})")
                
                figure = self.get_figure_at_position(canvas_x, canvas_y)
                if figure:
                    self.fill_figure(figure)
                    print(f"✓ Залита фигура: {figure} цветом {self.selected_color}")
                else:
                    if DEBUG_MODE:
                        print(f"[DEBUG] Фигура не найдена в позиции ({canvas_x}, {canvas_y})")
                    print("Фигура не найдена. Убедитесь, что курсор на фигуре")
            else:
                if DEBUG_MODE:
                    print(f"[DEBUG] Курсор вне canvas")
                print("Курсор вне области рисунка")
        elif button == "C":
            # Очистка фигуры под курсором
            canvas_screen_x = (SCREEN_WIDTH - CANVAS_WIDTH) // 2
            canvas_screen_y = (SCREEN_HEIGHT - CANVAS_HEIGHT) // 2
            
            if canvas_screen_x <= self.cursor_x <= canvas_screen_x + CANVAS_WIDTH and \
               canvas_screen_y <= self.cursor_y <= canvas_screen_y + CANVAS_HEIGHT:
                canvas_x = self.cursor_x - canvas_screen_x
                canvas_y = self.cursor_y - canvas_screen_y
                
                figure = self.get_figure_at_position(canvas_x, canvas_y)
                if figure and figure in self.filled_figures:
                    del self.filled_figures[figure]
                    self._draw_canvas_outline(clear=True)
                    print(f"✓ Очищена фигура: {figure}")
                else:
                    print("Фигура не найдена или уже пустая")
            else:
                print("Курсор вне области рисунка")
        elif button == "E":
            # Переключение на следующий рисунок
            self.reset_game()
            print("✓ Игра перезагружена!")
        elif button == "F":
            # Очистка всего canvas
            self.filled_figures = {}
            self._draw_canvas_outline(clear=True)
            print("✓ Canvas полностью очищен")
    
    def parse_data(self, line):
        """Парсинг данных от микроконтроллера"""
        if line.startswith("BTN:"):
            button = line[4:]
            self.handle_button(button)
        elif line.startswith("X:") and "Y:" in line:
            match = re.match(r'X:(\d+),Y:(\d+),B:(\d+)', line)
            if match:
                x_raw = int(match.group(1))
                y_raw = int(match.group(2))
                
                x_normalized = self.normalize_joystick_x(x_raw)
                y_normalized = self.normalize_joystick_y(y_raw)
                
                self.update_cursor(x_normalized, y_normalized)
    
    def draw_ui(self):
        """Отрисовка пользовательского интерфейса"""
        # Референсное изображение (справа сверху)
        ref_x = SCREEN_WIDTH - REFERENCE_SIZE - 20
        ref_y = 20
        self.screen.blit(self.reference_image, (ref_x, ref_y))
        ref_label = self.small_font.render("Образец", True, BLACK)
        self.screen.blit(ref_label, (ref_x, ref_y - 20))
        
        # Панель цветов (справа)
        panel_start_y = 250
        color_size = 40
        color_spacing = 10
        
        color_label = self.small_font.render("Цвета:", True, BLACK)
        self.screen.blit(color_label, (COLOR_PANEL_X, panel_start_y - 20))
        
        for i, color in enumerate(COLOR_PALETTE):
            color_y = panel_start_y + i * (color_size + color_spacing)
            color_rect = pygame.Rect(COLOR_PANEL_X, color_y, color_size, color_size)
            
            # Рисуем квадрат цвета
            pygame.draw.rect(self.screen, color, color_rect)
            pygame.draw.rect(self.screen, BLACK, color_rect, 2)
            
            # Подсветка выбранного цвета
            if i == self.color_index:
                pygame.draw.rect(self.screen, YELLOW, color_rect, 4)
        
        # Информация
        info_y = 10
        picture_name = "Человечек" if self.picture_type == 'human' else "Цветок"
        picture_text = self.font.render(f"Картинка: {picture_name}", True, BLACK)
        self.screen.blit(picture_text, (10, info_y))
        color_text = self.font.render(f"Выбран цвет: {self.color_index}", True, BLACK)
        self.screen.blit(color_text, (10, info_y + 30))
        
        hint_text = self.small_font.render("A/D - Выбрать цвет | B - Залить фигуру | C - Очистить фигуру", True, BLACK)
        self.screen.blit(hint_text, (10, SCREEN_HEIGHT - 50))
        hint_text2 = self.small_font.render("E - След. рисунок | F - Очистить всё", True, BLACK)
        self.screen.blit(hint_text2, (10, SCREEN_HEIGHT - 30))
    
    def run(self):
        """Главный цикл приложения"""
        if not self.serial_handler.find_and_connect():
            return
        
        self.serial_handler.start_reading()
        
        running = True
        while running:
            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    running = False
                elif event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_ESCAPE:
                        running = False
            
            # Обработка данных из очереди
            while True:
                line = self.serial_handler.get_data()
                if line is None:
                    break
                try:
                    if DEBUG_MODE:
                        if line.startswith("BTN:"):
                            print(f"[DEBUG] Кнопка: {line}")
                    self.parse_data(line)
                except Exception as e:
                    if DEBUG_MODE:
                        print(f"[ERROR] Ошибка: {e}")
            
            # Отрисовка
            self.screen.fill(GRAY)
            
            # Основной canvas (по центру)
            canvas_screen_x = (SCREEN_WIDTH - CANVAS_WIDTH) // 2
            canvas_screen_y = (SCREEN_HEIGHT - CANVAS_HEIGHT) // 2
            self.screen.blit(self.canvas, (canvas_screen_x, canvas_screen_y))
            
            # Рамка вокруг canvas
            pygame.draw.rect(self.screen, BLACK, 
                           (canvas_screen_x - 2, canvas_screen_y - 2, 
                            CANVAS_WIDTH + 4, CANVAS_HEIGHT + 4), 2)
            
            # UI элементы
            self.draw_ui()
            
            # Курсор
            pygame.draw.circle(self.screen, RED, (self.cursor_x, self.cursor_y), 5, 2)
            pygame.draw.circle(self.screen, RED, (self.cursor_x, self.cursor_y), 1)
            
            pygame.display.flip()
            self.clock.tick(60)
        
        self.serial_handler.close()
        pygame.quit()
        sys.exit()

