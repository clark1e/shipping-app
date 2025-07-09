using System.Windows;
using System.Windows.Input;

namespace BoardmanShipping
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter
                && DataContext is MainViewModel vm
                && vm.SearchCommand.CanExecute(null))
            {
                vm.SearchCommand.Execute(null);
                Keyboard.ClearFocus();
            }
        }
    }
}
