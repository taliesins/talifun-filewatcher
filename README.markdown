Talifun-FileWatcher
===========
# Status
Stable. 

# LICENSE
Apache 2.0 - see LICENSE.txt

# RESOURCES
## Source
1. Clone to repository locally: `git clone git@github.com:taliesins/talifun-filewatcher.git`

## Build
* Run build.cmd

# Description

A file watcher that waits for file change events to finish on a file before raising change events. Useful when you want to know when to begin processing a file. e.g. ftp connection drops and it resumes uploading, you dont want to process the file until the entire file has finished being uploaded.

It also will raise an event when all file change events on watched folders have finished. Useful when you want to know when to begin processing a btach of files. e.g. copy and paste a couple of directories into a watcher folder, you only want to start processing all the files once the file copy has finished.

Note: Keep in mind that file events can occur during processing of events, so if possible move files to be processed into a protected directory where further file events are unlikely to occur.
Note: If you are running into random file access violations, make sure antivirus scanning files is not causing any problems.