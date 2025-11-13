using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WpfClient
{
    public class PaintViewModel : INotifyPropertyChanged
    {
        private string _pictureType = "None";
        private ObservableCollection<string> _filledFigures = new();
        private double _cursorX;
        private double _cursorY;
        private string _selectedColor = "Black";
        private DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;
        private DateTimeOffset _lastRender = DateTimeOffset.MinValue;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PictureType
        {
            get => _pictureType;
            set => SetField(ref _pictureType, value);
        }

        public ObservableCollection<string> FilledFigures
        {
            get => _filledFigures;
            set => SetField(ref _filledFigures, value);
        }

        public double CursorX
        {
            get => _cursorX;
            set => SetField(ref _cursorX, value);
        }

        public double CursorY
        {
            get => _cursorY;
            set => SetField(ref _cursorY, value);
        }

        public string SelectedColor
        {
            get => _selectedColor;
            set => SetField(ref _selectedColor, value);
        }

        public DateTimeOffset LastUpdate
        {
            get => _lastUpdate;
            set => SetField(ref _lastUpdate, value);
        }

        public DateTimeOffset LastRender
        {
            get => _lastRender;
            set => SetField(ref _lastRender, value);
        }

        protected void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
