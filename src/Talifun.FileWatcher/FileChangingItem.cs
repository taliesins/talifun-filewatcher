using System;

namespace Talifun.FileWatcher
{
    internal class FileChangingItem : IFileChangingItem
    {
        public FileChangingItem(string filePath, FileEventType fileEventType)
        {
            FilePath = filePath;
            FileEventType = fileEventType;
        }

        public string FilePath { get; private set; }
        public FileEventType FileEventType { get; private set; }
        public DateTime FireTime { get; set; }
    }
}
