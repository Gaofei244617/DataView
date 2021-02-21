using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

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

    public class IntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int count = (int)value;
            return count.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return int.Parse(value as string);
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
            if (MainWindow.Incidents.Count(it => it.Name == str) > 0)
            {
                _incident = MainWindow.Incidents.Where(it => it.Name == str).ToList()[0].Display;
            }
            return _incident;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            if (MainWindow.Incidents.Count(it => it.Display == str) > 0)
            {
                return MainWindow.Incidents.Where(it => it.Display == str).ToList()[0].Name;
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
            string _scene = "UnKnown";
            if (MainWindow.Scenes.Count(it => it.Name == str) > 0)
            {
                _scene = MainWindow.Scenes.Where(it => it.Name == str).ToList()[0].Display;
            }
            return _scene;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = value as string;
            if (MainWindow.Scenes.Count(it => it.Display == str) > 0)
            {
                return MainWindow.Scenes.Where(it => it.Display == str).ToList()[0].Name;
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }

    // 路径转图片
    public class ImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string _path = value as string;
            if (File.Exists(_path))
            {
                if (_path.Length > 240)  // 规避文件名和路径名过长问题
                {
                    if (!Directory.Exists("./temp"))
                    {
                        Directory.CreateDirectory("./temp");
                    }
                    FileInfo fi = new FileInfo(_path);
                    string _tmp = Path.GetFileName(_path);
                    if (!File.Exists("./temp/" + _tmp))
                    {
                        fi.CopyTo("./temp/" + _tmp, true);
                    }
                    return new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"./temp/" + _tmp, UriKind.RelativeOrAbsolute));
                }
                else
                {
                    return new BitmapImage(new Uri(_path, UriKind.RelativeOrAbsolute));
                }
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

    // 提示标签
    public class StateLabelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] != DependencyProperty.UnsetValue &&
                values[1] != DependencyProperty.UnsetValue)
            {
                DetectType state = (DetectType)values[0];
                int count = (int)values[1];
                switch (state)
                {
                    case DetectType.UnKnown:
                        return "State: 未统计";

                    case DetectType.TrueDet:
                        return "State: 正检 [" + count.ToString() + "]";

                    case DetectType.FalseDet:
                        return "State: 误检 [" + count.ToString() + "]";

                    case DetectType.Ignore:
                        return "State: 不作统计";
                }
            }
            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                switch ((DetectType)value)
                {
                    case DetectType.UnKnown:
                        return Brushes.Black;

                    case DetectType.TrueDet:
                        return Brushes.Green;

                    case DetectType.FalseDet:
                        return Brushes.Red;

                    case DetectType.Ignore:
                        return Brushes.Gray;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StateToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            DetectType state = (DetectType)value;
            return state == (DetectType)int.Parse(parameter.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isChecked = (bool)value;
            if (!isChecked)
            {
                return null;
            }
            return (DetectType)int.Parse(parameter.ToString()); ;
        }
    }

    public class NumValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] != DependencyProperty.UnsetValue && values[1] != DependencyProperty.UnsetValue)
            {
                DetectType t = (DetectType)int.Parse(parameter.ToString());
                int val = (int)values[0];
                DetectType state = (DetectType)values[1];
                return t == state ? (double)val : 0;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            DetectType t = (DetectType)int.Parse(parameter.ToString());
            object[] objs = new object[2];
            double val = (double)value;
            objs[0] = (int)val;
            objs[1] = t;
            return objs;
        }
    }
}