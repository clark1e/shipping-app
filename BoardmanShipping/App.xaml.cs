using System.Text;
using System.Windows;          // already there in WPF apps
using System.Text.Encodings.Web;

namespace BoardmanShipping
{
    public partial class App : Application
    {
        static App()
        {
            // 🔑  enable 1252, 850, etc. for ODBC / OleDb drivers
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
