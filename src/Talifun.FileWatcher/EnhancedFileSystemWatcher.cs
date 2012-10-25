using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

namespace Talifun.FileWatcher
{
    public class EnhancedFileSystemWatcher : IEnhancedFileSystemWatcher
    {
        private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
        private readonly AsyncOperation _asyncOperation = AsyncOperationManager.CreateOperation(null);

        private readonly object _filesRaisingEventsLock = new object();
        private Dictionary<string, IFileChangingItem> _filesChanging;
        private Dictionary<string, FileFinishedChangingEventArgs> _filesFinishedChanging;
        private List<IFileEventItem> _fileEventItems;

        private readonly System.Timers.Timer _timer;
        private readonly System.Timers.Timer _timerForFileActivityFinished;
        private string _nextFileToCheck = string.Empty;

        private readonly FileSystemEventHandler _fileSystemWatcherChangedEvent;
        private readonly FileSystemEventHandler _fileSystemWatcherCreatedEvent;
        private readonly FileSystemEventHandler _fileSystemWatcherDeletedEvent;
        private readonly RenamedEventHandler _fileSystemWatcherRenamedEvent;
        private readonly FileFinishedChangingCallback _fileFinishedChangingCallback;
        private readonly FileSystemWatcher _fileSystemWatcher;

    	public EnhancedFileSystemWatcher(string folderToWatch, string includeFilter, string excludeFilter, int pollTime, bool includeSubdirectories)
        {
            FolderToWatch = folderToWatch;
            IncludeFilter = includeFilter;
            ExcludeFilter = excludeFilter;
            PollTime = pollTime;
            IncludeSubdirectories = includeSubdirectories;

            _timer = new System.Timers.Timer();
            _timer.Elapsed += OnTimeUp;
            _timer.Interval = PollTime;
            _timer.Enabled = false;
            _timer.AutoReset = false;

            _timerForFileActivityFinished = new System.Timers.Timer();
            _timerForFileActivityFinished.Elapsed += OnTimeUpForFileActivityFinished;
            _timerForFileActivityFinished.Interval = PollTime;
            _timerForFileActivityFinished.Enabled = false;
            _timerForFileActivityFinished.AutoReset = false;

            _fileSystemWatcherChangedEvent = OnFileChanged;
            _fileSystemWatcherCreatedEvent = OnFileCreated;
            _fileSystemWatcherDeletedEvent = OnFileDeleted;
            _fileSystemWatcherRenamedEvent = OnFileRenamed;
            _fileFinishedChangingCallback = OnFileFinishedChanging;

    		_fileSystemWatcher = new FileSystemWatcher(FolderToWatch)
    		{
    		    IncludeSubdirectories = IncludeSubdirectories,
    		    EnableRaisingEvents = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite 
    		};
    		_fileSystemWatcher.Changed += _fileSystemWatcherChangedEvent;
            _fileSystemWatcher.Created += _fileSystemWatcherCreatedEvent;
            _fileSystemWatcher.Deleted += _fileSystemWatcherDeletedEvent;
            _fileSystemWatcher.Renamed += _fileSystemWatcherRenamedEvent;
        }

        public void Start()
        {
            if (_fileSystemWatcher.EnableRaisingEvents) return;
            lock (_filesRaisingEventsLock)
            {
                _filesChanging = new Dictionary<string, IFileChangingItem>();
                _filesFinishedChanging = new Dictionary<string, FileFinishedChangingEventArgs>();
                _fileEventItems = new List<IFileEventItem>();
                _fileSystemWatcher.EnableRaisingEvents = true;

                RaiseEventsForExistingFiles();
            }
        }

        public void Stop()
        {
            if (!_fileSystemWatcher.EnableRaisingEvents) return;

            lock (_filesRaisingEventsLock)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                if (_timer != null)
                {
                    _timer.Stop();
                }

                if (_timerForFileActivityFinished != null)
                {
                    _timerForFileActivityFinished.Stop();
                }

                _nextFileToCheck = string.Empty;
                _filesChanging.Clear();
                _fileEventItems.Clear();
                _filesFinishedChanging.Clear();
            }
        }

    	public object UserState { get; set; }

