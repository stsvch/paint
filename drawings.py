#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Модуль для рисования различных фигур
"""

import pygame
from colors import *


class HumanDrawing:
    """Класс для рисования и работы с человечком"""
    
    @staticmethod
    def draw(surface, filled=False):
        """Рисует человечка из примитивов"""
        surface.fill(WHITE)
        
        # Тело - прямоугольник
        body_rect = pygame.Rect(80, 100, 40, 60)
        if filled:
            pygame.draw.rect(surface, BLUE, body_rect)
        else:
            pygame.draw.rect(surface, BLACK, body_rect, 2)
        
        # Голова - круг
        head_pos = (100, 80)
        if filled:
            pygame.draw.circle(surface, YELLOW, head_pos, 20)
        else:
            pygame.draw.circle(surface, BLACK, head_pos, 20, 2)
        
        # Руки - прямоугольники
        left_arm = pygame.Rect(60, 110, 20, 40)
        right_arm = pygame.Rect(120, 110, 20, 40)
        if filled:
            pygame.draw.rect(surface, RED, left_arm)
            pygame.draw.rect(surface, RED, right_arm)
        else:
            pygame.draw.rect(surface, BLACK, left_arm, 2)
            pygame.draw.rect(surface, BLACK, right_arm, 2)
        
        # Ноги - прямоугольники
        left_leg = pygame.Rect(85, 160, 15, 40)
        right_leg = pygame.Rect(100, 160, 15, 40)
        if filled:
            pygame.draw.rect(surface, GREEN, left_leg)
            pygame.draw.rect(surface, GREEN, right_leg)
        else:
            pygame.draw.rect(surface, BLACK, left_leg, 2)
            pygame.draw.rect(surface, BLACK, right_leg, 2)
        
        # Глаза - маленькие круги
        pygame.draw.circle(surface, BLACK, (95, 75), 3)
        pygame.draw.circle(surface, BLACK, (105, 75), 3)
    
    @staticmethod
    def draw_filled_figure(surface, figure_name, color, scale_x, scale_y):
        """Рисует залитую фигуру человечка"""
        if figure_name == 'head':
            center_x = int(100 * scale_x)
            center_y = int(80 * scale_y)
            radius = int(20 * scale_x)
            pygame.draw.circle(surface, color, (center_x, center_y), radius)
        elif figure_name == 'body':
            x = int(80 * scale_x)
            y = int(100 * scale_y)
            w = int(40 * scale_x)
            h = int(60 * scale_y)
            pygame.draw.rect(surface, color, pygame.Rect(x, y, w, h))
        elif figure_name == 'left_arm':
            x = int(60 * scale_x)
            y = int(110 * scale_y)
            w = int(20 * scale_x)
            h = int(40 * scale_y)
            pygame.draw.rect(surface, color, pygame.Rect(x, y, w, h))
        elif figure_name == 'right_arm':
            x = int(120 * scale_x)
            y = int(110 * scale_y)
            w = int(20 * scale_x)
            h = int(40 * scale_y)
            pygame.draw.rect(surface, color, pygame.Rect(x, y, w, h))
        elif figure_name == 'left_leg':
            x = int(85 * scale_x)
            y = int(160 * scale_y)
            w = int(15 * scale_x)
            h = int(40 * scale_y)
            pygame.draw.rect(surface, color, pygame.Rect(x, y, w, h))
        elif figure_name == 'right_leg':
            x = int(100 * scale_x)
            y = int(160 * scale_y)
            w = int(15 * scale_x)
            h = int(40 * scale_y)
            pygame.draw.rect(surface, color, pygame.Rect(x, y, w, h))
    
    @staticmethod
    def draw_outlines(surface, scale_x, scale_y):
        """Рисует контуры человечка"""
        # Тело
        body_rect = pygame.Rect(int(80 * scale_x), int(100 * scale_y), 
                               int(40 * scale_x), int(60 * scale_y))
        pygame.draw.rect(surface, BLACK, body_rect, 2)
        
        # Голова
        head_pos = (int(100 * scale_x), int(80 * scale_y))
        pygame.draw.circle(surface, BLACK, head_pos, int(20 * scale_x), 2)
        
        # Руки
        left_arm = pygame.Rect(int(60 * scale_x), int(110 * scale_y), 
                               int(20 * scale_x), int(40 * scale_y))
        right_arm = pygame.Rect(int(120 * scale_x), int(110 * scale_y), 
                               int(20 * scale_x), int(40 * scale_y))
        pygame.draw.rect(surface, BLACK, left_arm, 2)
        pygame.draw.rect(surface, BLACK, right_arm, 2)
        
        # Ноги
        left_leg = pygame.Rect(int(85 * scale_x), int(160 * scale_y), 
                              int(15 * scale_x), int(40 * scale_y))
        right_leg = pygame.Rect(int(100 * scale_x), int(160 * scale_y), 
                               int(15 * scale_x), int(40 * scale_y))
        pygame.draw.rect(surface, BLACK, left_leg, 2)
        pygame.draw.rect(surface, BLACK, right_leg, 2)
        
        # Глаза
        pygame.draw.circle(surface, BLACK, (int(95 * scale_x), int(75 * scale_y)), 3)
        pygame.draw.circle(surface, BLACK, (int(105 * scale_x), int(75 * scale_y)), 3)
    
    @staticmethod
    def get_figure_at(x, y, scale_x, scale_y):
        """Определяет фигуру человечка в позиции (x, y)"""
        # Голова - проверяем первой (чтобы приоритет был выше)
        head_center_x = int(100 * scale_x)
        head_center_y = int(80 * scale_y)
        head_radius = int(20 * scale_x)
        dist_sq = (x - head_center_x)**2 + (y - head_center_y)**2
        if dist_sq <= head_radius**2:
            return 'head'
        
        # Тело
        body_rect = pygame.Rect(int(80 * scale_x), int(100 * scale_y), 
                               int(40 * scale_x), int(60 * scale_y))
        if body_rect.collidepoint(x, y):
            return 'body'
        
        # Левая рука
        left_arm = pygame.Rect(int(60 * scale_x), int(110 * scale_y), 
                              int(20 * scale_x), int(40 * scale_y))
        if left_arm.collidepoint(x, y):
            return 'left_arm'
        
        # Правая рука
        right_arm = pygame.Rect(int(120 * scale_x), int(110 * scale_y), 
                               int(20 * scale_x), int(40 * scale_y))
        if right_arm.collidepoint(x, y):
            return 'right_arm'
        
        # Левая нога
        left_leg = pygame.Rect(int(85 * scale_x), int(160 * scale_y), 
                              int(15 * scale_x), int(40 * scale_y))
        if left_leg.collidepoint(x, y):
            return 'left_leg'
        
        # Правая нога
        right_leg = pygame.Rect(int(100 * scale_x), int(160 * scale_y), 
                                int(15 * scale_x), int(40 * scale_y))
        if right_leg.collidepoint(x, y):
            return 'right_leg'
        
        return None


class FlowerDrawing:
    """Класс для рисования и работы с цветком"""
    
    @staticmethod
    def draw(surface, filled=False):
        """Рисует цветок из примитивов"""
        surface.fill(WHITE)
        
        # Лепестки (4 круга) - разные цвета для каждого
        petal_radius = 20
        petals = [
            (100, 70),   # Верхний
            (120, 90),   # Правый
            (100, 110),  # Нижний
            (80, 90),    # Левый
        ]
        
        # Цвета для лепестков в образце
        petal_colors = [PINK, YELLOW, MAGENTA, CYAN]
        
        if filled:
            # Рисуем лепестки разными цветами
            for petal_pos, color in zip(petals, petal_colors):
                pygame.draw.circle(surface, color, petal_pos, petal_radius)
        else:
            # Контуры лепестков
            for petal_pos in petals:
                pygame.draw.circle(surface, BLACK, petal_pos, petal_radius, 2)
        
        # Стебель - прямоугольник
        stem_rect = pygame.Rect(95, 130, 10, 50)
        if filled:
            pygame.draw.rect(surface, GREEN, stem_rect)
        else:
            pygame.draw.rect(surface, BLACK, stem_rect, 2)
        
        # Листья - овалы (рисуем как круги)
        leaf1_pos = (110, 140)
        leaf2_pos = (85, 150)
        if filled:
            pygame.draw.circle(surface, GREEN, leaf1_pos, 12)
            pygame.draw.circle(surface, GREEN, leaf2_pos, 12)
        else:
            pygame.draw.circle(surface, BLACK, leaf1_pos, 12, 2)
            pygame.draw.circle(surface, BLACK, leaf2_pos, 12, 2)
    
    @staticmethod
    def draw_filled_figure(surface, figure_name, color, scale_x, scale_y):
        """Рисует залитую фигуру цветка"""
        if figure_name == 'petal_top':
            pos_x = int(100 * scale_x)
            pos_y = int(70 * scale_y)
            radius = int(20 * scale_x)
            pygame.draw.circle(surface, color, (pos_x, pos_y), radius)
        elif figure_name == 'petal_right':
            pos_x = int(120 * scale_x)
            pos_y = int(90 * scale_y)
            radius = int(20 * scale_x)
            pygame.draw.circle(surface, color, (pos_x, pos_y), radius)
        elif figure_name == 'petal_bottom':
            pos_x = int(100 * scale_x)
            pos_y = int(110 * scale_y)
            radius = int(20 * scale_x)
            pygame.draw.circle(surface, color, (pos_x, pos_y), radius)
        elif figure_name == 'petal_left':
            pos_x = int(80 * scale_x)
            pos_y = int(90 * scale_y)
            radius = int(20 * scale_x)
            pygame.draw.circle(surface, color, (pos_x, pos_y), radius)
        elif figure_name == 'stem':
            x = int(95 * scale_x)
            y = int(130 * scale_y)
            w = int(10 * scale_x)
            h = int(50 * scale_y)
            pygame.draw.rect(surface, color, pygame.Rect(x, y, w, h))
        elif figure_name == 'leaf1':
            pos_x = int(110 * scale_x)
            pos_y = int(140 * scale_y)
            radius = int(12 * scale_x)
            pygame.draw.circle(surface, color, (pos_x, pos_y), radius)
        elif figure_name == 'leaf2':
            pos_x = int(85 * scale_x)
            pos_y = int(150 * scale_y)
            radius = int(12 * scale_x)
            pygame.draw.circle(surface, color, (pos_x, pos_y), radius)
    
    @staticmethod
    def draw_outlines(surface, scale_x, scale_y):
        """Рисует контуры цветка"""
        # Лепестки
        petals = [
            (int(100 * scale_x), int(70 * scale_y)),
            (int(120 * scale_x), int(90 * scale_y)),
            (int(100 * scale_x), int(110 * scale_y)),
            (int(80 * scale_x), int(90 * scale_y)),
        ]
        for pos in petals:
            pygame.draw.circle(surface, BLACK, pos, int(20 * scale_x), 2)
        
        # Стебель
        stem = pygame.Rect(int(95 * scale_x), int(130 * scale_y), 
                          int(10 * scale_x), int(50 * scale_y))
        pygame.draw.rect(surface, BLACK, stem, 2)
        
        # Листья
        leaf1 = (int(110 * scale_x), int(140 * scale_y))
        leaf2 = (int(85 * scale_x), int(150 * scale_y))
        pygame.draw.circle(surface, BLACK, leaf1, int(12 * scale_x), 2)
        pygame.draw.circle(surface, BLACK, leaf2, int(12 * scale_x), 2)
    
    @staticmethod
    def get_figure_at(x, y, scale_x, scale_y):
        """Определяет фигуру цветка в позиции (x, y)"""
        # Лепестки
        petals = [
            ((int(100 * scale_x), int(70 * scale_y)), 'petal_top'),
            ((int(120 * scale_x), int(90 * scale_y)), 'petal_right'),
            ((int(100 * scale_x), int(110 * scale_y)), 'petal_bottom'),
            ((int(80 * scale_x), int(90 * scale_y)), 'petal_left'),
        ]
        for (pos_x, pos_y), name in petals:
            radius = int(20 * scale_x)
            dist_sq = (x - pos_x)**2 + (y - pos_y)**2
            if dist_sq <= radius**2:
                return name
        
        # Стебель
        stem = pygame.Rect(int(95 * scale_x), int(130 * scale_y), 
                          int(10 * scale_x), int(50 * scale_y))
        if stem.collidepoint(x, y):
            return 'stem'
        
        # Листья
        leaf1_pos = (int(110 * scale_x), int(140 * scale_y))
        leaf2_pos = (int(85 * scale_x), int(150 * scale_y))
        leaf_radius = int(12 * scale_x)
        
        dist_sq1 = (x - leaf1_pos[0])**2 + (y - leaf1_pos[1])**2
        if dist_sq1 <= leaf_radius**2:
            return 'leaf1'
        
        dist_sq2 = (x - leaf2_pos[0])**2 + (y - leaf2_pos[1])**2
        if dist_sq2 <= leaf_radius**2:
            return 'leaf2'
        
        return None


# Словарь для удобного доступа к классам рисунков
DRAWINGS = {
    'human': HumanDrawing,
    'flower': FlowerDrawing
}

