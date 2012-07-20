namespace Talifun.FileWatcher
{
    public class FileEventItem : IFileEventItem
    {
        public FileEventItem(string filePath, FileEventType fileEventType)
        {
            FilePath = filePath;
            FileEventType = fileEventType;
        }

        public string FilePath { get; private set; }
        public FileEventType FileEventType { get; set; }
    }
}
