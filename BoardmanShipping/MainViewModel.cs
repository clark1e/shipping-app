using BoardmanShipping.Services;
using BoardmanShipping.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace BoardmanShipping
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private DateTime _selectedDate;
        private ObservableCollection<SalesOrder> _orders = new();
        private ICollectionView _ordersView;
        private string _searchText = string.Empty;

        // Watermark options
        public IEnumerable<WatermarkType> PrintStatuses { get; } =
            Enum.GetValues(typeof(WatermarkType)).Cast<WatermarkType>();

        private WatermarkType _selectedWatermark = WatermarkType.None;
        public WatermarkType SelectedWatermark
        {
            get => _selectedWatermark;
            set
            {
                if (_selectedWatermark == value) return;
                _selectedWatermark = value;
                OnPropertyChanged(nameof(SelectedWatermark));
            }
        }

        public MainViewModel()
        {
            // Date navigation
            PrevDayCommand = new RelayCommand(_ => SelectedDate = SelectedDate.AddDays(-1));
            NextDayCommand = new RelayCommand(_ => SelectedDate = SelectedDate.AddDays(1));

            // Search
            SearchCommand = new RelayCommand(_ => DoSearch());

            // Sidebar/menu commands
            ShowDailyDiaryCommand = new RelayCommand(_ => { /* TODO */ });
            ShowWeeklyDiaryCommand = new RelayCommand(_ => { /* TODO */ });
            ShowMonthlyDiaryCommand = new RelayCommand(_ => { /* TODO */ });
            ImportOrdersCommand = new RelayCommand(_ => ShowImportWindow());
            SalesOrderDataCommand = new RelayCommand(_ => { /* TODO */ });
            ShowStatsCommand = new RelayCommand(_ => { /* TODO */ });
            AmendDatesCommand = new RelayCommand(_ => { /* TODO */ });
            PrintBarcodeCommand = new RelayCommand(_ => { /* TODO */ });
            PrintBoxLabelsCommand = new RelayCommand(_ => { /* TODO */ });
            ShowMiscCommand = new RelayCommand(_ => { /* TODO */ });
            TrackingInfoCommand = new RelayCommand(_ => { /* TODO */ });

            // Print daily diary
            PrintDailyDiaryCommand = new RelayCommand(_ => PrintDailyDiary());

            // Initialize
            _ordersView = CollectionViewSource.GetDefaultView(_orders);
            SelectedDate = DateTime.Today;
            SelectedWatermark = WatermarkType.None;
        }

        // Public properties & commands
        public ICollectionView OrdersView => _ordersView;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                LoadOrders();
            }
        }
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
            }
        }

        public int TotalQty { get; private set; }
        public double TotalWeight { get; private set; }
        public int TotalPallet { get; private set; }
        public int TotalBox { get; private set; }

        public ICommand PrevDayCommand { get; }
        public ICommand NextDayCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ShowDailyDiaryCommand { get; }
        public ICommand ShowWeeklyDiaryCommand { get; }
        public ICommand ShowMonthlyDiaryCommand { get; }
        public ICommand ImportOrdersCommand { get; }
        public ICommand SalesOrderDataCommand { get; }
        public ICommand ShowStatsCommand { get; }
        public ICommand AmendDatesCommand { get; }
        public ICommand PrintBarcodeCommand { get; }
        public ICommand PrintBoxLabelsCommand { get; }
        public ICommand ShowMiscCommand { get; }
        public ICommand TrackingInfoCommand { get; }
        public ICommand PrintDailyDiaryCommand { get; }

        // LoadOrders: fetch, hook, group, filter, totals
        private void LoadOrders()
        {
            _orders = OdbcDataService.GetOrders(SelectedDate);
            foreach (var so in _orders)
                so.PropertyChanged += SalesOrder_PropertyChanged;

            var cvs = new CollectionViewSource { Source = _orders };
            cvs.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SalesOrder.Acctname)));
            _ordersView = cvs.View;
            _ordersView.Filter = OrderFilter;
            OnPropertyChanged(nameof(OrdersView));

            TotalQty = _orders.Sum(o => o.Qty);
            TotalWeight = _orders.Sum(o => o.ItemWeight);
            TotalPallet = _orders.Sum(o => o.Pallet);
            TotalBox = _orders.Sum(o => o.Box);
            OnPropertyChanged(nameof(TotalQty));
            OnPropertyChanged(nameof(TotalWeight));
            OnPropertyChanged(nameof(TotalPallet));
            OnPropertyChanged(nameof(TotalBox));
        }

        // Search logic
        private void DoSearch()
        {
            var key = SearchText?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                LoadOrders(); return;
            }

            var dt = OdbcDataService.FindOrderDate(key);
            if (dt.HasValue)
                SelectedDate = dt.Value;
            else
                MessageBox.Show($"No order found matching '{SearchText}'", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Handle Completed toggles
        private void SalesOrder_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SalesOrder.Completed) && sender is SalesOrder so)
            {
                try { OdbcDataService.UpdateCompleted(so); }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update Completed: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Filter for SearchText
        private bool OrderFilter(object? obj)
        {
            if (string.IsNullOrEmpty(SearchText)) return true;
            if (obj is SalesOrder so)
                return so.Sonum.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || so.Custorderno.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            return false;
        }

        // Print daily diary with watermark
        private void PrintDailyDiary()
        {
            PrintService.PrintDailyDiary(_orders.ToList(), SelectedDate, SelectedWatermark);
        }

        // Show Import dialog
        private void ShowImportWindow()
        {
            var importWindow = new ImportSalesOrdersWindow();
            importWindow.Owner = Application.Current.MainWindow;
            importWindow.ShowDialog();
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
