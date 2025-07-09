using System;
using System.ComponentModel;

namespace BoardmanShipping
{
    public class SalesOrder : INotifyPropertyChanged
    {
        public string Acctname { get; set; } = string.Empty;
        public int Sonum { get; set; }
        public string Partno { get; set; } = string.Empty;
        public string Analysis1 { get; set; } = string.Empty;
        public string Itemdesc { get; set; } = string.Empty;
        public bool OnHold { get; set; }
        public string TrackStatus { get; set; } = string.Empty;
        public string Custorderno { get; set; } = string.Empty;

        private bool completed;
        public bool Completed
        {
            get => completed;
            set
            {
                if (completed != value)
                {
                    completed = value;
                    OnPropertyChanged(nameof(Completed));
                }
            }
        }

        public int Qty { get; set; }
        public DateTime DelDate { get; set; }
        public double ItemWeight { get; set; }
        public string NotesLine1 { get; set; } = string.Empty;
        public int Pallet { get; set; }
        public int Box { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
