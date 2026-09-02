using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using Microsoft.UI.Xaml.Media;
using System.ComponentModel;

namespace Sheet_Music_App
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool b = false;
            if (value is bool vb) b = vb;
            bool invert = (parameter as string) == "Invert";
            if (invert) b = !b;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility v) return v == Visibility.Visible;
            return false;
        }
    }

    public class BoolToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool b = false;
            if (value is bool vb) b = vb;
            return b ? Microsoft.UI.Xaml.Controls.Symbol.Save : Microsoft.UI.Xaml.Controls.Symbol.Edit;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return false;
        }
    }

    public class PieceEditableViewModel : INotifyPropertyChanged
    {
        public Guid Id { get; set; }
        private int _index;
        public int Index { get => _index; set { _index = value; Notify(); } }
        private string _title = string.Empty;
        public string Title { get => _title; set { _title = value; Notify(); } }
        private string _composer = string.Empty;
        public string Composer { get => _composer; set { _composer = value; Notify(); } }
        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set { _isEditing = value; Notify(); } }
        private bool _canMoveUp;
        public bool CanMoveUp { get => _canMoveUp; set { _canMoveUp = value; Notify(); } }
        private bool _canMoveDown;
        public bool CanMoveDown { get => _canMoveDown; set { _canMoveDown = value; Notify(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([System.Runtime.CompilerServices.CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
