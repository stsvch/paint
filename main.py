#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Paint Receiver - Программа для приема данных от NUCLEO-F103RB с JoystickShield
и визуализации в стиле Paint с раскрашиванием по образцу

Точка входа в приложение
"""

from paint_app import PaintApp


def main():
    """Главная функция запуска приложения"""
    app = PaintApp()
    app.run()


if __name__ == "__main__":
    main()

