using System.Windows;

namespace WpfClient;

public partial class App : Application
{
    private PaintController? _controller;

    protected override void OnStartup(StartupEventArgs e)
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

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
