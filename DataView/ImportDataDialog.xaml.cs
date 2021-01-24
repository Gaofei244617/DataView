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
    public partial class ImportDataDialog : Window
    {
        static public string alarmImagePath = null;
        static public string videoPath = null;
        static public string xmlFile = null;
        public ImportDataDialog()
        {
            InitializeComponent();
        }

        // 窗口拖动
        public void MousePress(object o, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Click_OK(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Click_Close(object sender, RoutedEventArgs e)
        {
            alarmImagePath = null;
            videoPath = null;
            xmlFile = null;
            this.Close();
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void TextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            TextBox txtBox = (TextBox)sender;
            txtBox.Text = files[0];

            // 标注文件路径
            xmlFile = (XmlFile.Text != null && XmlFile.Text.Length > 0) ? XmlFile.Text : null;
            // 测试视频路径
            videoPath = (VideoPath.Text != null && VideoPath.Text.Length > 0) ? VideoPath.Text : null;
            // 告警图片路径
            alarmImagePath = (AlarmImagePath.Text != null && AlarmImagePath.Text.Length > 0) ? AlarmImagePath.Text : null;
        }
    }
}
