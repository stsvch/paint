using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfClient
{
    public sealed class PaintController : IDisposable
    {
        private readonly PaintViewModel _viewModel;
        private readonly DispatcherTimer _timer;
        private readonly SerialPort? _serialPort;
        private bool _isRunning;

        private static readonly Dictionary<string, Action<PaintViewModel, string[]>> SerialCommands = new()
        {
            ["CURSOR"] = (vm, args) =>
            {
                if (args.Length >= 2 &&
                    double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    vm.CursorX = x;
                    vm.CursorY = y;
                }
            },
            ["COLOR"] = (vm, args) =>
            {
                if (args.Length >= 1)
                {
                    vm.SelectedColor = args[0];
                }
            },
            ["FIGURE"] = (vm, args) =>
            {
                if (args.Length >= 1)
                {
                    vm.FilledFigures.Add(string.Join(' ', args));
                }
            },
            ["PICTURE"] = (vm, args) =>
            {
                if (args.Length >= 1)
                {
                    vm.PictureType = args[0];
                }
            }
        };

        public PaintController(PaintViewModel viewModel, DispatcherTimer timer, SerialPort? serialPort)
        {
            _viewModel = viewModel;
            _timer = timer;
            _serialPort = serialPort;
        }

        public void Run()
        {
            if (_isRunning)
            {
                return;
            }

            _timer.Tick += OnTick;
            _timer.Start();
            CompositionTarget.Rendering += OnRendering;

            if (_serialPort is not null)
            {
                _serialPort.DataReceived += OnSerialDataReceived;
                try
                {
                    if (!_serialPort.IsOpen)
                    {
                        _serialPort.Open();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open serial port: {ex.Message}");
                }
            }

            _isRunning = true;
        }

        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _timer.Tick -= OnTick;
            _timer.Stop();
            CompositionTarget.Rendering -= OnRendering;

            if (_serialPort is not null)
            {
                _serialPort.DataReceived -= OnSerialDataReceived;
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }

            _isRunning = false;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _viewModel.LastUpdate = DateTimeOffset.Now;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            _viewModel.LastRender = DateTimeOffset.Now;
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var line = _serialPort?.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                ProcessSerialMessage(line);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read serial data: {ex.Message}");
            }
        }

        private void ProcessSerialMessage(string message)
        {
            var parts = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return;
            }

            var command = parts[0].ToUpperInvariant();
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            if (SerialCommands.TryGetValue(command, out var handler))
            {
                Application.Current.Dispatcher.Invoke(() => handler(_viewModel, args));
            }
        }

        public void Dispose()
        {
            Stop();
            _serialPort?.Dispose();
        }
    }
}
