using System;

namespace Talifun.FileWatcher
{
    public class FileChangingItem : FileEventItem, IFileChangingItem
    {
        public FileChangingItem(string filePath, FileEventType fileEventType) : base(filePath, fileEventType)
        {
        }

        public DateTime FireTime { get; set; }
    }
}
