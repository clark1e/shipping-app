using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using BoardmanShipping.Models;
using BoardmanShipping.Services;

namespace BoardmanShipping.ViewModels
{
    public class ImportSalesOrdersViewModel : INotifyPropertyChanged
    {
        private string _filterOrderText = string.Empty;

        public string FilterOrderText
        {
            get => _filterOrderText;
            set
            {
                if (_filterOrderText == value) return;
                _filterOrderText = value;
                OnPropertyChanged(nameof(FilterOrderText));
            }
        }

        public ObservableCollection<ImportItem> SelectionList { get; } = new();

        public ICommand FindOrderCommand { get; }
        public ICommand ImportCheckedCommand { get; }

        public ImportSalesOrdersViewModel()
        {
            FindOrderCommand = new RelayCommand(_ => LoadSelection());
            ImportCheckedCommand = new RelayCommand(_ => DoImport());

            LoadSelection();
        }

        private void LoadSelection()
        {
            long? num = long.TryParse(FilterOrderText, out var n) ? n : (long?)null;

            var data = ImportService.LoadSelection(20, num);
            SelectionList.Clear();
            foreach (var it in data) SelectionList.Add(it);
        }

        private void DoImport()
        {
            try
            {
                ImportService.ImportSelected(SelectionList);
                MessageBox.Show("Import completed.",
                                "Import",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                LoadSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed:\n{ex.Message}",
                                "Import Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        #endregion
    }
}
