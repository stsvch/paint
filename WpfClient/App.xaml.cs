using System;
using System.Windows;
using System.Windows.Media;

namespace WpfClient;

public partial class App : Application
{
    private PaintController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Если драйвер видеоускорения или кеш шрифтов дают сбой, WPF может
        // выбрасывать System.Windows.Media.Fonts TypeInitializationException
        // ещё до появления окна. Принудительно переключаемся в
        // программный режим рендеринга и показываем понятное сообщение,
        // чтобы приложение не падало молча.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

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
            MessageBox.Show(
                "Не удалось инициализировать подсистему шрифтов. Перезапустите службу \"Windows Presentation Foundation Font Cache 3.0.0.0\" и удалите файлы FontCache в %LOCALAPPDATA%. Приложение будет закрыто.",
                "Ошибка инициализации шрифтов",
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
