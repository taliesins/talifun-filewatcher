namespace Talifun.FileWatcher
{
    public interface IFileEventItem
    {
        string FilePath { get; }
        FileEventType FileEventType { get; set; }
    }
}
