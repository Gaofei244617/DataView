using Microsoft.Win32;
using Panuon.UI.Silver;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using Alphaleonis.Win32.Filesystem;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Directory = Alphaleonis.Win32.Filesystem.Directory;

namespace DataView
{
    public partial class MainWindow : Window
    {
        private string _aboutInfo = "交通事件指标统计系统" + "\n" + "版 本: V0.9";
        private DataWindow dataWindow = new DataWindow();

        public static Dictionary<string, string> SceneDict = new Dictionary<string, string>();
        public static Dictionary<string, string> IncidentDict = new Dictionary<string, string>();

        private List<AlarmDataItem> alarmImageListAll = new List<AlarmDataItem>();   // 所有告警图片
        private List<AlarmDataItem> alarmImageList = new List<AlarmDataItem>();      // 正在统计的告警图片
        private readonly List<VideoInfoItem> videoInfoList = new List<VideoInfoItem>();    // 视频应报事件
        private readonly List<TestVideoItem> testVideoList = new List<TestVideoItem>();    // 测试视频(视频名，所在路径)

        private int _index = -1;          // 当前告警图片索引
        private DetectType _state = 0;    // 告警状态
        private int _detCount = 0;        // 事件数

        private SQLiteConnection dbConnection = null;  // 数据库连接

        /************************************************************************************/

        // 默认构造
        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists("./MetaData.xml"))
            {
                // 元数据
                XmlDocument doc = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreComments = true;  // 忽略文档里面的注释
                XmlReader reader = XmlReader.Create("./MetaData.xml", settings);
                doc.Load(reader);
                XmlNode xn = doc.SelectSingleNode("root"); // 根节点
                XmlNodeList xnl = xn.ChildNodes;  // 根节点的子节点
                SceneDict.Clear();
                IncidentDict.Clear();
                foreach (XmlNode node in xnl)
                {
                    if (node.Name == "scene") // 场景下拉列表
                    {
                        XmlNodeList subNodes = node.ChildNodes;
                        foreach (XmlNode item in subNodes)
                        {
                            SceneDict.Add(item.Attributes["tag"].Value, item.InnerText);
                        }
                    }

                    if (node.Name == "incident") // 事件下拉列表
                    {
                        XmlNodeList subNodes = node.ChildNodes;
                        foreach (XmlNode item in subNodes)
                        {
                            var it = new ComboBoxItem();
                            it.Content = item.InnerText;
                            this.IncidentComBox.Items.Add(it);
                            IncidentDict.Add(item.Attributes["tag"].Value, item.InnerText);
                        }
                    }
                }
                reader.Close();
            }
            else
            {
                MessageWindow.ShowDialog("Can not read MetaData.xml", this);
            }

