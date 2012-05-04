using System;
using System.IO;

namespace Talifun.FileWatcher
{
    internal interface IFileChangingItem
    {
        DateTime FireTime
        {
            get;
            set;
        }

        FileSystemEventArgs FileSystemEventArgs
        {
            get;
        }
    }
}
