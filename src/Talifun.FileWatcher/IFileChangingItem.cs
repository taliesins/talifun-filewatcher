using System;

namespace Talifun.FileWatcher
{
    internal interface IFileChangingItem
    {
        DateTime FireTime
        {
            get;
            set;
        }

        string FilePath { get; }

        FileEventType FileEventType
        {
            get;
        }
    }
}
