using System;
using System.Collections.Generic;

namespace Talifun.FileWatcher
{
    public class AllFilesFinishedChangingEventArgs : EventArgs
    {
        public AllFilesFinishedChangingEventArgs(List<FileFinishedChangingEventArgs> filesFinishedChanging)
        {
            FilesFinishedChanging = filesFinishedChanging;
        }

        public List<FileFinishedChangingEventArgs> FilesFinishedChanging { get; private set; }
    }
}
