using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DataView
{
    public class DetectTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return DependencyProperty.UnsetValue;
            }

            DetectType detType = (DetectType)value;
            string strDetType = null;
            switch (detType)
            {
                case DetectType.UnKnown:
                    strDetType = "尚未统计";
                    break;
                case DetectType.TrueDet:
                    strDetType = "正检";
                    break;
                case DetectType.FalseDet:
                    strDetType = "误检";
                    break;
                case DetectType.Ignore:
                    strDetType = "不作统计";
                    break;
            }
            return strDetType;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            if (str == "尚未统计")
            {
                return DetectType.UnKnown;
            }
            else if (str == "正检")
            {
                return DetectType.TrueDet;

            }
            else if (str == "误检")
            {
                return DetectType.FalseDet;
            }
            else if (str == "不作统计")
            {
                return DetectType.Ignore;
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }

    public class DoubleToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return DependencyProperty.UnsetValue;
            }
            return (Math.Round((double)value * 100, 1)).ToString() + "%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            return double.Parse(str.TrimEnd(new char[] { '%', ' ' })) / 100d;
        }
    }

    // 事件类型转中文
    public class IncidentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return DependencyProperty.UnsetValue;
            }
            string str = value as string;
            string _incident = "UnKnown";
            if (MainWindow.IncidentDict.ContainsKey(str))
            {
                _incident = MainWindow.IncidentDict[str];
            }
            return _incident;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            if (MainWindow.IncidentDict.ContainsValue(str))
            {
                return MainWindow.IncidentDict.Where(f => f.Value == str).FirstOrDefault().Key;
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }

    // 场景类型转中文
    public class SceneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return DependencyProperty.UnsetValue;
            }
            string str = value as string;
            string _scene = "UnKnownfff";
            if (MainWindow.SceneDict.ContainsKey(str))
            {
                _scene = MainWindow.SceneDict[str];
            }
            return _scene;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            if (MainWindow.SceneDict.ContainsValue(str))
            {
                return MainWindow.SceneDict.Where(f => f.Value == str).FirstOrDefault().Key;
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }

    // 路径转图片
    public class StringToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = (string)value;
            if (!string.IsNullOrEmpty(path))
            {
                return new BitmapImage(new Uri(path, UriKind.RelativeOrAbsolute));
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
