using System;
using System.ComponentModel;

namespace BoardmanShipping.Models
{
    public class ImportItem : INotifyPropertyChanged
    {
        private bool _toImport;

        public long OrderNumber { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime? OrderDate { get; set; }

        public bool ToImport
        {
            get => _toImport;
            set
            {
                if (_toImport == value) return;
                _toImport = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToImport)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
