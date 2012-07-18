using System;

namespace Talifun.FileWatcher
{
    public class FileActivityFinishedEventArgs : EventArgs
    {
        public FileActivityFinishedEventArgs(object userState)
        {
            UserState = userState;
        }

        public object UserState { get; private set; }
    }
}
