using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfClient.Controls
{
    public partial class PlaybackControls : UserControl
    {
        // События, на которые подпишется MainWindow
        public event RoutedEventHandler? RecordRequested;
        public event RoutedEventHandler? PlaybackRequested;
        public event RoutedEventHandler? PauseResumeRequested;
        public event RoutedEventHandler? StopRequested;

        public PlaybackControls()
        {
            InitializeComponent();
        }

        private void OnRecordClick(object sender, RoutedEventArgs e)
        {
            RecordRequested?.Invoke(this, e);
        }

        private void OnPlaybackClick(object sender, RoutedEventArgs e)
        {
            PlaybackRequested?.Invoke(this, e);
        }

        private void OnPauseResumeClick(object sender, RoutedEventArgs e)
        {
            PauseResumeRequested?.Invoke(this, e);
        }

        private void OnStopPlaybackClick(object sender, RoutedEventArgs e)
        {
            StopRequested?.Invoke(this, e);
        }
    }
}
