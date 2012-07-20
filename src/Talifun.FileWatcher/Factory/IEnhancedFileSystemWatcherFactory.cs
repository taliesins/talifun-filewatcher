namespace Talifun.FileWatcher
{
    public interface IEnhancedFileSystemWatcherFactory
    {
        IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch);
        IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter);
        IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, int pollTime, bool includeSubdirectories);
        IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, int pollTime, bool includeSubdirectories, object userState);
        IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, string excludeFiler);
        IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, string excludeFiler, int pollTime, bool includeSubdirectories);
        IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, string excludeFiler, int pollTime, bool includeSubdirectories, object userState);
    }
}
