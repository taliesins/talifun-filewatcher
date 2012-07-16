namespace Talifun.FileWatcher
{
    public enum FileEventType
    {
        Created = 1,
        Deleted = 2,
        Changed = 4,
        Renamed = 8,
        InDirectory = 16
    }
}
