using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MySqlConnector;
using WpfClient.Services;

namespace WpfClient;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnRecordClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PaintViewModel viewModel)
        {
            viewModel.IsRecording = !viewModel.IsRecording;
        }
    }

    private async void OnPlaybackClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PaintViewModel viewModel && viewModel.Controller != null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Нажата кнопка Демо, попытка подключения к БД...");
                var playback = new Demo(DatabaseConfig.ConnectionString, Application.Current.Dispatcher);
                
                try
                {
                    var sessions = await playback.GetAvailableSessionsAsync();
                    playback.Dispose();

                    System.Diagnostics.Debug.WriteLine($"Получено сессий из БД: {sessions.Count}");

                    if (sessions.Count == 0)
                    {
                        MessageBox.Show(
                            "Нет доступных сессий для воспроизведения.\n\nПопробуйте:\n1. Проверить, что база данных запущена (docker-compose up -d)\n2. Сначала записать сессию (кнопка 'Запись')",
                            "Демо режим",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }

                    var dialog = new PlaybackDialog(sessions)
                    {
                        Owner = this
                    };

                    if (dialog.ShowDialog() == true && dialog.SelectedSessionId.HasValue)
                    {
                        viewModel.IsPlaying = true;
                        System.Diagnostics.Debug.WriteLine("Запуск воспроизведения в фоновом потоке");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine("Вызов StartPlaybackAsync в фоновом потоке");
                                await viewModel.Controller.StartPlaybackAsync(dialog.SelectedSessionId.Value);
                                System.Diagnostics.Debug.WriteLine("StartPlaybackAsync завершен в фоновом потоке");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка в фоновом потоке: {ex.Message}");
                                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                            }
                            finally
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    viewModel.IsPlaying = false;
                                    System.Diagnostics.Debug.WriteLine("IsPlaying установлен в false");
                                });
                            }
                        });
                    }
                }
                catch (MySqlConnector.MySqlException dbEx)
                {
                    playback.Dispose();
                    var errorMessage = dbEx.Message.Contains("Unable to connect") || dbEx.Message.Contains("timeout")
                        ? "Не удалось подключиться к базе данных.\n\nУбедитесь, что:\n1. MySQL контейнер запущен: docker-compose up -d\n2. Порт 3308 свободен\n3. База данных доступна"
                        : $"Ошибка базы данных: {dbEx.Message}";
                    
                    MessageBox.Show(errorMessage, "Ошибка подключения к БД", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Diagnostics.Debug.WriteLine($"Ошибка БД: {dbEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"StackTrace: {dbEx.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при открытии демо-режима: {ex.Message}\n\nУбедитесь, что база данных запущена (docker-compose up -d)",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Общая ошибка: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }
    }

    private void OnPauseResumeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PaintViewModel viewModel && viewModel.Controller != null)
        {
            if (viewModel.IsPaused)
            {
                viewModel.Controller.ResumePlayback();
            }
            else
            {
                viewModel.Controller.PausePlayback();
            }
            // Обновляем свойства
            viewModel.NotifyPropertyChanged(nameof(PaintViewModel.IsPaused));
            viewModel.NotifyPropertyChanged(nameof(PaintViewModel.PlayingStatus));
        }
    }

    private void OnStopPlaybackClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is PaintViewModel viewModel && viewModel.Controller != null)
        {
            viewModel.Controller.StopPlayback();
            viewModel.IsPlaying = false;
        }
    }

    private void OnJoystickModeToggle(object sender, RoutedEventArgs e)
    {
        if (DataContext is PaintViewModel viewModel)
        {
            // Переключаем режим между Absolute и Centered
            viewModel.JoystickMode = viewModel.JoystickMode == JoystickMode.Absolute 
                ? JoystickMode.Centered 
                : JoystickMode.Absolute;
        }
    }
}
