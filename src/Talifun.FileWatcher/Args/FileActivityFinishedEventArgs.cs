using System;
using System.Collections.Generic;

namespace Talifun.FileWatcher
{
    public class FileActivityFinishedEventArgs : EventArgs
    {
        public FileActivityFinishedEventArgs(IEnumerable<IFileEventItem> fileEventItems, object userState)
        {
            FileEventItems = fileEventItems;
            UserState = userState;
        }

        public IEnumerable<IFileEventItem> FileEventItems { get; private set; }
        public object UserState { get; private set; }
    }
}
