
$root = Split-Path -parent $MyInvocation.MyCommand.Definition
$fileWatcherAssemblyPath = Resolve-Path "$root\..\..\..\bin\4.0\Talifun.FileWatcher.dll"


[Reflection.Assembly]::LoadFile($fileWatcherAssemblyPath)
$pesterModule = "$root\Pester\Pester.psm1"
Import-Module $pesterModule -verbose


function Wait-KeyPress() {
    If ($psISE) {
        Write-Host 'Press ctr+c to exit'
        while($true) 
        {
            Start-Sleep -milliseconds 100
        } 
    } 
    else
    {
        Write-Host 'Press any key to exit'
        do {
            Start-Sleep -milliseconds 100
        } until ($Host.UI.RawUI.KeyAvailable)

        $Host.UI.RawUI.FlushInputBuffer()
    }
}

function Invoke-PesterForFile {
    param(
        [Parameter(Position=0,Mandatory=$true)]
        [string]$fixtures_path,
        [Parameter(Position=1,Mandatory=$false)]
        [string]$testName = $null, 
        [Parameter(Position=2,Mandatory=$false)]
        [switch]$EnableExit, 
        [Parameter(Position=3,Mandatory=$false)]
        [string]$OutputXml = '',
        [Parameter(Position=4,Mandatory=$false)]
        [string]$Tags = $null,
        [switch]$EnableLegacyExpectations = $false

    )

}

function Invoke-AutoTest {
	param(
		[Parameter(Position=0,Mandatory=$false)]
		[string]$folderToWatch = (Resolve-Path "."),
		[Parameter(Position=1,Mandatory=$false)]
		[string]$includeFilter = ".+?\.(ps1|psm1|psm1)$",
		[Parameter(Position=2,Mandatory=$false)]
		[string]$excludeFilter = "",
		[Parameter(Position=3,Mandatory=$false)]
		[int]$pollTime = 2000,
		[Parameter(Position=4,Mandatory=$false)]
		[boolean]$includeSubdirectories = $true
	)

	$watcher = new-object Talifun.FileWatcher.EnhancedFileSystemWatcher($folderToWatch, $includeFilter, $excludeFilter, $pollTime, $includeSubdirectories) 

	Register-ObjectEvent $watcher -EventName FileFinishedChangingEvent -SourceIdentifier 'FileFinishedChangingEvent' -Action { param($sender, $eventArgs) 
        if (!($eventArgs.ChangeType -eq [Talifun.FileWatcher.FileEventType]::Deleted) -and !($eventArgs.FilePath.EndsWith(".test.ps1")))
        {        
            $testFileName = "$([System.IO.Path]::GetFileNameWithoutExtension($eventArgs.FilePath)).test.ps1"
            $testFileDirectoryPath = split-path $eventArgs.FilePath
            $testFilePath = Join-Path $testFileDirectoryPath $testFileName
            
            if (Test-Path $testFilePath)
            {
                Write-Host ("*"*20) 
                Write-Host "$testFilePath - $($eventArgs.ChangeType)"
                Write-Host ("*"*20)
                
                Invoke-Pester $testFilePath

                Write-Host ("*"*20)
            }
        }
	}

    $watcher.Start()

    Wait-KeyPress

    $watcher.Stop()
}