            Resume();
        }

        private void InitMemoryData(string alarmImagePath, string videoPath, string xmlFile)
        {
            try
            {
                // 数据初始化
                alarmImageListAll.Clear();
                videoInfoList.Clear();
                testVideoList.Clear();
                SceneComBox.Items.Clear();
                _index = -1;
                _state = 0;
                _detCount = 0;

                // 标注文件
                if (xmlFile != null && xmlFile.Length > 0)
                {
                    XmlDocument doc = new XmlDocument();
                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.IgnoreComments = true;  // 忽略文档里面的注释
                    XmlReader reader = XmlReader.Create(xmlFile, settings);
                    doc.Load(reader);
                    XmlNode xn = doc.SelectSingleNode("root"); // 根节点
                    XmlNodeList xnl = xn.ChildNodes;  // 根节点的子节点
                    List<string> _tmpComboBoxList = new List<string>();
                    foreach (XmlNode node in xnl)
                    {
                        string _video = node.Attributes["name"].Value;
                        string _scene = node.Attributes["scene"].Value;
                        XmlNodeList subNodes = node.ChildNodes;
                        if (subNodes.Count > 0)
                        {
                            foreach (XmlNode item in subNodes)
                            {
                                string _incident = item.Name;
                                int _count = item.InnerText.ToInt().Value;
                                var _videoInfoItem = new VideoInfoItem
                                {
                                    Scene = _scene,
                                    VideoName = _video,
                                    Incident = _incident,
                                    Count = _count
                                };
                                videoInfoList.Add(_videoInfoItem);
                                if (_tmpComboBoxList.Count(f => f == _scene) == 0)
                                {
                                    _tmpComboBoxList.Add(_scene);
                                    var it = new ComboBoxItem();
                                    it.Content = SceneDict.ContainsKey(_scene) ? SceneDict[_scene] : _scene;
                                    if (!SceneDict.ContainsKey(_scene))
                                    {
                                        SceneDict[_scene] = _scene;
                                    }
                                    this.SceneComBox.Items.Add(it);
                                }
                            }
                        }
                        else // 无事件发生
                        {
                            var _videoInfoItem = new VideoInfoItem
                            {
                                Scene = _scene,
                                VideoName = _video,
                                Incident = "null",
                                Count = 0
                            };
                            videoInfoList.Add(_videoInfoItem);
                        }
                    }
                    reader.Close();

                    // 默认选项
                    var _comboBoxItem = new ComboBoxItem();
                    _comboBoxItem.Content = "UnKnown";
                    SceneDict["UnKnown"] = "UnKnown";
                    this.SceneComBox.Items.Add(_comboBoxItem);
                }

                // 告警图片
                if (alarmImagePath != null && alarmImagePath.Length > 0)
                {
                    SetImageTreeView(alarmImagePath); // 告警图片目录树
                    this.Title = "Start to import alarm images ...";
                    string[] alarmImages = Utility.Director(alarmImagePath).Where(f =>
                    {
                        string ex = Path.GetExtension(f);
                        return (ex == ".jpg" || ex == ".png" || ex == ".bmp");
                    }).ToArray();

                    // 图片命名方式: 视频名___事件_帧号.jpg
                    this.Title = "Start to parse alarm images ...";
                    int _id = 0;
                    foreach (string img in alarmImages)
                    {
                        string name = Path.GetFileNameWithoutExtension(img);
                        string[] strs = Regex.Split(name, "___", RegexOptions.IgnoreCase);
                        if (strs.Length < 2)
                        {
                            MessageWindow.Show("告警图片命名格式错误\n" + img, this);
                            continue;
                        }
                        string[] infos = strs[1].Split('_');
                        string _scene;
                        string _incident = infos[0];
                        string _video = strs[0];
                        var _list = videoInfoList.Where(f => Path.GetFileNameWithoutExtension(f.VideoName) == _video).ToList();
                        if (_list.Count > 0)
                        {
                            _video = _list[0].VideoName;
                            _scene = _list[0].Scene;
                        }
                        else
                        {
                            _video += ".h264";
                            _scene = "UnKnown";
                        }
                        if (!SceneDict.ContainsKey(_scene))
                        {
                            _scene = "UnKnown";
                        }
                        if (!IncidentDict.ContainsKey(_incident))
                        {
                            _incident = "UnKnown";
                        }

                        var _alarmDataItem = new AlarmDataItem
                        {
                            ID = _id++,
                            ImagePath = img,
                            Video = _video,
                            Scene = _scene,
                            Incident = _incident,
                            Frame = Convert.ToInt32(infos[1]),
                            State = DetectType.UnKnown,
                            Count = 0
                        };
                        alarmImageListAll.Add(_alarmDataItem);
                        dataWindow.AddAlarmImageItem(_alarmDataItem);
                    }
                }

                // 测试视频
                this.Title = "Start to import test videos ...";
                if (videoPath != null && videoPath.Length > 0)
                {
                    var videos = Utility.Director(videoPath).Where(f =>
                    {
                        string ex = Path.GetExtension(f);
                        return (ex == ".ts" || ex == ".mp4" || ex == ".flv");
                    }).ToList();

                    foreach (var item in videos)
                    {
                        string _videoName = Path.GetFileName(item);
                        string _VideoPath = Path.GetDirectoryName(item);
                        var _testVideoItem = new TestVideoItem { VideoName = _videoName, VideoPath = _VideoPath };
                        testVideoList.Add(_testVideoItem);
                        dataWindow.AddTestVideoItem(_testVideoItem);
                    }
                }

                // 详细数据
                dataWindow.SetDetailData(GetDetailData());
            }
            catch (Exception e)
            {
                MessageWindow.ShowDialog(e.ToString(), this);
            }
        }

        // 创建空数据表
        private void CreateBlankTabs()
        {
            string dbPath = "Data Source = " + AppDomain.CurrentDomain.BaseDirectory + @"data.db";
            dbConnection = new SQLiteConnection(dbPath);
            if (dbConnection == null)
            {
                MessageWindow.ShowDialog("无法连接到数据库", this);
                return;
            }
            dbConnection.Open();

            SQLiteCommand cmd = new SQLiteCommand { Connection = dbConnection };
            cmd.CommandText = "BEGIN";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS DataPathTab(Item VARCHAR(64) UNIQUE, Path VARCHAR(512))";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS AlarmImageTab" +
                "(id INTEGER, image VARCHAR(512) PRIMARY KEY, scene VARCHAR(64), incident VARCHAR(64), video VARCHAR(256), frame INTEGER, state INTEGER, count INTEGER)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS TestVideoTab(VideoName VARCHAR(256), Directory VARCHAR(256))";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS VideoInfoTab(Scene VARCHAR(64), Video VARCHAR(256), Incident VARCHAR(64), Count INTEGER)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "COMMIT";
            cmd.ExecuteNonQuery();
        }

        private void InitDataBase(string alarmImagePath, string videoPath, string xmlFile)
        {
            dbConnection?.Close();
            dbConnection = null;
            // 删除旧文件
            if (File.Exists("./data.db"))
            {
                string ex = DateTime.Now.ToString("yyyyMMddHHmmss");
                FileInfo fi = new FileInfo("./data.db");
                try
                {
                    fi.MoveTo("./data.db." + ex);
                }
                catch (IOException e)
                {
                    MessageWindow.ShowDialog(e.ToString(), this);
                }
            }

            // 创建数据表
            CreateBlankTabs();

            /********************* DataPathTab 数据更新 ********************/
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.Connection = dbConnection;

            cmd.CommandText = "BEGIN";
            cmd.ExecuteNonQuery();
            cmd.CommandText = string.Format($"INSERT INTO DataPathTab(Item, Path) VALUES ('AlarmImagePath', '{alarmImagePath}')");
            cmd.ExecuteNonQuery();
            cmd.CommandText = string.Format($"INSERT INTO DataPathTab(Item, Path) VALUES ('VideoPath', '{videoPath}')");
            cmd.ExecuteNonQuery();
            cmd.CommandText = string.Format($"INSERT INTO DataPathTab(Item, Path) VALUES ('VideoInfoFilePath', '{xmlFile}')");
            cmd.ExecuteNonQuery();
            cmd.CommandText = "COMMIT";
            cmd.ExecuteNonQuery();

            /********************* AlarmImageTab 数据更新 ********************/
            cmd.CommandText = "BEGIN";
            cmd.ExecuteNonQuery();
            foreach (var item in alarmImageListAll)
            {
                cmd.CommandText = string.Format($"INSERT INTO AlarmImageTab(id, image, scene, incident, video, frame, state, count)" +
                    $" VALUES ('{item.ID}', '{item.ImagePath}','{item.Scene}','{item.Incident}', '{item.Video}', '{item.Frame}', '{(int)item.State}', '{item.Count}')");
                cmd.ExecuteNonQuery();
            }
            cmd.CommandText = "COMMIT";
            cmd.ExecuteNonQuery();

            /************************** TestVideoTab 数据更新 ************************************/
            cmd.CommandText = "BEGIN";
            cmd.ExecuteNonQuery();
            foreach (var item in testVideoList)
            {
                cmd.CommandText = string.Format($"INSERT INTO TestVideoTab(VideoName, Directory) VALUES ('{item.VideoName}', '{item.VideoPath}')");
                cmd.ExecuteNonQuery();
            }
            cmd.CommandText = "COMMIT";
            cmd.ExecuteNonQuery();

            /************************** MarkVideoTab 数据更新 ************************************/
            cmd.CommandText = "BEGIN";
            cmd.ExecuteNonQuery();
            foreach (var item in videoInfoList)
            {
                cmd.CommandText = string.Format($"INSERT INTO VideoInfoTab(Scene, Video, Incident, Count) " +
                    $"VALUES ('{item.Scene}', '{item.VideoName}', '{item.Incident}', '{item.Count}')");
                cmd.ExecuteNonQuery();
            }
            cmd.CommandText = "COMMIT";
            cmd.ExecuteNonQuery();
        }

        public void InitData(string alarmImagePath, string videoPath, string xmlFile)
        {
            InitMemoryData(alarmImagePath, videoPath, xmlFile);
            InitDataBase(alarmImagePath, videoPath, xmlFile);
            MessageWindow.ShowDialog("数据初始化完成", this);
        }

        // 恢复上次状态
        public void Resume()
        {
            if (!File.Exists("./data.db"))
            {
                CreateBlankTabs();
                return;
            }

            string DBPath = "Data Source = " + AppDomain.CurrentDomain.BaseDirectory + @"data.db";
            dbConnection = new SQLiteConnection(DBPath);
            if (dbConnection == null)
            {
                MessageWindow.ShowDialog("无法连接到数据库 data.db", this);
                return;
            }
            dbConnection.Open();

            SQLiteCommand cmd = new SQLiteCommand();
            cmd.Connection = dbConnection; // 连接数据库

            // 标注视频
            this.Title = "Start to read data frome VideoInfoTab ...";
            cmd.CommandText = "SELECT * FROM VideoInfoTab";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                List<string> _tmpComboBoxList = new List<string>();
                while (dataReader.Read())
                {
                    //VideoDataTab(Scene, Video, Incident, Count)
                    string _scene = dataReader.GetString(0);
                    string _video = dataReader.GetString(1);
                    string _incident = dataReader.GetString(2);
                    int _count = dataReader.GetInt32(3);

                    var _videoInfoItem = new VideoInfoItem
                    {
                        Scene = _scene,
                        VideoName = _video,
                        Incident = _incident,
                        Count = _count
                    };
                    videoInfoList.Add(_videoInfoItem);
                    if (_tmpComboBoxList.Count(f => f == _scene) == 0)
                    {
                        _tmpComboBoxList.Add(_scene);
                        var it = new ComboBoxItem
                        {
                            Content = SceneDict.ContainsKey(_scene) ? SceneDict[_scene] : _scene
                        };
                        if (!SceneDict.ContainsKey(_scene))
                        {
                            SceneDict[_scene] = _scene;
                        }
                        this.SceneComBox.Items.Add(it);
                    }
                }
            }

            // 告警图片
            this.Title = "Start to read data frome AlarmImageTab ...";
            cmd.CommandText = "SELECT * FROM AlarmImageTab";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                while (dataReader.Read())
                {
                    //(id INTEGER, image VARCHAR(512) PRIMARY KEY, scene VARCHAR(64), incident VARCHAR(64), video VARCHAR(256), frame INTEGER, state INTEGER)
                    int _id = dataReader.GetInt32(0);
                    string _imgPath = dataReader.GetString(1);
                    string _scene = dataReader.GetString(2);
                    string _incident = dataReader.GetString(3);
                    string _videoName = dataReader.GetString(4);
                    int _frame = dataReader.GetInt32(5);
                    int _state = dataReader.GetInt32(6);
                    int _count = dataReader.GetInt32(7);

                    var _alarmDataItem = new AlarmDataItem
                    {
                        ID = _id,
                        ImagePath = _imgPath,
                        Video = _videoName,
                        Scene = _scene,
                        Incident = _incident,
                        Frame = _frame,
                        State = (DetectType)_state,
                        Count = _count
                    };

                    alarmImageListAll.Add(_alarmDataItem);
                    dataWindow.AddAlarmImageItem(_alarmDataItem);
                }
            }

            // 测试视频
            this.Title = "Start to read data frome TestVideoTab ...";
            cmd.CommandText = "SELECT * FROM TestVideoTab";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                while (dataReader.Read())
                {
                    //(VideoName VARCHAR(256) PRIMARY KEY, Directory VARCHAR(256))
                    string _videoName = dataReader.GetString(0);
                    string _videoPath = dataReader.GetString(1);
                    var _testVideoItem = new TestVideoItem { VideoName = _videoName, VideoPath = _videoPath };
                    testVideoList.Add(_testVideoItem);
                    dataWindow.AddTestVideoItem(_testVideoItem);
                }
            }

            // 告警图片路径
            cmd.CommandText = "SELECT * FROM DataPathTab WHERE Item = 'AlarmImagePath'";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                if (dataReader.Read())
                {
                    string _imagePath = dataReader.GetString(1);
                    if (Directory.Exists(_imagePath))
                    {
                        SetImageTreeView(_imagePath);
                    }
                    else
                    {
                        MessageWindow.ShowDialog("无法访问告警图片路径 [" + _imagePath + "]");
                    }
                }
            }

            // 上一次统计的告警图片路径
            cmd.CommandText = "SELECT * FROM DataPathTab WHERE Item = 'LastImagePath'";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                if (dataReader.Read())
                {
                    string _dir = dataReader.GetString(1);
                    if (Directory.Exists(_dir))
                    {
                        foreach (var item in alarmImageListAll)
                        {
                            if (item.ImagePath.Contains(_dir))
                            {
                                alarmImageList.Add(item);
                            }
                        }
                    }
                    else
                    {
                        MessageWindow.ShowDialog("无法访问告警图片路径 [" + _dir + "]");
                    }
                }
            }

            // 界面显示图片
            if (alarmImageList.Count > 0)
            {
                _index = 0;
                _state = alarmImageList[0].State;
                _detCount = alarmImageList[0].Count;
                ShowImage(alarmImageList[_index]);
            }

            // 更细统计数据
            dataWindow.SetDetailData(GetDetailData());
        }

        // 显示图片
        private void ShowImage(AlarmDataItem alarmImage)
        {
            if (!File.Exists(alarmImage.ImagePath))
            {
                return;
            }
            if (alarmImage.ImagePath.Length > 240)  // 规避文件名和路径名过长问题
            {
                FileInfo fi = new FileInfo(alarmImage.ImagePath);
                fi.CopyTo("temp.jpg", true);
                imgView.Source = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + @"temp.jpg", UriKind.RelativeOrAbsolute));
            }
            else
            {
                imgView.Source = new BitmapImage(new Uri(alarmImage.ImagePath, UriKind.RelativeOrAbsolute));
            }
            this.Title = "[" + (_index + 1) + "/" + alarmImageList.Count + "] " + Path.GetFileName(alarmImage.ImagePath);
            int _detCount = alarmImage.Count;

            // 告警结果
            switch (alarmImage.State)
            {
                case DetectType.TrueDet:
                    this.trueDetBtn.IsChecked = true;
                    this.trueDetNumBox.Value = _detCount;
                    break;

                case DetectType.FalseDet:
                    this.falseDetBtn.IsChecked = true;
                    this.falseDetNumBox.Value = _detCount;
                    break;

                case DetectType.Ignore:
                    this.ignoreBtn.IsChecked = true;
                    break;

                default:
                    this.trueDetBtn.IsChecked = false;
                    this.trueDetNumBox.Value = 0;
                    this.falseDetBtn.IsChecked = false;
                    this.falseDetNumBox.Value = 0;
                    this.ignoreBtn.IsChecked = false;
                    break;
            }

            // 场景ComboBox
            foreach (var item in this.SceneComBox.Items)
            {
                var it = (ComboBoxItem)item;
                if ((string)it.Content == SceneDict[alarmImage.Scene])
                {
                    this.SceneComBox.SelectedItem = item;
                    break;
                }
            }

            // 事件ComboBox
            foreach (var item in this.IncidentComBox.Items)
            {
                var it = (ComboBoxItem)item;
                if ((string)it.Content == IncidentDict[alarmImage.Incident])
                {
                    this.IncidentComBox.SelectedItem = item;
                    break;
                }
            }

            if (alarmImage.State == DetectType.UnKnown &&
                videoInfoList.Count(f => f.VideoName == alarmImage.Video) == 0)
            {
                MessageWindow.Show("告警图片视频不在统计范围" + "\n" + alarmImage.Video, this);
            }

            UpdateStateLabel(alarmImage.State, alarmImage.Count);
        }

        // 查看数据
        private void ViewDataClick(object sender, RoutedEventArgs e)
        {
            if (dataWindow == null)
            {
                dataWindow = new DataWindow();
            }
            dataWindow.Show();
        }

        // 打开图片位置
        private void OpenImageDirClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Path.GetDirectoryName(alarmImageList[_index].ImagePath));
        }

        // 打开视频位置
        private void OpenVideoDirClick(object sender, RoutedEventArgs e)
        {
            bool flag = false;
            foreach (var item in testVideoList)
            {
                if (item.VideoName == alarmImageList[_index].Video)
                {
                    System.Diagnostics.Process.Start(item.VideoPath);
                    flag = true;
                    break;
                }
            }

            if (!flag)
            {
                MessageWindow.ShowDialog("无法打开视频 [" + alarmImageListAll[_index].Video + "] 所在位置！", this);
            }
        }

        // 打开视频文件
        private void OpenVideoClick(object sender, RoutedEventArgs e)
        {
            bool flag = false;
            foreach (var item in testVideoList)
            {
                if (item.VideoName == alarmImageList[_index].Video)
                {
                    System.Diagnostics.Process.Start(Path.Combine(item.VideoPath, item.VideoName));
                    flag = true;
                    break;
                }
            }

            if (!flag)
            {
                MessageWindow.ShowDialog("未找到视频 [" + alarmImageListAll[_index].Video + "]", this);
            }
        }

        // 更新数据
        private void UpdateData(AlarmDataItem item)
        {
            // 更新数据库
            if (dbConnection != null)
            {
                SQLiteCommand cmd = new SQLiteCommand
                {
                    Connection = dbConnection,
                    CommandText = string.Format($"UPDATE AlarmImageTab SET state = '{(int)_state}', count = '{_detCount}' WHERE image = '{alarmImageList[_index].ImagePath}'")
                };
                cmd.ExecuteNonQuery();
            }

            // 更新UI数据
            dataWindow.UpdateAlarmImageItem(alarmImageList[_index]);
            UpdateStateLabel(_state, _detCount);

            /*********************** 统计结果(详细数据) ********************/
            dataWindow.SetDetailData(GetDetailData());
        }

        // 识别正检/误检
        private void SetImageStateClick(object sender, RoutedEventArgs e)
        {
            this.trueDetNumBox.Value = 0;
            this.falseDetNumBox.Value = 0;

            var btn = (RadioButton)sender;
            if ((string)btn.Content == "正检")
            {
                _state = DetectType.TrueDet;
                _detCount = 1;
                this.trueDetNumBox.Value = 1;
            }
            else if ((string)btn.Content == "误检")
            {
                _state = DetectType.FalseDet;
                _detCount = 1;
                this.falseDetNumBox.Value = 1;
            }
            else if ((string)btn.Content == "不作统计")
            {
                _state = DetectType.Ignore;
                _detCount = 0;
            }
            alarmImageList[_index].State = _state;
            alarmImageList[_index].Count = _detCount;
            UpdateData(alarmImageList[_index]);
        }

        private void NumBoxChanged(object sender, RoutedEventArgs e)
        {
            var item = (HandyControl.Controls.NumericUpDown)sender;
            _state = DetectType.UnKnown;
            if (item == this.trueDetNumBox && this.trueDetBtn.IsChecked == true) // 正检
            {
                _state = DetectType.TrueDet;
            }
            else if (item == this.falseDetNumBox && this.falseDetBtn.IsChecked == true) // 误检
            {
                _state = DetectType.FalseDet;
            }
            else if (this.ignoreBtn.IsChecked == true)
            {
                _state = DetectType.Ignore;
            }
            _detCount = (int)item.Value;

            alarmImageList[_index].State = _state;
            alarmImageList[_index].Count = _detCount;
            UpdateData(alarmImageList[_index]);
        }

        // 更新状态标签
        private void UpdateStateLabel(DetectType state, int count)
        {
            switch (state)
            {
                case DetectType.UnKnown:
                    this.StateLabel.Content = "State: 未统计";
                    this.StateLabel.Foreground = Brushes.Black;
                    break;

                case DetectType.TrueDet:
                    this.StateLabel.Content = "State: 正检 [" + _detCount.ToString() + "]";
                    this.StateLabel.Foreground = Brushes.Green;
                    break;

                case DetectType.FalseDet:
                    this.StateLabel.Content = "State: 误检 [" + _detCount.ToString() + "]";
                    this.StateLabel.Foreground = Brushes.Red;
                    break;

                case DetectType.Ignore:
                    this.StateLabel.Content = "State: 不作统计";
                    this.StateLabel.Foreground = Brushes.Gray;
                    break;
            }
        }

        // 计算每个视频的统计结果(应检/正检/误检/多检)
        private List<DetailDataItem> GetDetailData()
        {
            Dictionary<string, Dictionary<string, Dictionary<string, IncidentCount>>> dat = new Dictionary<string, Dictionary<string, Dictionary<string, IncidentCount>>>();
            foreach (var item in videoInfoList)
            {
                if (!dat.ContainsKey(item.Scene))
                {
                    dat[item.Scene] = new Dictionary<string, Dictionary<string, IncidentCount>>();
                }
                if (!dat[item.Scene].ContainsKey(item.VideoName))
                {
                    dat[item.Scene][item.VideoName] = new Dictionary<string, IncidentCount>();
                }
                dat[item.Scene][item.VideoName][item.Incident] = new IncidentCount { ActualCount = item.Count };
            }

            foreach (var item in alarmImageListAll)
            {
                if (item.State == DetectType.UnKnown || item.State == DetectType.Ignore)
                {
                    continue;
                }
                if (!dat.ContainsKey(item.Scene))
                {
                    dat[item.Scene] = new Dictionary<string, Dictionary<string, IncidentCount>> {
                        { item.Video, new Dictionary<string, IncidentCount> {
                            { item.Incident, new IncidentCount() }
                        } }
                    };
                }
                if (!dat[item.Scene].ContainsKey(item.Video))
                {
                    dat[item.Scene][item.Video] = new Dictionary<string, IncidentCount> { { item.Incident, new IncidentCount() } };
                }
                if (!dat[item.Scene][item.Video].ContainsKey(item.Incident))
                {
                    dat[item.Scene][item.Video][item.Incident] = new IncidentCount();
                }
                switch (item.State)
                {
                    case DetectType.TrueDet:
                        dat[item.Scene][item.Video][item.Incident].TrueDetect += item.Count;
                        int _tmp = dat[item.Scene][item.Video][item.Incident].TrueDetect;
                        int _tmp2 = dat[item.Scene][item.Video][item.Incident].ActualCount;
                        if (_tmp > _tmp2)
                        {
                            dat[item.Scene][item.Video][item.Incident].MultiDetect += _tmp - _tmp2;
                            dat[item.Scene][item.Video][item.Incident].TrueDetect = _tmp2;
                        }
                        break;

                    case DetectType.FalseDet:
                        dat[item.Scene][item.Video][item.Incident].FalseDetect += item.Count;
                        break;
                }
            }

            List<DetailDataItem> _list = new List<DetailDataItem>();
            foreach (KeyValuePair<string, Dictionary<string, Dictionary<string, IncidentCount>>> item in dat)
            {
                foreach (KeyValuePair<string, Dictionary<string, IncidentCount>> subIemt in item.Value)
                {
                    foreach (KeyValuePair<string, IncidentCount> subSubItem in subIemt.Value)
                    {
                        _list.Add(new DetailDataItem
                        {
                            Scene = item.Key,
                            Video = subIemt.Key,
                            Incident = subSubItem.Key,
                            ActualCount = subSubItem.Value.ActualCount,
                            TrueDetect = subSubItem.Value.TrueDetect,
                            FalseDetect = subSubItem.Value.FalseDetect,
                            MultiDetect = subSubItem.Value.MultiDetect
                        });
                    }
                }
            }
            return _list;
        }

        // 修改场景
        private void SceneChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SceneComBox.SelectedItem == null)
            {
                return;
            }

            string _scene = (SceneComBox.SelectedItem as ComboBoxItem).Content as string;
            if (_scene != null && _scene.Length > 0)
            {
                var _list = SceneDict.Where(f => f.Value == _scene).ToList();
                if (_list.Count > 0)
                {
                    alarmImageListAll[_index].Scene = _list[0].Key;
                    UpdateData(alarmImageListAll[_index]);
                }
            }
        }

        // 修改事件类型
        private void IncidentChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IncidentComBox.SelectedItem == null)
            {
                return;
            }
            string _incident = (IncidentComBox.SelectedItem as ComboBoxItem).Content as string;
            if (_incident != null && _incident.Length > 0)
            {
                var _list = IncidentDict.Where(f => f.Value == _incident).ToList();
                if (_list.Count > 0)
                {
                    alarmImageListAll[_index].Incident = _list[0].Key;
                    UpdateData(alarmImageListAll[_index]);
                }
            }
        }

        // 下一张图片
        private void NextImageClick(object sender, RoutedEventArgs e)
        {
            if (_index >= alarmImageList.Count - 1)
            {
                MessageWindow.ShowDialog("No More Image", this);
                return;
            }
            else
            {
                _index++;
            }
            _state = alarmImageList[_index].State;
            _detCount = alarmImageList[_index].Count;
            ShowImage(alarmImageList[_index]);
        }

        // 上一张图片
        private void PreImageClick(object sender, RoutedEventArgs e)
        {
            if (_index <= 0 || alarmImageList.Count == 0)
            {
                MessageWindow.ShowDialog("No More Image", this);
                return;
            }
            else
            {
                _index--;
            }
            _state = alarmImageList[_index].State;
            _detCount = alarmImageList[_index].Count;
            ShowImage(alarmImageList[_index]);
        }

        // 初始化数据
        private void ImportDataClick(object sender, RoutedEventArgs e)
        {
            ImportDataDialog win = new ImportDataDialog();
            win.Owner = this;
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            win.ShowDialog(); // 模态方式显示

            string _alarmImagePath = ImportDataDialog.alarmImagePath;
            string _videoPath = ImportDataDialog.videoPath;
            string _xmlFile = ImportDataDialog.xmlFile;
            if (_alarmImagePath == null || _videoPath == null || _xmlFile == null)
            {
                MessageWindow.ShowDialog("初始化数据失败:\n" + _alarmImagePath + "\n" + _videoPath + "\n" + _xmlFile, this);
                return;
            }
            dataWindow?.Init();  // 初始化数据窗口
            InitData(_alarmImagePath, _videoPath, _xmlFile);
        }

        // 导出数据
        private void ExportData(object sender, RoutedEventArgs e)
        {
            if (!File.Exists("./data.db"))
            {
                MessageWindow.ShowDialog("文件 data.db 不存在", this);
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = "另存为",
                FileName = "data.db",
                DefaultExt = ".db",
                Filter = "Data base file|*.db"
            };

            if (dlg.ShowDialog() == true)
            {
                FileInfo fi = new FileInfo("./data.db");
                fi.CopyTo(dlg.FileName);
            }
        }

        // 合并数据
        private void MergeData(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog()
            {
                Filter = "Data Base file (.db)|*.db|All files (*.*)|*.*"
            };
            string fileName = null;
            if (openFileDialog.ShowDialog() == true)
            {
                fileName = openFileDialog.FileName;
            }
            if (fileName == null || fileName.Length == 0)
            {
                return;
            }

            string _dbPath = "Data Source = " + fileName;
            SQLiteCommand cmd = new SQLiteCommand
            {
                Connection = new SQLiteConnection(_dbPath),
                CommandText = "SELECT * FROM AlarmImageTab WHERE state != 0"
            };

            List<AlarmDataItem> _alarmImageList = new List<AlarmDataItem>();
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                while (dataReader.Read())
                {
                    var _alarmDataItem = new AlarmDataItem
                    {
                        ID = dataReader.GetInt32(0),
                        ImagePath = dataReader.GetString(1),
                        Scene = dataReader.GetString(2),
                        Incident = dataReader.GetString(3),
                        Video = dataReader.GetString(4),
                        Frame = dataReader.GetInt32(5),
                        State = (DetectType)(dataReader.GetInt32(6)),
                        Count = dataReader.GetInt32(7)
                    };

                    _alarmImageList.Add(_alarmDataItem);
                }
                cmd.Connection.Close();
            }

            // 合并数据
            foreach (var item in _alarmImageList)
            {
                if (item.State != DetectType.UnKnown)
                {
                    if (dbConnection == null)
                    {
                        MessageWindow.ShowDialog("无法连接到数据库", this);
                        return;
                    }

                    var imgs = alarmImageListAll.Where(f => Path.GetFileName(f.ImagePath) == Path.GetFileName(item.ImagePath)).ToList();
                    cmd = new SQLiteCommand { Connection = dbConnection };
                    if (imgs.Count == 0)
                    {
                        alarmImageListAll.Add(item);
                        // 更新数据库
                        cmd.CommandText = string.Format($"INSERT INTO AlarmImageTab(id, image, scene, incident, video, frame, state, count)" +
                            $" VALUES ('{item.ID}', '{item.ImagePath}','{item.Scene}','{item.Incident}', '{item.Video}', '{item.Frame}', '{(int)item.State}', '{item.Count}')");
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        imgs[0].State = item.State;
                        imgs[0].Count = item.Count;
                        // 更新数据库
                        cmd.CommandText = string.Format($"UPDATE AlarmImageTab SET state = '{(int)item.State}', count = '{item.Count}' WHERE image = '{alarmImageListAll[_index].ImagePath}'");
                        cmd.ExecuteNonQuery();
                    }
                    dataWindow.UpdateAlarmImageItem(item);
                }
            }
        }

        // 关于
        private void AboutInfo(object sender, RoutedEventArgs e)
        {
            MessageWindow.ShowDialog(_aboutInfo, this);
        }

        private void MouseEnter_RadioBtn(object sender, MouseEventArgs e)
        {
            (sender as RadioButton).FontWeight = FontWeights.Bold;
        }

        private void MouseLeave_RadioBtn(object sender, MouseEventArgs e)
        {
            (sender as RadioButton).FontWeight = FontWeights.Normal;
        }

        private void TreeViewRightClick(object sender, MouseButtonEventArgs e)
        {
            var item = ImageTreeView.SelectedItem as FileRecord;
            if (item == null)
            {
                return;
            }
            ContextMenu cm = this.TryFindResource("TreeMenu") as ContextMenu;
            cm.Placement = PlacementMode.MousePoint;
            cm.IsOpen = true;
        }

        // 显示告警图片目录树
        void SetImageTreeView(string path)
        {
            if (Directory.Exists(path))
            {
                var tree = new ObservableCollection<FileRecord>
                {
                    new FileRecord { Info = new DirAndFileInfo { FullName = path } }
                };
                ImageTreeView.ItemsSource = tree;
            }
            else
            {
                MessageWindow.ShowDialog("告警图片文件夹 [" + path + "] 不存在", this);
            }
        }

        // 统计数据
        private void CountDataClick(object sender, RoutedEventArgs e)
        {
            var treeViewItem = ImageTreeView.SelectedItem as FileRecord;
            var dir = treeViewItem.Info.FullName;

            alarmImageList.Clear();
            foreach (var item in alarmImageListAll)
            {
                if (item.ImagePath.Contains(dir))
                {
                    alarmImageList.Add(item);
                }
            }

            if (alarmImageList.Count > 0)
            {
                _index = 0;
                _state = alarmImageList[0].State;
                _detCount = alarmImageList[0].Count;
                ShowImage(alarmImageList[0]);
            }
            else
            {
                _index = -1;
                _state = 0;
                _detCount = 0;
                MessageWindow.ShowDialog("目录 [" + dir + "] 下无告警图片");
                return;
            }

            // 更新数据库
            SQLiteCommand cmd = new SQLiteCommand();
            cmd.Connection = dbConnection; // 连接数据库
            cmd.CommandText = string.Format($"INSERT OR REPLACE INTO DataPathTab(Item, Path) VALUES ('LastImagePath', '{dir}')");
            cmd.ExecuteNonQuery();
        }

        // 关闭窗口
        private void OnWindowClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dataWindow.terminate(); // 关闭数据展示窗口
            dbConnection?.Close();  // 关闭数据库连接
        }
    }
}