#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Модуль для работы с последовательным портом (COM-port)
"""

import serial
import serial.tools.list_ports
import threading
from queue import Queue
from config import BAUD_RATE, DEBUG_MODE


class SerialHandler:
    """Класс для управления последовательным портом"""
    
    def __init__(self):
        self.serial_port = None
        self.data_queue = Queue()
        self.serial_thread = None
        self._running = False
    
    def find_and_connect(self):
        """Поиск и подключение к доступному COM порту"""
        print("Поиск доступного COM порта...")
        
        # Список портов для проверки
        stm_ports_to_try = ['COM3', 'COM4', 'COM5', 'COM6', 'COM7', 'COM8', 'COM9', 'COM10']
        
        # Сначала пытаемся найти STM устройства
        try:
            ports = serial.tools.list_ports.comports()
            for port, desc, hwid in ports:
                desc_upper = desc.upper()
                if any(keyword in desc_upper for keyword in ['STM', 'ST MICRO', 'STLINK', 'NUCLEO', 'VIRTUAL COM', 'ST MICROELECTRONICS']):
                    if port not in stm_ports_to_try:
                        stm_ports_to_try.insert(0, port)
        except:
            pass
        
        # Пробуем подключиться к каждому порту из списка
        for port in stm_ports_to_try:
            try:
                test_port = serial.Serial(port, BAUD_RATE, timeout=0.1)
                test_port.close()
                self.serial_port = serial.Serial(port, BAUD_RATE, timeout=0.1)
                print(f"✓ Найден и подключен к порту: {port} на {BAUD_RATE} бод")
                return True
            except:
                continue
        
        print("\n✗ Не удалось найти рабочий COM порт")
        print("Проверьте подключение устройства и попробуйте снова")
        return False
    
    def start_reading(self):
        """Запуск потока для чтения данных"""
        if self.serial_port and not self._running:
            self._running = True
            self.serial_thread = threading.Thread(target=self._read_thread, daemon=True)
            self.serial_thread.start()
    
    def _read_thread(self):
        """Поток для чтения данных из последовательного порта"""
        while self._running and self.serial_port and self.serial_port.is_open:
            try:
                if self.serial_port.in_waiting > 0:
                    line = self.serial_port.readline().decode('utf-8', errors='ignore').strip()
                    if line:
                        if DEBUG_MODE:
                            print(f"[UART] Получено: {line}")
                        self.data_queue.put(line)
            except Exception as e:
                print(f"Ошибка чтения: {e}")
                break
    
    def get_data(self):
        """Получить данные из очереди (если есть)"""
        if not self.data_queue.empty():
            return self.data_queue.get_nowait()
        return None
    
    def close(self):
        """Закрыть соединение"""
        self._running = False
        if self.serial_port:
            self.serial_port.close()

