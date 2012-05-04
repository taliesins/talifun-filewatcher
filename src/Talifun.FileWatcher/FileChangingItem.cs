using System;
using System.IO;

namespace Talifun.FileWatcher
{
    internal class FileChangingItem : IFileChangingItem
    {
        public FileChangingItem(FileSystemEventArgs fileSystemEventArgs)
        {
            FileSystemEventArgs = fileSystemEventArgs;
        }

        public FileSystemEventArgs FileSystemEventArgs { get; private set; }
        public DateTime FireTime { get; set; }
    }
}
