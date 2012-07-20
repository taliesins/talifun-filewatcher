using System;

namespace Talifun.FileWatcher
{
    public interface IFileChangingItem : IFileEventItem
    {
        DateTime FireTime { get; set; }
    }
}
