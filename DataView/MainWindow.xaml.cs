using Alphaleonis.Win32.Filesystem;
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
using System.Windows.Data;
using System.Windows.Input;
using System.Xml;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace DataView
{
    public partial class MainWindow : Window
    {
        public static List<SceneItem> Scenes = new List<SceneItem>();               // 场景集合
        public static List<IncidentItem> Incidents = new List<IncidentItem>();      // 事件集合

        private readonly string _aboutInfo = "交通事件指标统计系统" + "\n" + "版 本: V1.1"; // 版本信息
        private DataWindow dataWindow = new DataWindow();                           // 数据窗口
        private SQLiteConnection dbConnection = null;                               // 数据库连接
        
        private readonly ObservableCollection<FileRecord> fileTree = new ObservableCollection<FileRecord>(); // 告警图片目录树
        private List<AlarmImage> alarmImageListAll = new List<AlarmImage>();        // 所有告警图片
        private readonly List<AlarmImage> alarmImageList = new List<AlarmImage>();  // 正在统计的告警图片
        private List<VideoInfo> videoInfoList = new List<VideoInfo>();              // 视频应报事件
        private List<TestVideo> testVideoList = new List<TestVideo>();              // 测试视频(视频名，所在路径)

        /************************************************************************************/

        // 默认构造
        public MainWindow()
        {
            InitializeComponent();

            // 数据绑定
            DataContext = alarmImageList;
            SceneComBox.ItemsSource = Scenes;
            IncidentComBox.ItemsSource = Incidents;
            ImageTreeView.ItemsSource = fileTree;

            // 元数据
            if (File.Exists("./MetaData.xml"))
            {
                XmlDocument doc = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings { IgnoreComments = true }; // 忽略文档里面的注释
                XmlReader reader = XmlReader.Create("./MetaData.xml", settings);
                doc.Load(reader);
                XmlNode xn = doc.SelectSingleNode("root"); // 根节点
                XmlNodeList xnl = xn.ChildNodes;  // 根节点的子节点
                Scenes.Clear();
                Incidents.Clear();
                foreach (XmlNode node in xnl)
                {
                    if (node.Name == "scene") // 场景下拉列表
                    {
                        XmlNodeList subNodes = node.ChildNodes;
                        foreach (XmlNode item in subNodes)
                        {
                            if (Scenes.Count(f => f.Name == item.Attributes["tag"].Value) == 0)
                            {
                                Scenes.Add(new SceneItem { Display = item.InnerText, Name = item.Attributes["tag"].Value });
                            }
                        }
                    }

                    if (node.Name == "incident") // 事件下拉列表
                    {
                        XmlNodeList subNodes = node.ChildNodes;
                        foreach (XmlNode item in subNodes)
                        {
                            if (Incidents.Count(f => f.Name == item.Attributes["tag"].Value) == 0)
                            {
                                Incidents.Add(new IncidentItem { Display = item.InnerText, Name = item.Attributes["tag"].Value });
                            }
                        }
                    }
                }
                reader.Close();
            }
            else
            {
                MessageWindow.ShowDialog("Can not read MetaData.xml", this);
            }

            // 删除临时数据
            if (Directory.Exists("./temp"))
            {
                Directory.Delete("./temp", true);
            }

            // 恢复上次程序退出前状态
            Resume();
        }

        // 成员变量初始化
        public void InitData()
        {
            alarmImageListAll.Clear();
            alarmImageList.Clear();
            videoInfoList.Clear();
            testVideoList.Clear();
            CollectionViewSource.GetDefaultView(DataContext)?.MoveCurrentToFirst();
        }

        // 解析视频标注文件
        private List<VideoInfo> ParseXml(string xmlFile)
        {
            if (File.Exists(xmlFile))
            {
                List<VideoInfo> videos = new List<VideoInfo>();
                XmlDocument doc = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreComments = true;  // 忽略文档里面的注释
                XmlReader reader = XmlReader.Create(xmlFile, settings);
                doc.Load(reader);
                XmlNode xn = doc.SelectSingleNode("root"); // 根节点
                XmlNodeList xnl = xn.ChildNodes;  // 根节点的子节点
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
                            var _videoInfoItem = new VideoInfo
                            {
                                Scene = _scene,
                                VideoName = _video,
                                Incident = _incident,
                                Count = _count
                            };
                            videos.Add(_videoInfoItem);
                        }
                    }
                    else // 无事件发生
                    {
                        var _videoInfoItem = new VideoInfo
                        {
                            Scene = _scene,
                            VideoName = _video,
                            Incident = "null",
                            Count = 0
                        };
                        videos.Add(_videoInfoItem);
                    }
                }
                reader.Close();
                return videos;
            }
            else
            {
                MessageWindow.ShowDialog($"无法访问文件 [{xmlFile}]");
            }

            return null;
        }

        // 解析告警图片文件夹
        private List<AlarmImage> ParseAlarmImage(string alarmImagePath)
        {
            if (Directory.Exists(alarmImagePath))
            {
                List<AlarmImage> images = new List<AlarmImage>();
                // 递归获取所有jpg文件
                string[] alarmImages = Utility.Director(alarmImagePath).Where(f =>
                {
                    string ex = Path.GetExtension(f);
                    return (ex == ".jpg" || ex == ".png" || ex == ".bmp");
                }).ToArray();

                // 图片命名方式: 视频名___事件_帧号.jpg
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

                    // 场景下拉列表
                    if (Scenes.Count(item => item.Name == _scene) == 0)
                    {
                        Scenes.Add(new SceneItem { Display = _scene, Name = _scene });
                    }

                    // 事件类型下拉列表
                    if (Incidents.Count(item => item.Name == _incident) == 0)
                    {
                        Incidents.Add(new IncidentItem { Display = _incident, Name = _incident });
                    }

                    var _alarmDataItem = new AlarmImage
                    {
                        ID = _id++,
                        ImagePath = img,
                        Video = _video,
                        Scene = _scene,
                        Incident = _incident,
                        Frame = Convert.ToInt32(infos[1]),
                        State = DetectType.UnKnown,
                        IncidentCount = 0
                    };
                    images.Add(_alarmDataItem);
                }

                return images;
            }
            else
            {
                MessageWindow.ShowDialog($"无法访问告警图片文件夹 [{alarmImagePath}]", this);
            }
            return null;
        }

        // 解析测试视频
        private List<TestVideo> ParseTestVideo(string videoPath)
        {
            if (Directory.Exists(videoPath))
            {
                List<TestVideo> testVideos = new List<TestVideo>();
                var videos = Utility.Director(videoPath).Where(f =>
                {
                    string ex = Path.GetExtension(f);
                    return (ex == ".ts" || ex == ".mp4" || ex == ".flv" || ex == ".avi");
                }).ToList();

                foreach (var item in videos)
                {
                    var _testVideoItem = new TestVideo { VideoName = Path.GetFileName(item), VideoPath = Path.GetDirectoryName(item) };
                    testVideos.Add(_testVideoItem);
                }

                return testVideos;
            }
            else
            {
                MessageWindow.ShowDialog($"无法访问文件夹 [{videoPath}]", this);
            }

            return null;
        }

        private void InitMemoryData(string alarmImagePath, string videoPath, string xmlFile)
        {
            try
            {
                InitData();  // 数据初始化
                videoInfoList = ParseXml(xmlFile);  // 标注文件
                fileTree.Clear();
                fileTree.Add(new FileRecord { Info = new DirAndFileInfo { FullName = alarmImagePath } });

                // 告警图片
                alarmImageListAll = ParseAlarmImage(alarmImagePath);
                alarmImageListAll?.ForEach(it => dataWindow.AddAlarmImageItem(it));

                // 测试视频
                testVideoList = ParseTestVideo(videoPath);
                testVideoList?.ForEach(it => dataWindow.AddTestVideoItem(it));

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
                    fi.MoveTo("./data.db." + ex, MoveOptions.ReplaceExisting);
                }
                catch (IOException e)
                {
                    MessageWindow.ShowDialog(e.ToString(), this);
                    return;
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
                    $" VALUES ('{item.ID}', '{item.ImagePath}','{item.Scene}','{item.Incident}', '{item.Video}', '{item.Frame}', '{(int)item.State}', '{item.IncidentCount}')");
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

        // 初始化统计数据
        public void InitData(string alarmImagePath, string videoPath, string xmlFile)
        {
            InitMemoryData(alarmImagePath, videoPath, xmlFile);
            InitDataBase(alarmImagePath, videoPath, xmlFile);
        }

        // 初始化数据Click
        private void InitDataClick(object sender, RoutedEventArgs e)
        {
            ImportDataDialog win = new ImportDataDialog
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            win.ShowDialog(); // 模态方式显示

            string _alarmImagePath = ImportDataDialog.alarmImagePath;
            string _videoPath = ImportDataDialog.videoPath;
            string _xmlFile = ImportDataDialog.xmlFile;
            if (_alarmImagePath == null || _videoPath == null || _xmlFile == null)
            {
                MessageWindow.ShowDialog("初始化数据失败", this);
                return;
            }
            dataWindow?.Init();  // 初始化数据窗口
            InitData(_alarmImagePath, _videoPath, _xmlFile);
            CountData(_alarmImagePath);
            MessageWindow.ShowDialog("数据初始化完成", this);
        }

        private void CountData(string dir)
        {
            alarmImageList.Clear();
            foreach (var item in alarmImageListAll)
            {
                if (item.ImagePath.Contains(dir))
                {
                    alarmImageList.Add(item);
                }
            }

            if (alarmImageList.Count == 0)
            {
                MessageWindow.ShowDialog($"目录 [{dir}] 下无告警图片");
                return;
            }

            var view = CollectionViewSource.GetDefaultView(DataContext);
            view?.MoveCurrentToFirst();

            this.Title = $"[{1}/{alarmImageList.Count}] {Path.GetFileName(CurrentAlarmImage()?.ImagePath)}";
            SetSelectedSceneItem(CurrentAlarmImage()?.Scene);
            SetSelectedIncidentItem(CurrentAlarmImage()?.Incident);

            // 更新数据库
            SQLiteCommand cmd = new SQLiteCommand
            {
                Connection = dbConnection, // 连接数据库
                CommandText = string.Format($"INSERT OR REPLACE INTO DataPathTab(Item, Path) VALUES ('LastImagePath', '{dir}')")
            };
            cmd.ExecuteNonQuery();
        }

        // 统计指标(TreeView ContextMenu)
        private void CountDataClick(object sender, RoutedEventArgs e)
        {
            var treeViewItem = ImageTreeView.SelectedItem as FileRecord;
            string dir = treeViewItem.Info.FullName;
            CountData(dir);
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
            cmd.CommandText = "SELECT * FROM VideoInfoTab";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                while (dataReader.Read())
                {
                    // VideoDataTab(Scene, Video, Incident, Count)
                    string _scene = dataReader.GetString(0);
                    string _video = dataReader.GetString(1);
                    string _incident = dataReader.GetString(2);
                    int _count = dataReader.GetInt32(3);

                    var _videoInfoItem = new VideoInfo
                    {
                        Scene = _scene,
                        VideoName = _video,
                        Incident = _incident,
                        Count = _count
                    };
                    videoInfoList.Add(_videoInfoItem);
                }
            }

            // 告警图片
            cmd.CommandText = "SELECT * FROM AlarmImageTab";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                while (dataReader.Read())
                {
                    // (id INTEGER, image VARCHAR(512) PRIMARY KEY, scene VARCHAR(64), incident VARCHAR(64), video VARCHAR(256), frame INTEGER, state INTEGER)
                    int _id = dataReader.GetInt32(0);
                    string _imgPath = dataReader.GetString(1);
                    string _scene = dataReader.GetString(2);
                    string _incident = dataReader.GetString(3);
                    string _videoName = dataReader.GetString(4);
                    int _frame = dataReader.GetInt32(5);
                    int _state = dataReader.GetInt32(6);
                    int _count = dataReader.GetInt32(7);

                    var _alarmDataItem = new AlarmImage
                    {
                        ID = _id,
                        ImagePath = _imgPath,
                        Video = _videoName,
                        Scene = _scene,
                        Incident = _incident,
                        Frame = _frame,
                        State = (DetectType)_state,
                        IncidentCount = _count
                    };

                    alarmImageListAll.Add(_alarmDataItem);
                    dataWindow.AddAlarmImageItem(_alarmDataItem);
                }
            }

            // 测试视频
            cmd.CommandText = "SELECT * FROM TestVideoTab";
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                while (dataReader.Read())
                {
                    // (VideoName VARCHAR(256) PRIMARY KEY, Directory VARCHAR(256))
                    string _videoName = dataReader.GetString(0);
                    string _videoPath = dataReader.GetString(1);
                    var _testVideoItem = new TestVideo { VideoName = _videoName, VideoPath = _videoPath };
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
                        fileTree.Clear();
                        fileTree.Add(new FileRecord { Info = new DirAndFileInfo { FullName = _imagePath } });
                    }
                    else
                    {
                        MessageWindow.ShowDialog($"无法访问告警图片路径 [{_imagePath}]");
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
                        MessageWindow.ShowDialog($"无法访问告警图片路径 [{_dir}]");
                    }
                }
            }

            this.Title = $"[{1}/{alarmImageList.Count}] {Path.GetFileName(CurrentAlarmImage()?.ImagePath)}";
            SetSelectedSceneItem(CurrentAlarmImage()?.Scene);
            SetSelectedIncidentItem(CurrentAlarmImage()?.Incident);

            // 更细统计数据
            dataWindow.SetDetailData(GetDetailData());
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
            System.Diagnostics.Process.Start(Path.GetDirectoryName(CurrentAlarmImage().ImagePath));
        }

        private AlarmImage CurrentAlarmImage()
        {
            if (DataContext != null)
            {
                return CollectionViewSource.GetDefaultView(DataContext).CurrentItem as AlarmImage;
            }
            return null;
        }

        // 打开视频位置
        private void OpenVideoDirClick(object sender, RoutedEventArgs e)
        {
            bool flag = false;
            foreach (var item in testVideoList)
            {
                if (item.VideoName == CurrentAlarmImage().Video)
                {
                    System.Diagnostics.Process.Start(item.VideoPath);
                    flag = true;
                    break;
                }
            }

            if (!flag)
            {
                MessageWindow.ShowDialog($"无法打开视频 [{CurrentAlarmImage().Video}] 所在位置！", this);
            }
        }

        // 打开视频文件
        private void OpenVideoClick(object sender, RoutedEventArgs e)
        {
            bool flag = false;
            foreach (var item in testVideoList)
            {
                if (item.VideoName == CurrentAlarmImage().Video)
                {
                    System.Diagnostics.Process.Start(Path.Combine(item.VideoPath, item.VideoName));
                    flag = true;
                    break;
                }
            }

            if (!flag)
            {
                MessageWindow.ShowDialog($"未找到视频 [{CurrentAlarmImage().Video}]", this);
            }
        }

        // 更新数据
        private void UpdateData(AlarmImage item)
        {
            // 更新数据库
            if (dbConnection != null)
            {
                SQLiteCommand cmd = new SQLiteCommand
                {
                    Connection = dbConnection,
                    CommandText = string.Format($"UPDATE AlarmImageTab SET scene = '{item.Scene}', incident = '{item.Incident}', state = '{(int)item.State}', count = '{item.IncidentCount}' " +
                    $"WHERE image = '{CurrentAlarmImage().ImagePath}'")
                };
                cmd.ExecuteNonQuery();
            }

            // 更新UI数据
            dataWindow.UpdateAlarmImageItem(CurrentAlarmImage());

            /*********************** 统计结果(详细数据) ********************/
            dataWindow.SetDetailData(GetDetailData());
        }

        // 识别正检/误检
        private void SetImageStateClick(object sender, RoutedEventArgs e)
        {
            UpdateData(CurrentAlarmImage());
        }

        private void NumBoxChanged(object sender, RoutedEventArgs e)
        {
            var _alarmImage = CurrentAlarmImage();
            if (_alarmImage != null)
            {
                UpdateData(CurrentAlarmImage());
            }
        }

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
                        dat[item.Scene][item.Video][item.Incident].TrueDetect += item.IncidentCount;
                        int _tmp = dat[item.Scene][item.Video][item.Incident].TrueDetect;
                        int _tmp2 = dat[item.Scene][item.Video][item.Incident].ActualCount;
                        if (_tmp > _tmp2)
                        {
                            dat[item.Scene][item.Video][item.Incident].MultiDetect += _tmp - _tmp2;
                            dat[item.Scene][item.Video][item.Incident].TrueDetect = _tmp2;
                        }
                        break;

                    case DetectType.FalseDet:
                        dat[item.Scene][item.Video][item.Incident].FalseDetect += item.IncidentCount;
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
            string _scene = (sender as ComboBox).SelectedValue as string;
            if (!_scene.IsNullOrEmpty())
            {
                var _alarmImage = CollectionViewSource.GetDefaultView(DataContext).CurrentItem as AlarmImage;
                _alarmImage.Scene = _scene;
                UpdateData(_alarmImage);
            }
        }

        // 修改事件类型
        private void IncidentChanged(object sender, SelectionChangedEventArgs e)
        {
            string _incident = (sender as ComboBox).SelectedValue as string;
            if (!_incident.IsNullOrEmpty())
            {
                var _alarmImage = CollectionViewSource.GetDefaultView(DataContext).CurrentItem as AlarmImage;
                _alarmImage.Incident = _incident;
                UpdateData(_alarmImage);
            }
        }

        public void SetSelectedSceneItem(string scene)
        {
            if (scene.IsNullOrEmpty())
            {
                return;
            }

            foreach (SceneItem item in this.SceneComBox.Items)
            {
                if (scene == item.Name)
                {
                    this.SceneComBox.SelectedItem = item;
                    break;
                }
            }
        }

        public void SetSelectedIncidentItem(string incident)
        {
            if (incident.IsNullOrEmpty())
            {
                return;
            }

            foreach (IncidentItem item in this.IncidentComBox.Items)
            {
                if (incident == item.Name)
                {
                    this.IncidentComBox.SelectedItem = item;
                    break;
                }
            }
        }

        // 告警图片校验
        private string Validation(AlarmImage alarmImage)
        {
            if (alarmImage.State == DetectType.UnKnown &&
                videoInfoList.Count(f => f.VideoName == alarmImage.Video) == 0)
            {
                return "该图片不在应统计视频范围内";
            }

            return null;
        }

        // 下一张图片
        private void NextImageClick(object sender, RoutedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(DataContext);
            if (view.CurrentPosition < alarmImageList.Count - 1)
            {
                view.MoveCurrentToNext();
                this.Title = $"[{view.CurrentPosition + 1}/{alarmImageList.Count}] {Path.GetFileName(CurrentAlarmImage()?.ImagePath)}";
            }
            else
            {
                MessageWindow.ShowDialog("No More Image", this);
            }

            SetSelectedSceneItem(CurrentAlarmImage().Scene);
            SetSelectedIncidentItem(CurrentAlarmImage().Incident);

            var msg = Validation(CurrentAlarmImage());
            if (msg != null)
            {
                MessageWindow.ShowDialog(msg, this);
            }
        }

        // 上一张图片
        private void PreImageClick(object sender, RoutedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(DataContext);
            if (view.CurrentPosition > 0)
            {
                view.MoveCurrentToPrevious();
                this.Title = $"[{view.CurrentPosition + 1}/{alarmImageList.Count}] {Path.GetFileName(CurrentAlarmImage()?.ImagePath)}";
            }
            else
            {
                MessageWindow.ShowDialog("No More Image", this);
            }
            SetSelectedSceneItem(CurrentAlarmImage().Scene);
            SetSelectedIncidentItem(CurrentAlarmImage().Incident);
            var msg = Validation(CurrentAlarmImage());
            if (msg != null)
            {
                MessageWindow.ShowDialog(msg, this);
            }
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
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            string _dbPath = $"Data Source = {fileName}";
            SQLiteCommand cmd = new SQLiteCommand
            {
                Connection = new SQLiteConnection(_dbPath),
                CommandText = "SELECT * FROM AlarmImageTab WHERE state != 0"
            };
            cmd.Connection.Open();

            List<AlarmImage> _alarmImageList = new List<AlarmImage>();
            using (SQLiteDataReader dataReader = cmd.ExecuteReader())
            {
                while (dataReader.Read())
                {
                    var _alarmDataItem = new AlarmImage
                    {
                        ID = dataReader.GetInt32(0),
                        ImagePath = dataReader.GetString(1),
                        Scene = dataReader.GetString(2),
                        Incident = dataReader.GetString(3),
                        Video = dataReader.GetString(4),
                        Frame = dataReader.GetInt32(5),
                        State = (DetectType)(dataReader.GetInt32(6)),
                        IncidentCount = dataReader.GetInt32(7)
                    };

                    _alarmImageList.Add(_alarmDataItem);
                }
                cmd.Connection.Close();
            }

            // 合并数据
            if (dbConnection == null)
            {
                MessageWindow.ShowDialog("无法连接到数据库", this);
                return;
            }
            cmd = new SQLiteCommand { Connection = dbConnection };
            cmd.CommandText = "BEGIN";
            cmd.ExecuteNonQuery();
            foreach (var item in _alarmImageList)
            {
                if (item.State != DetectType.UnKnown)
                {
                    var imgs = alarmImageListAll.Where(f => Path.GetFileName(f.ImagePath) == Path.GetFileName(item.ImagePath)).ToList();
                    if (imgs.Count == 0)  // 新的告警图片
                    {
                        alarmImageListAll.Add(item);
                        // 更新数据库
                        cmd.CommandText = string.Format($"INSERT INTO AlarmImageTab(id, image, scene, incident, video, frame, state, count)" +
                            $" VALUES ('{item.ID}', '{item.ImagePath}','{item.Scene}','{item.Incident}', '{item.Video}', '{item.Frame}', '{(int)item.State}', '{item.IncidentCount}')");
                        cmd.ExecuteNonQuery();
                    }
                    else  // 已存在的告警图片
                    {
                        imgs[0].State = item.State;
                        imgs[0].IncidentCount = item.IncidentCount;
                        // 更新数据库
                        cmd.CommandText = string.Format($"UPDATE AlarmImageTab SET state = '{(int)item.State}', count = '{item.IncidentCount}' WHERE image = '{ imgs[0].ImagePath}'");
                        cmd.ExecuteNonQuery();
                    }
                    dataWindow.UpdateAlarmImageItem(item);
                }
            }
            cmd.CommandText = "COMMIT";
            cmd.ExecuteNonQuery();

            // 更新数据窗口
            dataWindow.SetDetailData(GetDetailData());

            MessageWindow.ShowDialog("完成数据合并", this);
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
            if (!(ImageTreeView.SelectedItem is FileRecord))
            {
                return;
            }
            ContextMenu cm = this.TryFindResource("TreeMenu") as ContextMenu;
            cm.Placement = PlacementMode.MousePoint;
            cm.IsOpen = true;
        }

        // 关闭窗口
        private void OnWindowClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dataWindow.Terminate(); // 关闭数据展示窗口
            dbConnection?.Close();  // 关闭数据库连接
        }
    }
}