namespace Talifun.FileWatcher
{
    public enum FileWatcherEventType
    {
        Created = 1,
        Deleted = 2,
        Changed = 4,
        Renamed = 8,
        Exists = 16
    }
}
