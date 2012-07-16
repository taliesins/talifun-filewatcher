using System;

namespace Talifun.FileWatcher
{
    public class FileFinishedChangingEventArgs : EventArgs
    {
        public FileFinishedChangingEventArgs(string filePath, FileEventType changeType, object userState)
        {
            FilePath = filePath;
            ChangeType = changeType;
            UserState = userState;
        }

        public string FilePath { get; private set; }
        public FileEventType ChangeType { get; private set; }
        public object UserState { get; private set; }
    }
}
