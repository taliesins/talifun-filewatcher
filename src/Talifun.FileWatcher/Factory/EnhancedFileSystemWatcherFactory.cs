namespace Talifun.FileWatcher
{
    public sealed class EnhancedFileSystemWatcherFactory : IEnhancedFileSystemWatcherFactory
    {
        private const int DefaultPollTime = 2;
        private const string DefaultIncludeFilter = "";
        private const string DefaulExcludeFilter = "";
        private const bool DefaultIncludeSubDirectories = true;

        private EnhancedFileSystemWatcherFactory()
        {
        }

        public static readonly IEnhancedFileSystemWatcherFactory Instance = new EnhancedFileSystemWatcherFactory();

        public IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch)
        {
            return CreateEnhancedFileSystemWatcher(folderToWatch, DefaultIncludeFilter);
        }

        public IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter)
        {
            return CreateEnhancedFileSystemWatcher(folderToWatch, includeFilter, DefaultPollTime, DefaultIncludeSubDirectories);
        }

        public IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, int pollTime, bool includeSubdirectories)
        {
            return CreateEnhancedFileSystemWatcher(folderToWatch, includeFilter, DefaulExcludeFilter, pollTime, includeSubdirectories);
        }

        public IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, int pollTime, bool includeSubdirectories, object userState)
        {
            return CreateEnhancedFileSystemWatcher(folderToWatch, includeFilter, DefaulExcludeFilter, pollTime, includeSubdirectories, userState);
        }

        public IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, string excludeFiler)
        {
            return CreateEnhancedFileSystemWatcher(folderToWatch, includeFilter, excludeFiler, DefaultPollTime, DefaultIncludeSubDirectories);
        }

        public IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, string excludeFiler, int pollTime, bool includeSubdirectories)
        {
            return new EnhancedFileSystemWatcher(folderToWatch, includeFilter, excludeFiler, pollTime, includeSubdirectories);
        }

        public IEnhancedFileSystemWatcher CreateEnhancedFileSystemWatcher(string folderToWatch, string includeFilter, string excludeFiler, int pollTime, bool includeSubdirectories, object userState)
        {
            IEnhancedFileSystemWatcher folderMonitor = new EnhancedFileSystemWatcher(folderToWatch, includeFilter, excludeFiler, pollTime, includeSubdirectories)
            {
                UserState = userState
            };

            return folderMonitor;
        }
    }
}