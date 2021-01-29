using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DataView
{
    /// <summary>
    /// MessageWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MessageWindow : Window
    {
        public MessageWindow()
        {
            InitializeComponent();
        }

        // 窗口拖动
        public void MousePress(object o, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        // 关闭关窗口
        private void Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public static void Show(string msg)
        {
            var win = new MessageWindow();
            win.Text.Text = msg;
            win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            win.Show();
        }
        public static void ShowDialog(string msg)
        {
            var win = new MessageWindow();
            win.Text.Text = msg;
            win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            win.ShowDialog();
        }

        public static void Show(string msg, Window parent)
        {
            var win = new MessageWindow();
            win.Owner = parent;
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.Text.Text = msg;
            win.Show();
        }

        public static void ShowDialog(string msg, Window parent)
        {
            var win = new MessageWindow();
            win.Owner = parent;
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.Text.Text = msg;
            win.ShowDialog();
        }

    }
}
