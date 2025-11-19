using System;
using System.Windows;
using System.Windows.Media;
using WpfClient.Diagnostics;

namespace WpfClient;

public partial class App : Application
{
    private PaintController? _controller;
    private StartupDiagnostics? _diagnostics;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Если драйвер видеоускорения или кеш шрифтов дают сбой, WPF может
        // выбрасывать System.Windows.Media.Fonts TypeInitializationException
        // ещё до появления окна. Принудительно переключаемся в
        // программный режим рендеринга и показываем понятное сообщение,
        // чтобы приложение не падало молча.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                _diagnostics?.LogException(ex, "Необработанное исключение AppDomain");
            }
        };

        DispatcherUnhandledException += (_, args) =>
        {
            _diagnostics?.LogException(args.Exception, "Необработанное исключение диспетчера");
            MessageBox.Show(
                $"Произошла ошибка: {args.Exception.Message}\n\nПолный лог: {_diagnostics?.LogFilePath}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };

        StartupDiagnostics? diagnostics = null;

        try
        {
            diagnostics = _diagnostics = new StartupDiagnostics();
            _diagnostics.WriteStartupInfo();
        }
        catch (Exception ex)
        {
            // Диагностика полезна, но не должна блокировать запуск. Если лог не
            // удалось открыть (например, из‑за прав на каталог), показываем
            // сообщение и продолжаем без записи.
            MessageBox.Show(
                "Не удалось инициализировать сбор диагностической информации. Продолжаем запуск без логирования.\n\n" +
                ex.Message,
                "Предупреждение",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        try
        {
            base.OnStartup(e);
            InitializeComponent();

            var viewModel = new PaintViewModel();
            var engine = new PaintEngine();
            _controller = new PaintController(viewModel, engine);
            viewModel.Controller = _controller;

            var window = new MainWindow
            {
                DataContext = viewModel
            };

            MainWindow = window;

            window.Closed += (_, _) => _controller?.Dispose();
            window.Show();

            _controller.Run();
        }
        catch (TypeInitializationException ex) when (ex.TypeName == typeof(Fonts).FullName)
        {
            _diagnostics?.LogException(ex, "Ошибка инициализации подсистемы шрифтов");
            MessageBox.Show(
                "Не удалось инициализировать подсистему шрифтов. Перезапустите службу \"Windows Presentation Foundation Font Cache 3.0.0.0\" и удалите файлы FontCache в %LOCALAPPDATA%. Приложение будет закрыто.",
                "Ошибка инициализации шрифтов",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
        catch (Exception ex)
        {
            _diagnostics?.LogException(ex, "Ошибка запуска приложения");
            MessageBox.Show(
                "Приложение не удалось запустить.\n\n" +
                ex.Message +
                (_diagnostics is not null
                    ? $"\n\nПодробности в логе: {_diagnostics.LogFilePath}"
                    : string.Empty),
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
