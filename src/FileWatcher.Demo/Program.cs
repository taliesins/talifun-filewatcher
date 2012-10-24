using System;
using System.IO;
using Talifun.FileWatcher;

namespace FileWatcher.Demo
{
    public class Program
    {
        private const string WatcherFolder = "WatchedFolder";
        private static readonly object Lock = new object();
        static void Main(string[] args)
        {
            var watchedFolder = new DirectoryInfo(WatcherFolder);
            if (!watchedFolder.Exists)
            {
                watchedFolder.Create();
            }

            Console.WriteLine("Press any key to exit");
            Console.WriteLine(string.Format("Try copying one file into watched folder ({0})", watchedFolder.FullName));
            Console.WriteLine("Try copying a batch of files into a watched folder");

            var fileWatcher = EnhancedFileSystemWatcherFactory.Instance.CreateEnhancedFileSystemWatcher(watchedFolder.FullName, "", 1000, true);
            fileWatcher.Start();

            fileWatcher.DirectoryCreatedEvent += OnFileWatcherDirectoryCreatedEvent;
            fileWatcher.DirectoryDeletedEvent += OnFileWatcherDirectoryDeletedEvent;
            fileWatcher.DirectoryRenamedEvent += OnFileWatcherDirectoryRenamedEvent;

            fileWatcher.FileCreatedEvent += OnFileCreatedEvent;
            fileWatcher.FileChangedEvent += OnFileChangedEvent;
            fileWatcher.FileFinishedChangingEvent += OnFileFinishedChangingEvent;
            fileWatcher.FileRenamedEvent += OnFileRenamedEvent;
            fileWatcher.FileDeletedEvent += OnFileDeletedEvent;
            fileWatcher.FilesFinishedChangingEvent += OnFilesFinishedChangingEvent;
            fileWatcher.FileActivityFinishedEvent += OnFileActivityFinishedEvent;

            Console.ReadKey();
            fileWatcher.Stop();
        }

        static void OnFileWatcherDirectoryRenamedEvent(object sender, DirectoryRenamedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnDirectoryRenamedEvent:");
                Console.WriteLine("  OldFilePath = {0}", e.OldFilePath);
                Console.WriteLine("  NewFilePath = {0}", e.NewFilePath);
                Console.WriteLine();
            }
        }

        static void OnFileWatcherDirectoryDeletedEvent(object sender, DirectoryDeletedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnDirectoryDeletedEvent:");
                Console.WriteLine("  FilePath = {0}", e.FilePath);
                Console.WriteLine();
            }
        }

        static void OnFileWatcherDirectoryCreatedEvent(object sender, DirectoryCreatedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnDirectoryCreatedEvent:");
                Console.WriteLine("  FilePath = {0}", e.FilePath);
                Console.WriteLine();
            }
        }

        static void OnFileDeletedEvent(object sender, FileDeletedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnFileDeletedEvent:");
                Console.WriteLine("  FilePath = {0}", e.FilePath);
                Console.WriteLine();
            }
        }

        static void OnFileChangedEvent(object sender, FileChangedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnFileChangedEvent :");
                Console.WriteLine("  FilePath = {0}", e.FilePath);
                Console.WriteLine();
            }
        }

        static void OnFileRenamedEvent(object sender, FileRenamedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnFileRenamedEvent:");
                Console.WriteLine("  OldFilePath = {0}", e.OldFilePath);
                Console.WriteLine("  NewFilePath = {0}", e.NewFilePath);
                Console.WriteLine();
            }
        }

        static void OnFileCreatedEvent(object sender, FileCreatedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnFileCreatedEvent:");
                Console.WriteLine("  FilePath = {0}", e.FilePath);
                Console.WriteLine();
            }
        }

        static void OnFilesFinishedChangingEvent(object sender, FilesFinishedChangingEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnFilesFinishedChangingEvent:");
                foreach (var fileFinishedChangingEventArgs in e.FilesFinishedChanging)
                {
                    Console.WriteLine("  FilePath = {0}", fileFinishedChangingEventArgs.FilePath);
                    Console.WriteLine("  ChangeType = {0}",
                                      Enum.GetName(typeof (FileEventType), fileFinishedChangingEventArgs.ChangeType));
                }
                Console.WriteLine();
            }
        }

        static void OnFileFinishedChangingEvent(object sender, FileFinishedChangingEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnFileFinishedChangingEvent:");
                Console.WriteLine("  FilePath = {0}", e.FilePath);
                Console.WriteLine("  ChangeType = {0}", Enum.GetName(typeof (FileEventType), e.ChangeType));
                Console.WriteLine();
            }
        }

        static void OnFileActivityFinishedEvent(object sender, FileActivityFinishedEventArgs e)
        {
            lock (Lock)
            {
                Console.WriteLine("OnFileActivityFinishedEvent:");
                Console.WriteLine();
            }
        }
    }
}
