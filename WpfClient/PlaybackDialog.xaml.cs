using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfClient.Services;

namespace WpfClient;

public partial class PlaybackDialog : Window
{
    private readonly System.Collections.Generic.List<SessionInfo> _sessions;
    private readonly Demo _playback;
    public int? SelectedSessionId { get; private set; }

    public PlaybackDialog(System.Collections.Generic.List<SessionInfo> sessions)
    {
        InitializeComponent();
        _sessions = sessions;
        _playback = new Demo(DatabaseConfig.ConnectionString, Application.Current.Dispatcher);
        SessionsListBox.ItemsSource = _sessions;
        if (_sessions.Count > 0)
        {
            SessionsListBox.SelectedIndex = 0;
        }
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _playback?.Dispose();
        base.OnClosed(e);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PlayButton.IsEnabled = SessionsListBox.SelectedItem is SessionInfo;
    }

    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (SessionsListBox.SelectedItem is SessionInfo session)
        {
            SelectedSessionId = session.Id;
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnItemBorderMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            var deleteButton = FindChild<Button>(border, "DeleteButton");
            if (deleteButton != null)
            {
                deleteButton.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnItemBorderMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            var deleteButton = FindChild<Button>(border, "DeleteButton");
            if (deleteButton != null)
            {
                deleteButton.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T && (child as FrameworkElement)?.Name == childName)
            {
                return child as T;
            }
            var childOfChild = FindChild<T>(child, childName);
            if (childOfChild != null)
            {
                return childOfChild;
            }
        }
        return null;
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SessionInfo session)
        {
            var result = MessageBox.Show(
                $"Вы уверены, что хотите удалить запись '{session.DrawingKey}'?\nЭто действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var deleted = await _playback.DeleteSessionAsync(session.Id);
                    if (deleted)
                    {
                        _sessions.Remove(session);
                        SessionsListBox.ItemsSource = null;
                        SessionsListBox.ItemsSource = _sessions;

                        // Обновляем выбранный индекс
                        if (SessionsListBox.Items.Count > 0)
                        {
                            SessionsListBox.SelectedIndex = 0;
                        }
                        else
                        {
                            PlayButton.IsEnabled = false;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Не удалось удалить запись.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении записи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

}


