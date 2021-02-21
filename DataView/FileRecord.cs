using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace DataView
{
    internal class DirAndFileInfo
    {
        private static Dictionary<string, BitmapImage> icons = new Dictionary<string, BitmapImage>
        {
            { "folder", new BitmapImage(new Uri("theme/folder2.png", UriKind.RelativeOrAbsolute))},
            { ".jpg", new BitmapImage(new Uri("theme/image2.png", UriKind.RelativeOrAbsolute))},
            { ".json", new BitmapImage(new Uri("theme/json.png", UriKind.RelativeOrAbsolute))},
            { ".html", new BitmapImage(new Uri("theme/html.png", UriKind.RelativeOrAbsolute))},
            { "default", new BitmapImage(new Uri("theme/file.png", UriKind.RelativeOrAbsolute))}
        };

        private string _fullName;
        public BitmapImage Icon { get; set; }
        public string Name { get; set; }

        public string FullName
        {
            get { return _fullName; }
            set
            {
                _fullName = value;
                if (File.Exists(_fullName))
                {
                    Name = (new FileInfo(_fullName)).Name;
                    var ex = Path.GetExtension(_fullName);
                    Icon = icons.ContainsKey(ex) ? icons[ex] : icons["default"];
                }
                else
                {
                    Name = (new DirectoryInfo(_fullName)).Name;
                    Icon = icons["folder"];
                }
            }
        }

        public DirAndFileInfo[] GetDirsAndFiles()
        {
            if (FullName == null || FullName.Length == 0 || File.Exists(FullName))
            {
                return null;
            }
            List<DirAndFileInfo> _list = new List<DirAndFileInfo>();
            var info = new DirectoryInfo(FullName);
            foreach (var item in info.GetDirectories())
            {
                _list.Add(new DirAndFileInfo { FullName = item.FullName });
            }
            foreach (var item in info.GetFiles())
            {
                _list.Add(new DirAndFileInfo { FullName = item.FullName });
            }
            if (_list.Count == 0)
            {
                return null;
            }
            return _list.ToArray();
        }
    }

    internal class FileRecord
    {
        public DirAndFileInfo Info { get; set; }

        public IEnumerable<FileRecord> Directories
        {
            get
            {
                var infos = Info.GetDirsAndFiles();
                if (infos == null)
                {
                    return null;
                }
                return from di in infos select new FileRecord { Info = di };
            }
        }
    }
}