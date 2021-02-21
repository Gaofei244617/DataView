using System.ComponentModel;

namespace DataView
{
    public enum DetectType : int
    {
        UnKnown = 0,      // 未统计
        TrueDet = 1,      // 正检
        FalseDet = 2,     // 误检
        Ignore = 3        // 不作统计
    }

    public class NotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected internal void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class VideoInfo
    {
        public string Scene { get; set; }
        public string VideoName { get; set; }
        public string Incident { get; set; }
        public int Count { get; set; }
    }

    // 测试视频
    public class TestVideo
    {
        public string VideoName { get; set; }   // 视频名称(含后缀)
        public string VideoPath { get; set; }   // 视频绝对路径(不含文件名)
    }

    // 告警图片信息
    public class AlarmImage : NotifyPropertyChanged
    {
        private string _scene;          // 场景
        private string _incident;       // 事件
        private string _imgPath;        // 图片路径
        private string _video;          // 视频名
        private int _frame;             // 告警帧号
        private int _id;                // id
        private DetectType _state;      // 状态
        private int _count;             // 告警数量

        public string Scene
        {
            get { return _scene; }
            set { _scene = value; OnPropertyChanged("Scene"); }
        }

        public string Incident
        {
            get { return _incident; }
            set { _incident = value; OnPropertyChanged("Incident"); }
        }

        public string ImagePath
        {
            get { return _imgPath; }
            set { _imgPath = value; OnPropertyChanged("ImagePath"); }
        }

        public string Video
        {
            get { return _video; }
            set { _video = value; OnPropertyChanged("Video"); }
        }

        public int Frame
        {
            get { return _frame; }
            set { _frame = value; OnPropertyChanged("Frame"); }
        }

        public int ID
        {
            get { return _id; }
            set { _id = value; OnPropertyChanged("ID"); }
        }

        public DetectType State
        {
            get { return _state; }
            set
            {
                if (_state != value)
                {
                    _state = value;
                    IncidentCount = (value == DetectType.TrueDet || value == DetectType.FalseDet) ? 1 : 0;
                    OnPropertyChanged("State");
                }
            }
        }

        public int IncidentCount
        {
            get { return _count; }
            set { _count = value; OnPropertyChanged("IncidentCount"); }
        }
    }

    // 总体数据
    public class DataItem : NotifyPropertyChanged
    {
        private string _incident;             // 事件
        private int _actualCount = 0;         // 应检
        private int _trueDetect = 0;          // 正检
        private int _falseDetect = 0;         // 误检
        private int _multiDetect = 0;         // 多检
        private double _recall = 0;           // 检出率
        private double _precision = 0;        // 准确率
        private double _multiDetectRate = 0;  // 多检率

        public string Incident
        {
            get { return _incident; }
            set { _incident = value; OnPropertyChanged("Incident"); }
        }

        public int ActualCount
        {
            get { return _actualCount; }
            set { _actualCount = value; OnPropertyChanged("ActualCount"); }
        }

        public int TrueDetect
        {
            get { return _trueDetect; }
            set { _trueDetect = value; OnPropertyChanged("TrueDetect"); }
        }

        public int FalseDetect
        {
            get { return _falseDetect; }
            set { _falseDetect = value; OnPropertyChanged("FalseDetect"); }
        }

        public int MultiDetect
        {
            get { return _multiDetect; }
            set { _multiDetect = value; OnPropertyChanged("MultiDetect"); }
        }

        public double Recall
        {
            get { return _recall; }
            set { _recall = value; OnPropertyChanged("Recall"); }
        }

        public double Precision
        {
            get { return _precision; }
            set { _precision = value; OnPropertyChanged("Precision"); }
        }

        public double MultiDetectRate
        {
            get { return _multiDetectRate; }
            set { _multiDetectRate = value; OnPropertyChanged("MultiDetectRate"); }
        }
    }

    // 详细数据
    public class DetailDataItem : NotifyPropertyChanged
    {
        private string _scene;      // 场景
        private string _video;      // 视频
        private string _incident;   // 事件
        private int _actualCount;   // 应检
        private int _trueDetect;    // 正检
        private int _falseDetect;   // 误检
        private int _multiDetect;   // 多检

        public string Scene
        {
            get { return _scene; }
            set { _scene = value; OnPropertyChanged("Scene"); }
        }

        public string Video
        {
            get { return _video; }
            set { _video = value; OnPropertyChanged("Video"); }
        }

        public string Incident
        {
            get { return _incident; }
            set { _incident = value; OnPropertyChanged("Incident"); }
        }

        public int ActualCount
        {
            get { return _actualCount; }
            set { _actualCount = value; OnPropertyChanged("ActualCount"); }
        }

        public int TrueDetect
        {
            get { return _trueDetect; }
            set { _trueDetect = value; OnPropertyChanged("TrueDetect"); }
        }

        public int FalseDetect
        {
            get { return _falseDetect; }
            set { _falseDetect = value; OnPropertyChanged("FalseDetect"); }
        }

        public int MultiDetect
        {
            get { return _multiDetect; }
            set { _multiDetect = value; OnPropertyChanged("MultiDetect"); }
        }
    }

    public class IncidentCount
    {
        public int ActualCount = 0;   // 应检
        public int TrueDetect = 0;    // 正检
        public int FalseDetect = 0;   // 误检
        public int MultiDetect = 0;   // 多检
    }

    public class SceneItem
    {
        public string Name { get; set; }
        public string Display { get; set; }
    }

    public class IncidentItem
    {
        public string Name { get; set; }
        public string Display { get; set; }
    }
}