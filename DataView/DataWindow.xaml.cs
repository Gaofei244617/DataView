using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.IO.Path;

namespace DataView
{
    public partial class DataWindow : Window
    {
        private bool _terminateFlag = false;

        private readonly ObservableCollection<DataItem> generalData = new ObservableCollection<DataItem>();               // 总数据
        private readonly ObservableCollection<DetailDataItem> detailData = new ObservableCollection<DetailDataItem>();    // 详细数据(以视频为单位)
        private readonly ObservableCollection<AlarmImage> alarmImageData = new ObservableCollection<AlarmImage>();  // 告警图片
        private readonly ObservableCollection<TestVideo> testVideoData = new ObservableCollection<TestVideo>();   // 测试视频

        private readonly ColumnSeries recall = new ColumnSeries
        {
            Title = "检出率",
            Values = new ChartValues<double>(),
            Fill = new SolidColorBrush(Color.FromRgb(36, 169, 225)),
            MaxColumnWidth = 25
        };

        private readonly ColumnSeries precision = new ColumnSeries
        {
            Title = "准确率",
            Values = new ChartValues<double>(),
            Fill = new SolidColorBrush(Color.FromRgb(107, 194, 53)),
            MaxColumnWidth = 25
        };

        private readonly ColumnSeries multiDetRate = new ColumnSeries
        {
            Title = "多检率",
            Values = new ChartValues<double>(),
            Fill = new SolidColorBrush(Color.FromRgb(248, 147, 29)),
            MaxColumnWidth = 25
        };

        public DataWindow()
        {
            InitializeComponent();

            DetailDataTab.LoadingRow += new EventHandler<DataGridRowEventArgs>(this.DataGrid_LoadingRow);
            DetailDataTab.DataContext = detailData;
            GeneralDataTab.DataContext = generalData;
            AlarmImageTab.DataContext = alarmImageData;
            TestVideoTab.LoadingRow += new EventHandler<DataGridRowEventArgs>(this.DataGrid_LoadingRow);
            TestVideoTab.DataContext = testVideoData;

            DataChart.Series.Add(recall);
            DataChart.Series.Add(precision);
            DataChart.Series.Add(multiDetRate);
        }

        // 添加告警图片
        public void AddAlarmImageItem(AlarmImage item)
        {
            alarmImageData.Add(item);
        }

        // 更新统计结果
        public void UpdateAlarmImageItem(AlarmImage item)
        {
            var _list = alarmImageData.Where(f => Path.GetFileName(f.ImagePath) == Path.GetFileName(item.ImagePath)).ToList();
            if (_list.Count == 0)
            {
                alarmImageData.Add(item);
            }
            else
            {
                _list[0] = item;
            }
        }

        public void SetDetailData(List<DetailDataItem> detailDataList)
        {
            detailData.Clear();
            foreach (var item in detailDataList)
            {
                detailData.Add(item);
            }
            UpdateGeneralData();
            UpdateChart();
        }

        private void UpdateGeneralData()
        {
            Dictionary<string, DataItem> dat = new Dictionary<string, DataItem>();
            // 汇总各事件应检、正检、误检、多检
            foreach (var item in detailData)
            {
                if (!dat.ContainsKey(item.Incident))
                {
                    dat[item.Incident] = new DataItem { Incident = item.Incident };
                }
                dat[item.Incident].ActualCount += item.ActualCount;
                dat[item.Incident].TrueDetect += item.TrueDetect;
                dat[item.Incident].FalseDetect += item.FalseDetect;
                dat[item.Incident].MultiDetect += item.MultiDetect;
            }

            generalData.Clear();
            // 检出率、有效率、多检率
            foreach (var item in dat)
            {
                double _actualCount = item.Value.ActualCount;
                double _trueDetect = item.Value.TrueDetect;
                double _falseDetect = item.Value.FalseDetect;
                double _multiDetect = item.Value.MultiDetect;

                // 检出率
                if (item.Value.ActualCount == 0)
                {
                    item.Value.Recall = 0;
                }
                else
                {
                    item.Value.Recall = _trueDetect / _actualCount;
                }

                // 有效率 and 多检率
                if (item.Value.TrueDetect + item.Value.FalseDetect + item.Value.MultiDetect == 0)
                {
                    item.Value.Precision = 0;
                    item.Value.MultiDetectRate = 0;
                }
                else
                {
                    item.Value.Precision = (_trueDetect + _multiDetect) / (_trueDetect + _falseDetect + _multiDetect);
                    item.Value.MultiDetectRate = _multiDetect / (_trueDetect + _falseDetect + _multiDetect);
                }
                generalData.Add(item.Value);
            }
        }

        private void UpdateChart()
        {
            AxisX.Labels = new List<string>();
            recall.Values.Clear();
            precision.Values.Clear();
            multiDetRate.Values.Clear();
            foreach (var item in generalData)
            {
                var _list = MainWindow.Incidents.Where(it => it.Name == item.Incident).ToList();
                AxisX.Labels.Add(_list.Count > 0 ? _list[0].Display : item.Incident);
                recall.Values.Add(item.Recall * 100);
                precision.Values.Add(item.Precision * 100);
                multiDetRate.Values.Add(item.MultiDetectRate * 100);
            }
        }

        // 添加测试视频
        public void AddTestVideoItem(TestVideo item)
        {
            testVideoData.Add(item);
        }

        public void Init()
        {
            generalData.Clear();
            detailData.Clear();
            alarmImageData.Clear();
            testVideoData.Clear();
            recall.Values.Clear();
            precision.Values.Clear();
            multiDetRate.Values.Clear();
        }

        // 自动增加行号
        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = e.Row.GetIndex() + 1;
        }

        // 关闭窗口
        private void OnWindowClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 隐藏窗口,并不实际关闭
            if (!_terminateFlag)
            {
                this.Hide();
                e.Cancel = true;
            }
        }

        // 强制关闭窗口
        public void Terminate()
        {
            _terminateFlag = true;
            this.Close();
        }
    }
}