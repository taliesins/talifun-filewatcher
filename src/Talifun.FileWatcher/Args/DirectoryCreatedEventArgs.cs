using System;

namespace Talifun.FileWatcher
{
    public class DirectoryCreatedEventArgs : EventArgs
    {
        public DirectoryCreatedEventArgs(string filePath, object userState)
        {
            FilePath = filePath;
            UserState = userState;
        }

        public string FilePath { get; private set; }
        public object UserState { get; private set; }
    }
}