    	public bool IsRunning
        {
            get
            {
                return _fileSystemWatcher.EnableRaisingEvents;
            }
        }

        public string FolderToWatch { get; private set; }

        public string IncludeFilter { get; private set; }

        public string ExcludeFilter { get; private set; }

        public int PollTime { get; private set; }

        public bool IncludeSubdirectories { get; private set; }

        private void RaiseEventsForExistingFiles()
        {
            GetAllFilesToCheck(FolderToWatch);
        }

        /// <summary>
        /// Retrieve all files that should raise an InDirectory event i.e. they match the filter.
        /// </summary>
        /// <param name="folderPath"></param>
        private void GetAllFilesToCheck(string folderPath)
        {
            if (folderPath == null || folderPath.Length <= 0) return;
            // search in subdirectories
            if (IncludeSubdirectories)
            {
                var folders = Directory.GetDirectories(folderPath);
                foreach (string folder in folders)
                {
                    GetAllFilesToCheck(folder);
                }
            }

            string[] files = null;
        	files = Directory.GetFiles(folderPath);

			foreach (var file in files)
			{
				if (!ShouldMonitorFile(file)) continue;

                Push(file, FileEventType.InDirectory);
			}
        }

        /// <summary>
        /// Check if file is in use.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static bool IsFileLocked(string filePath)
        {
            var result = false;
            FileStream file = null;
            try
            {
                file = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch
            {
                result = true;
            }
            finally
            {
                if (file != null)
                {
                    file.Close();
                }
            }
            return result;
        }

        /// <summary>
        /// Does file match provided filter.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>True if it matches filter; else false</returns>
		private bool ShouldMonitorFile(string fileName)
		{
			const RegexOptions regxOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;
			return (string.IsNullOrEmpty(IncludeFilter) || Regex.IsMatch(fileName, IncludeFilter, regxOptions))
                && (string.IsNullOrEmpty(ExcludeFilter) || !Regex.IsMatch(fileName, ExcludeFilter, regxOptions));
		}

    	#region FilesChanging Queue

        /// <summary>
        /// A file event has occured on a file already waiting to be raised, so update the time 
        /// the file event should next be fired.
        /// </summary>
        /// <param name="filePath"></param>
        private void Touch(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

        	IFileChangingItem item;
			if (_filesChanging.TryGetValue(filePath, out item))
			{
				item.FireTime = DateTime.Now.AddMilliseconds(PollTime);
			}

            if (_nextFileToCheck == filePath)
            {
                GetNextFileToCheck();
            }
        }

        /// <summary>
        /// Add a file event that has occurred for a file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileEventType"></param>
        private void Push(string filePath, FileEventType fileEventType)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            IFileChangingItem item = new FileChangingItem(filePath, fileEventType);
            item.FireTime = DateTime.Now.AddMilliseconds(PollTime);
            _filesChanging.Add(filePath, item);

            if (string.IsNullOrEmpty(_nextFileToCheck))
            {
                GetNextFileToCheck();
            }
        }

        /// <summary>
        /// Remove a file event.
        /// </summary>
        /// <param name="filePath"></param>
        private void Pop(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var fileChanging = _filesChanging[filePath];
            _filesChanging.Remove(filePath);

            _fileEventItems.Add(fileChanging);

            GetNextFileToCheck();
        }

        /// <summary>
        /// Retrieve the next file in the queue to have its event raised.
        /// </summary>
        private void GetNextFileToCheck()
        {
            _timer.Stop();
            
            var currentDateTime = DateTime.Now;

            _nextFileToCheck = string.Empty;

            var lowestDateTime = DateTime.MaxValue;
            var nextFileToCheck = string.Empty;

            foreach (var item in _filesChanging)
            {
                var dateTime = item.Value.FireTime;

                if (currentDateTime >= lowestDateTime) continue;

                lowestDateTime = dateTime;
                nextFileToCheck = item.Key;
            }    
            
            if (string.IsNullOrEmpty(nextFileToCheck))
            {
                //There are no more files to raise events for
                if (!_timerForFileActivityFinished.Enabled)
                {
                    _timerForFileActivityFinished.Interval = PollTime;
                    _timerForFileActivityFinished.Start();
                }
            }
            else
            {
                if (_timerForFileActivityFinished.Enabled)
                {
                    _timerForFileActivityFinished.Stop();
                }
                double interval = 1;

                if (lowestDateTime > currentDateTime)
                {
                    interval = lowestDateTime.Subtract(currentDateTime).TotalMilliseconds;
                }

                if (interval < 1)
                {
                    interval = 1;
                }

                _nextFileToCheck = nextFileToCheck;
                _timer.Interval = interval;
                _timer.Start();
            }
        }

