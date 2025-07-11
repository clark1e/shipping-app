using System.Windows;
using System.Windows.Input;

namespace BoardmanShipping
{
    //  Code‑behind for MainWindow.xaml
    //  NOTE:  absolutely **no** XAML markup should live in this file.
    //  If you ever see a <Window ...> tag in here the compiler will choke
    //  with hundreds of "invalid token '<'" errors.
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// When the user presses ↵ in the search box we run the view‑model command.
        /// </summary>
        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            {
                vm.SearchCommand.Execute(null);
            }
        }
    }
}
