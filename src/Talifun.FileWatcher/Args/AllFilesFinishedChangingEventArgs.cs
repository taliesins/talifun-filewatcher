using System;
using System.Collections.Generic;

namespace Talifun.FileWatcher
{
    public class AllFilesFinishedChangingEventArgs : EventArgs
    {
        public AllFilesFinishedChangingEventArgs(List<FileFinishedChangingEventArgs> filesFinishedChanging, object userState)
        {
            FilesFinishedChanging = filesFinishedChanging;
            UserState = userState;
        }

        public List<FileFinishedChangingEventArgs> FilesFinishedChanging { get; private set; }
        public object UserState { get; private set; }
    }
}