        #endregion

        #region Events raised by file watcher

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>We using an in memory queue and poping them off one by one. We may have 1000s of file events so executing them all
        /// at once does not scale well.</remarks>
        private void OnTimeUp(object sender, ElapsedEventArgs e)
        { 
            lock (_filesRaisingEventsLock)
            {
                if (_timer == null) return;
                _timer.Stop();

                if (!string.IsNullOrEmpty(_nextFileToCheck) && _filesChanging.ContainsKey(_nextFileToCheck))
                {
                    var item = _filesChanging[_nextFileToCheck];
                    if (item.FireTime <= DateTime.Now)
                    {
                        var fileFinishedChangingEventArgs = new FileFinishedChangingEventArgs(item.FilePath, item.FileEventType, UserState);
                        _fileFinishedChangingCallback(fileFinishedChangingEventArgs);
                    }
                }

                GetNextFileToCheck();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>It is assumed that there will be no file events once this has been raised. Chances are that file 
        /// events are going to occur, so its up to the calling client to switch off the file watcher while processing if necessary.</remarks>
        private void OnTimeUpForFileActivityFinished(object sender, ElapsedEventArgs e)
        {
            _timerForFileActivityFinished.Stop();
            var fileEventItems = new List<IFileEventItem>(_fileEventItems);
            _fileEventItems.Clear();
            var activityFinishedEventArgs = new FileActivityFinishedEventArgs(fileEventItems, UserState);
            RaiseAsynchronousOnFileActivityFinishedEvent(activityFinishedEventArgs);
        }

        private void OnFileFinishedChanging(FileFinishedChangingEventArgs e)
        {
            lock (_filesRaisingEventsLock)
            {
                var filePath = e.FilePath;
                if (_filesChanging.ContainsKey(filePath))
                {
                    if ( IsFileLocked(filePath))
                    {
                        //The file is still currently in use, lets try raise the event later
                        Touch(filePath);
                    }
                    else
                    {
                        Pop(filePath);

                        var fileFinishedChangingEventArgs = new FileFinishedChangingEventArgs(e.FilePath, e.ChangeType, UserState);

                        //We only want to know about the last event, not any that may have happened in the mean time
                        _filesFinishedChanging[fileFinishedChangingEventArgs.FilePath] = fileFinishedChangingEventArgs;

                        RaiseAsynchronousOnFileFinishedChangingEvent(fileFinishedChangingEventArgs);

                        if (_filesChanging == null || _filesChanging.Count < 1)
                        {
                            if (_filesFinishedChanging != null && _filesFinishedChanging.Count > 0)
                            {
                                //There are no more files that are in the change queue so let everyone know the files have finished changing
                                var filesFinishedChangingEventArgs = new FilesFinishedChangingEventArgs(new List<FileFinishedChangingEventArgs>(_filesFinishedChanging.Values), UserState);
                                RaiseAsynchronousOnFilesFinishedChangingEvent(filesFinishedChangingEventArgs);

                                _filesFinishedChanging = new Dictionary<string, FileFinishedChangingEventArgs>();
                            }
                        }
                    }
                }
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;

            var isDirectory = (File.GetAttributes(filePath) & FileAttributes.Directory) == FileAttributes.Directory;

            if (isDirectory)
            {
                var directoryCreatedEventArgs = new DirectoryCreatedEventArgs(e.FullPath, UserState);
                RaiseAsynchronousOnDirectoryCreatedEvent(directoryCreatedEventArgs);
                return;
            }

			if (!ShouldMonitorFile(filePath)) return;
            if (e.ChangeType != WatcherChangeTypes.Created) return;
            lock (_filesRaisingEventsLock)
            {
                if (_filesChanging.ContainsKey(filePath))
                {
                    _filesChanging[filePath].FileEventType = FileEventType.Created;
                    Touch(filePath);
                }
                else
                {
                    Push(filePath, FileEventType.Created);
                }
            }

            var fileCreatedEventArgs = new FileCreatedEventArgs(e.FullPath, UserState);
            RaiseAsynchronousOnFileCreatedEvent(fileCreatedEventArgs);
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;

            var isDirectory = Directory.Exists(filePath) & (File.GetAttributes(filePath) & FileAttributes.Directory) == FileAttributes.Directory;

            if (isDirectory)
            {
                return;
            }

			if (!ShouldMonitorFile(filePath)) return;

            lock (_filesRaisingEventsLock)
            {
                if (_filesChanging.ContainsKey(filePath))
                {
                    _filesChanging[filePath].FileEventType = FileEventType.Changed;
                    Touch(filePath);
                }
                else
                {
                    Push(filePath, FileEventType.Changed);
                }
            }

            var fileChangedEventArgs = new FileChangedEventArgs(filePath, UserState);
            RaiseAsynchronousOnFileChangedEvent(fileChangedEventArgs);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;

            //There is no good way of detecting files without extensions and directories with full stops in name. The file/directory has been deleted so we can't find it out from the path.
            var isDirectory = Path.GetExtension(filePath) == string.Empty;

            if (isDirectory)
            {
                lock (_filesRaisingEventsLock)
                {
                    var filesInDirectory = _filesChanging.Keys.Where(x => x.StartsWith(e.FullPath + "/"));

                    foreach (var fileInDirectory in filesInDirectory)
                    {
                        Pop(fileInDirectory);
                    }
                }
                var directoryDeletedEventArgs = new DirectoryDeletedEventArgs(e.FullPath, UserState);
                RaiseAsynchronousOnDirectoryDeletedEvent(directoryDeletedEventArgs);
                return;
            }

			if (!ShouldMonitorFile(filePath)) return;

            lock (_filesRaisingEventsLock)
            {
                if (_filesChanging.ContainsKey(filePath))
                {
                    Pop(filePath);

                    var fileFinishedChangingEventArgs = new FileFinishedChangingEventArgs(filePath, FileEventType.Deleted, UserState);
                    RaiseAsynchronousOnFileFinishedChangingEvent(fileFinishedChangingEventArgs);
                }
            }

            var fileDeletedEventArgs = new FileDeletedEventArgs(e.FullPath, UserState);
            RaiseAsynchronousOnFileDeletedEvent(fileDeletedEventArgs);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            var filePath = e.FullPath;

            var isDirectory = Directory.Exists(filePath) & (File.GetAttributes(filePath) & FileAttributes.Directory) == FileAttributes.Directory;

            if (isDirectory)
            {
                lock (_filesRaisingEventsLock)
                {
                    var filesInDirectory = _filesChanging.Keys.Where(x => x.StartsWith(e.OldFullPath + "/"));

                    foreach (var fileInDirectory in filesInDirectory)
                    {
                        var fileInDirectoryPath = _filesChanging[fileInDirectory].FilePath;
                        Pop(fileInDirectory);

                        var fileFinishedChangingEventArgs = new FileFinishedChangingEventArgs(fileInDirectoryPath, FileEventType.Renamed, UserState);
                        RaiseAsynchronousOnFileFinishedChangingEvent(fileFinishedChangingEventArgs);
                    }
                }

                var directoryRenamedEventArgs = new DirectoryRenamedEventArgs(e.OldFullPath, e.FullPath, UserState);
                RaiseAsynchronousOnDirectoryRenamedEvent(directoryRenamedEventArgs);
                return;
            }

			if (!ShouldMonitorFile(filePath)) return;

            lock (_filesRaisingEventsLock)
            {
                if (_filesChanging.ContainsKey(filePath))
                {
                    Pop(filePath);

                    var fileFinishedChangingEventArgs = new FileFinishedChangingEventArgs(filePath, FileEventType.Renamed, UserState);
                    RaiseAsynchronousOnFileFinishedChangingEvent(fileFinishedChangingEventArgs);
                }
            }

            var fileRenamedEventArgs = new FileRenamedEventArgs(e.OldFullPath, e.FullPath, UserState);
            RaiseAsynchronousOnFileRenamedEvent(fileRenamedEventArgs);
        }

        #endregion


        #region FileChangedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private FileChangedEventHandler _fileChangedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _fileChangedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event FileChangedEventHandler FileChangedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_fileChangedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileChangedEvent.add");
                }
                try
                {
                    _fileChangedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_fileChangedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_fileChangedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileChangedEvent.remove");
                }
                try
                {
                    _fileChangedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_fileChangedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnFileChangedEvent(FileChangedEventArgs e)
        {
            // TODO: Implement default behaviour of OnFileChangedEvent
        }

        private void AsynchronousOnFileChangedEventRaised(object state)
        {
            var e = state as FileChangedEventArgs;
            RaiseOnFileChangedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnFileChangedEvent(FileChangedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnFileChangedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnFileChangedEvent(FileChangedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnFileChangedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnFileChangedEvent(FileChangedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            FileChangedEventHandler eventHandler;

            if (!Monitor.TryEnter(_fileChangedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnFileChangedEvent");
            }
            try
            {
                eventHandler = _fileChangedEvent;
            }
            finally
            {
                Monitor.Exit(_fileChangedEventLock);
            }

            OnFileChangedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region FileCreatedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private FileCreatedEventHandler _fileCreatedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _fileCreatedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event FileCreatedEventHandler FileCreatedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_fileCreatedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileCreatedEvent.add");
                }
                try
                {
                    _fileCreatedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_fileCreatedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_fileCreatedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileCreatedEvent.remove");
                }
                try
                {
                    _fileCreatedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_fileCreatedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnFileCreatedEvent(FileCreatedEventArgs e)
        {
            // TODO: Implement default behaviour of OnFileCreatedEvent
        }

        private void AsynchronousOnFileCreatedEventRaised(object state)
        {
            var e = state as FileCreatedEventArgs;
            RaiseOnFileCreatedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnFileCreatedEvent(FileCreatedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnFileCreatedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnFileCreatedEvent(FileCreatedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnFileCreatedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnFileCreatedEvent(FileCreatedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            FileCreatedEventHandler eventHandler;

            if (!Monitor.TryEnter(_fileCreatedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnFileCreatedEvent");
            }
            try
            {
                eventHandler = _fileCreatedEvent;
            }
            finally
            {
                Monitor.Exit(_fileCreatedEventLock);
            }

            OnFileCreatedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region FileDeletedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private FileDeletedEventHandler _fileDeletedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _fileDeletedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event FileDeletedEventHandler FileDeletedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_fileDeletedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileDeletedEvent.add");
                }
                try
                {
                    _fileDeletedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_fileDeletedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_fileDeletedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileDeletedEvent.remove");
                }
                try
                {
                    _fileDeletedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_fileDeletedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnFileDeletedEvent(FileDeletedEventArgs e)
        {
            // TODO: Implement default behaviour of OnFileDeletedEvent
        }

        private void AsynchronousOnFileDeletedEventRaised(object state)
        {
            var e = state as FileDeletedEventArgs;
            RaiseOnFileDeletedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnFileDeletedEvent(FileDeletedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnFileDeletedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnFileDeletedEvent(FileDeletedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnFileDeletedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnFileDeletedEvent(FileDeletedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            FileDeletedEventHandler eventHandler;

            if (!Monitor.TryEnter(_fileDeletedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnFileDeletedEvent");
            }
            try
            {
                eventHandler = _fileDeletedEvent;
            }
            finally
            {
                Monitor.Exit(_fileDeletedEventLock);
            }

            OnFileDeletedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region FileRenamedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private FileRenamedEventHandler _fileRenamedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _fileRenamedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event FileRenamedEventHandler FileRenamedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_fileRenamedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileRenamedEvent.add");
                }
                try
                {
                    _fileRenamedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_fileRenamedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_fileRenamedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileRenamedEvent.remove");
                }
                try
                {
                    _fileRenamedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_fileRenamedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnFileRenamedEvent(FileRenamedEventArgs e)
        {
            // TODO: Implement default behaviour of OnFileRenamedEvent
        }

        private void AsynchronousOnFileRenamedEventRaised(object state)
        {
            var e = state as FileRenamedEventArgs;
            RaiseOnFileRenamedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnFileRenamedEvent(FileRenamedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnFileRenamedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnFileRenamedEvent(FileRenamedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnFileRenamedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnFileRenamedEvent(FileRenamedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            FileRenamedEventHandler eventHandler;

            if (!Monitor.TryEnter(_fileRenamedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnFileRenamedEvent");
            }
            try
            {
                eventHandler = _fileRenamedEvent;
            }
            finally
            {
                Monitor.Exit(_fileRenamedEventLock);
            }

            OnFileRenamedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion


        #region DirectoryCreatedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private DirectoryCreatedEventHandler _directoryCreatedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _directoryCreatedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event DirectoryCreatedEventHandler DirectoryCreatedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_directoryCreatedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - DirectoryCreatedEvent.add");
                }
                try
                {
                    _directoryCreatedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_directoryCreatedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_directoryCreatedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - DirectoryCreatedEvent.remove");
                }
                try
                {
                    _directoryCreatedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_directoryCreatedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnDirectoryCreatedEvent(DirectoryCreatedEventArgs e)
        {
            // TODO: Implement default behaviour of OnDirectoryCreatedEvent
        }

        private void AsynchronousOnDirectoryCreatedEventRaised(object state)
        {
            var e = state as DirectoryCreatedEventArgs;
            RaiseOnDirectoryCreatedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnDirectoryCreatedEvent(DirectoryCreatedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnDirectoryCreatedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnDirectoryCreatedEvent(DirectoryCreatedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnDirectoryCreatedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnDirectoryCreatedEvent(DirectoryCreatedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            DirectoryCreatedEventHandler eventHandler;

            if (!Monitor.TryEnter(_directoryCreatedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnDirectoryCreatedEvent");
            }
            try
            {
                eventHandler = _directoryCreatedEvent;
            }
            finally
            {
                Monitor.Exit(_directoryCreatedEventLock);
            }

            OnDirectoryCreatedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region DirectoryDeletedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private DirectoryDeletedEventHandler _directoryDeletedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _directoryDeletedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event DirectoryDeletedEventHandler DirectoryDeletedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_directoryDeletedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - DirectoryDeletedEvent.add");
                }
                try
                {
                    _directoryDeletedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_directoryDeletedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_directoryDeletedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - DirectoryDeletedEvent.remove");
                }
                try
                {
                    _directoryDeletedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_directoryDeletedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnDirectoryDeletedEvent(DirectoryDeletedEventArgs e)
        {
            // TODO: Implement default behaviour of OnDirectoryDeletedEvent
        }

        private void AsynchronousOnDirectoryDeletedEventRaised(object state)
        {
            var e = state as DirectoryDeletedEventArgs;
            RaiseOnDirectoryDeletedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnDirectoryDeletedEvent(DirectoryDeletedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnDirectoryDeletedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnDirectoryDeletedEvent(DirectoryDeletedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnDirectoryDeletedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnDirectoryDeletedEvent(DirectoryDeletedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            DirectoryDeletedEventHandler eventHandler;

            if (!Monitor.TryEnter(_directoryDeletedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnDirectoryDeletedEvent");
            }
            try
            {
                eventHandler = _directoryDeletedEvent;
            }
            finally
            {
                Monitor.Exit(_directoryDeletedEventLock);
            }

            OnDirectoryDeletedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region DirectoryRenamedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private DirectoryRenamedEventHandler _directoryRenamedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _directoryRenamedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event DirectoryRenamedEventHandler DirectoryRenamedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_directoryRenamedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - DirectoryRenamedEvent.add");
                }
                try
                {
                    _directoryRenamedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_directoryRenamedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_directoryRenamedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - DirectoryRenamedEvent.remove");
                }
                try
                {
                    _directoryRenamedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_directoryRenamedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnDirectoryRenamedEvent(DirectoryRenamedEventArgs e)
        {
            // TODO: Implement default behaviour of OnDirectoryRenamedEvent
        }

        private void AsynchronousOnDirectoryRenamedEventRaised(object state)
        {
            var e = state as DirectoryRenamedEventArgs;
            RaiseOnDirectoryRenamedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnDirectoryRenamedEvent(DirectoryRenamedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnDirectoryRenamedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnDirectoryRenamedEvent(DirectoryRenamedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnDirectoryRenamedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnDirectoryRenamedEvent(DirectoryRenamedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            DirectoryRenamedEventHandler eventHandler;

            if (!Monitor.TryEnter(_directoryRenamedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnDirectoryRenamedEvent");
            }
            try
            {
                eventHandler = _directoryRenamedEvent;
            }
            finally
            {
                Monitor.Exit(_directoryRenamedEventLock);
            }

            OnDirectoryRenamedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion


        #region FileFinishedChangingEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private FileFinishedChangingEventHandler _fileFinishedChangingEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _fileFinishedChangingEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event FileFinishedChangingEventHandler FileFinishedChangingEvent
        {
            add
            {
                if (!Monitor.TryEnter(_fileFinishedChangingEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileFinishedChangingEvent.add");
                }
                try
                {
                    _fileFinishedChangingEvent += value;
                }
                finally
                {
                    Monitor.Exit(_fileFinishedChangingEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_fileFinishedChangingEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileFinishedChangingEvent.remove");
                }
                try
                {
                    _fileFinishedChangingEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_fileFinishedChangingEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnFileFinishedChangingEvent(FileFinishedChangingEventArgs e)
        {
            // TODO: Implement default behaviour of OnFileFinishedChangingEvent
        }

        private void AsynchronousOnFileFinishedChangingEventRaised(object state)
        {
            var e = state as FileFinishedChangingEventArgs;
            RaiseOnFileFinishedChangingEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnFileFinishedChangingEvent(FileFinishedChangingEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnFileFinishedChangingEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnFileFinishedChangingEvent(FileFinishedChangingEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnFileFinishedChangingEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnFileFinishedChangingEvent(FileFinishedChangingEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            FileFinishedChangingEventHandler eventHandler;

            if (!Monitor.TryEnter(_fileFinishedChangingEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnFileFinishedChangingEvent");
            }
            try
            {
                eventHandler = _fileFinishedChangingEvent;
            }
            finally
            {
                Monitor.Exit(_fileFinishedChangingEventLock);
            }

            OnFileFinishedChangingEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region FilesFinishedChangingEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private FilesFinishedChangingEventHandler _filesFinishedChangingEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _filesFinishedChangingEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event FilesFinishedChangingEventHandler FilesFinishedChangingEvent
        {
            add
            {
                if (!Monitor.TryEnter(_filesFinishedChangingEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FilesFinishedChangingEvent.add");
                }
                try
                {
                    _filesFinishedChangingEvent += value;
                }
                finally
                {
                    Monitor.Exit(_filesFinishedChangingEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_filesFinishedChangingEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FilesFinishedChangingEvent.remove");
                }
                try
                {
                    _filesFinishedChangingEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_filesFinishedChangingEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnFilesFinishedChangingEvent(FilesFinishedChangingEventArgs e)
        {
            // TODO: Implement default behaviour of OnFilesFinishedChangingEvent
        }

        private void AsynchronousOnFilesFinishedChangingEventRaised(object state)
        {
            var e = state as FilesFinishedChangingEventArgs;
            RaiseOnFilesFinishedChangingEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnFilesFinishedChangingEvent(FilesFinishedChangingEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnFilesFinishedChangingEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnFilesFinishedChangingEvent(FilesFinishedChangingEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnFilesFinishedChangingEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnFilesFinishedChangingEvent(FilesFinishedChangingEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            FilesFinishedChangingEventHandler eventHandler;

            if (!Monitor.TryEnter(_filesFinishedChangingEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnFilesFinishedChangingEvent");
            }
            try
            {
                eventHandler = _filesFinishedChangingEvent;
            }
            finally
            {
                Monitor.Exit(_filesFinishedChangingEventLock);
            }

            OnFilesFinishedChangingEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region FileActivityFinishedEvent
        /// <summary>
        /// Where the actual event is stored.
        /// </summary>
        private FileActivityFinishedEventHandler _fileActivityFinishedEvent;

        /// <summary>
        /// Lock for event delegate access.
        /// </summary>
        private readonly object _fileActivityFinishedEventLock = new object();

        /// <summary>
        /// The event that is fired.
        /// </summary>
        public event FileActivityFinishedEventHandler FileActivityFinishedEvent
        {
            add
            {
                if (!Monitor.TryEnter(_fileActivityFinishedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileActivityFinishedEvent.add");
                }
                try
                {
                    _fileActivityFinishedEvent += value;
                }
                finally
                {
                    Monitor.Exit(_fileActivityFinishedEventLock);
                }
            }
            remove
            {
                if (!Monitor.TryEnter(_fileActivityFinishedEventLock, _lockTimeout))
                {
                    throw new ApplicationException("Timeout waiting for lock - FileActivityFinishedEvent.remove");
                }
                try
                {
                    _fileActivityFinishedEvent -= value;
                }
                finally
                {
                    Monitor.Exit(_fileActivityFinishedEventLock);
                }
            }
        }

        /// <summary>
        /// Template method to add default behaviour for the event
        /// </summary>
        private void OnFileActivityFinishedEvent(FileActivityFinishedEventArgs e)
        {
            // TODO: Implement default behaviour of OnFileActivityFinishedEvent
        }

        private void AsynchronousOnFileActivityFinishedEventRaised(object state)
        {
            var e = state as FileActivityFinishedEventArgs;
            RaiseOnFileActivityFinishedEvent(e);
        }

        /// <summary>
        /// Will raise the event on the calling thread synchronously. 
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseCrossThreadOnFileActivityFinishedEvent(FileActivityFinishedEventArgs e)
        {
            _asyncOperation.SynchronizationContext.Send(new SendOrPostCallback(AsynchronousOnFileActivityFinishedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the calling thread asynchronously. 
        /// i.e. it will immediatly continue processing even though event 
        /// handlers have not processed the event yet.
        /// </summary>
        /// <param name="state">The state to be passed to the event.</param>
        private void RaiseAsynchronousOnFileActivityFinishedEvent(FileActivityFinishedEventArgs e)
        {
            _asyncOperation.Post(new SendOrPostCallback(AsynchronousOnFileActivityFinishedEventRaised), e);
        }

        /// <summary>
        /// Will raise the event on the current thread synchronously.
        /// i.e. it will wait until all event handlers have processed the event.
        /// </summary>
        /// <param name="e">The state to be passed to the event.</param>
        private void RaiseOnFileActivityFinishedEvent(FileActivityFinishedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.

            FileActivityFinishedEventHandler eventHandler;

            if (!Monitor.TryEnter(_fileActivityFinishedEventLock, _lockTimeout))
            {
                throw new ApplicationException("Timeout waiting for lock - RaiseOnFileActivityFinishedEvent");
            }
            try
            {
                eventHandler = _fileActivityFinishedEvent;
            }
            finally
            {
                Monitor.Exit(_fileActivityFinishedEventLock);
            }

            OnFileActivityFinishedEvent(e);

            if (eventHandler != null)
            {
                eventHandler(this, e);
            }
        }
        #endregion

        #region IDisposable Members
        private int _alreadyDisposed = 0;

        ~EnhancedFileSystemWatcher()
        {
            Dispose(false);
        }

        void IDisposable.Dispose()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_alreadyDisposed != 0) return;
            // dispose of the managed and unmanaged resources
            Dispose(true);

            // tell the GC that the Finalize process no longer needs
            // to be run for this object.		
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposeManagedResources)
        {
            if (!disposeManagedResources) return;
            var disposedAlready = Interlocked.Exchange(ref _alreadyDisposed, 1);
            if (disposedAlready != 0)
            {
                return;
            }

            // Dispose managed resources.

            _fileSystemWatcher.Changed -= _fileSystemWatcherChangedEvent;
            _fileSystemWatcher.Created -= _fileSystemWatcherCreatedEvent;
            _fileSystemWatcher.Deleted -= _fileSystemWatcherDeletedEvent;
            _fileSystemWatcher.Renamed -= _fileSystemWatcherRenamedEvent;

            _fileSystemWatcher.Dispose();

            _fileChangedEvent = null;
            _fileCreatedEvent = null;
            _fileDeletedEvent = null;
            _fileRenamedEvent = null;
            _fileFinishedChangingEvent = null;
            // Dispose unmanaged resources.

            // If it is available, make the call to the
            // base class's Dispose(Boolean) method
        }
        #endregion
    }
}
