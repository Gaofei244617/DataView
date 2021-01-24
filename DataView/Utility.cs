using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataView
{
    class Utility
    {
        // 递归获取文件夹下所有文件
        public static List<string> Director(string dirs)
        {
            List<string> list = new List<string>();
            // 绑定到指定的文件夹目录
            DirectoryInfo dir = new DirectoryInfo(dirs);
            // 检索表示当前目录的文件和子目录
            FileSystemInfo[] fsinfos = dir.GetFileSystemInfos();
            // 遍历检索的文件和子目录
            foreach (FileSystemInfo fsinfo in fsinfos)
            {
                // 判断是否为空文件夹　　
                if (fsinfo is DirectoryInfo)
                {
                    // 递归调用
                    list.AddRange(Director(fsinfo.FullName));
                }
                else
                {
                    // 将得到的文件全路径放入到集合中
                    list.Add(fsinfo.FullName);
                }
            }
            return list;
        }
    }
}
