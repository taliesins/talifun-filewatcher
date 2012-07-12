using System;
using System.IO;
using Talifun.FileWatcher;

namespace FileWatcher.Demo
{
    public class Program
    {
        private const string WatcherFolder = "WatchedFolder";
        static void Main(string[] args)
        {
            if (!Directory.Exists(WatcherFolder))
            {
                Directory.CreateDirectory(WatcherFolder);
            }

            Console.WriteLine("Press any key to exit");
            Console.WriteLine("Try copying one file into watched folder");
            Console.WriteLine("Try copying a batch of files into a watched folder");

            var fileWatcher = EnhancedFileSystemWatcherFactory.Instance.CreateEnhancedFileSystemWatcher(WatcherFolder);
            fileWatcher.Start();
            fileWatcher.FileFinishedChangingEvent += OnFileFinishedChangingEvent;
            fileWatcher.AllFilesFinishedChangingEvent += OnAllFilesFinishedChangingEvent;

            Console.ReadKey();
            fileWatcher.Stop();
        }

        static void OnAllFilesFinishedChangingEvent(object sender, AllFilesFinishedChangingEventArgs e)
        {
            Console.WriteLine("OnAllFilesFinishedChangingEvent:");
            foreach (var fileFinishedChangingEventArgs in e.FilesFinishedChanging)
            {
                Console.WriteLine("  FilePath = {0}", fileFinishedChangingEventArgs.FilePath);
                Console.WriteLine("  ChangeType = {0}", Enum.GetName(typeof(WatcherChangeTypes), fileFinishedChangingEventArgs.ChangeType));
            }
            Console.WriteLine();
        }

        static void OnFileFinishedChangingEvent(object sender, FileFinishedChangingEventArgs e)
        {
            Console.WriteLine("FileFinishedChangingEvent:");
            Console.WriteLine("  FilePath = {0}", e.FilePath);
            Console.WriteLine("  ChangeType = {0}", Enum.GetName(typeof(WatcherChangeTypes), e.ChangeType));
            Console.WriteLine();
        }
    }
}
