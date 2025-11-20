using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfClient.Controls
{
    public partial class PlaybackSessionItem : UserControl
    {
        public event EventHandler<object?>? DeleteRequested;

        public PlaybackSessionItem()
        {
            InitializeComponent();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // наружу отдаём DataContext (модель сессии)
            DeleteRequested?.Invoke(this, DataContext);
        }
    }
}
