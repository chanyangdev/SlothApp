using System.Windows;
using Sloth.Core.Models;
using Sloth.Core.Services;

namespace SlothApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        var c = new Customer(1, "C24001", "홍길동");
        LblOut.Text = HelloService.Hello(c);
    }
}
