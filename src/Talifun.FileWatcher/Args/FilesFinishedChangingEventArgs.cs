using System;
using System.Collections.Generic;

namespace Talifun.FileWatcher
{
    public class FilesFinishedChangingEventArgs : EventArgs
    {
        public FilesFinishedChangingEventArgs(List<FileFinishedChangingEventArgs> filesFinishedChanging, object userState)
        {
            FilesFinishedChanging = filesFinishedChanging;
            UserState = userState;
        }

        public List<FileFinishedChangingEventArgs> FilesFinishedChanging { get; private set; }
        public object UserState { get; private set; }
    }
}
