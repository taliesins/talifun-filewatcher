using System;

namespace Talifun.FileWatcher
{
    public class DirectoryRenamedEventArgs : EventArgs
    {
        public DirectoryRenamedEventArgs(string oldFilePath, string newFilePath, object userState)
        {
            OldFilePath = oldFilePath;
            NewFilePath = newFilePath;
            UserState = userState;
        }

        public string OldFilePath { get; private set; }
        public string NewFilePath { get; private set; }
        public object UserState { get; private set; }
    }
}