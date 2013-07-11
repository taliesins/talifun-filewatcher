cls
Get-EventSubscriber | Unregister-Event

Import-Module .\AutoTest.psm1 -Force

Invoke-AutoTest -folderToWatch "D:\projects\talifun-filewatcher\bin\"