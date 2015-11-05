$lineBreak = "`r`n"
$noticeTemplate = "// Copyright (c) Microsoft. All rights reserved.$lineBreak// Licensed under the MIT license. See LICENSE file in the project root for full license information.$lineBreak$lineBreak"
$tokenToReplace = [regex]::Escape("[FileName]")

Function CreateFileSpecificNotice($sourcePath){
    $fileName = Split-Path $sourcePath -Leaf
    $fileSpecificNotice = $noticeTemplate -replace $tokenToReplace, $fileName
    return $fileSpecificNotice
}

Function SourceFileContainsNotice($sourcePath){
    $copyrightSnippet = [regex]::Escape("// Copyright (c) Microsoft")

    $fileSpecificNotice = CreateFileSpecificNotice($sourcePath)
    $arrMatchResults = Get-Content $sourcePath | Select-String $copyrightSnippet

    if ($arrMatchResults -ne $null -and $arrMatchResults.count -gt 0){
        return $true 
    }
    else{ 
        return $false 
    }
}

Function AddHeaderToSourceFile($sourcePath) {
    # "Source path is: $sourcePath"
    
    $containsNotice = SourceFileContainsNotice($sourcePath)
    # "Contains notice: $containsNotice"

    if ($containsNotice){
        #"Source file already contains notice -- not adding"
    }
    else {
        #"Source file does not contain notice -- adding"
        $noticeToInsert = CreateFileSpecificNotice($sourcePath)

        $fileLines = (Get-Content $sourcePath) -join $lineBreak
    
        $content = $noticeToInsert + $fileLines + $lineBreak

        $content | Out-File $sourcePath -Encoding utf8

    }
}

$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
$parent = (get-item $scriptPath).Parent.FullName
$startingPath = "$parent\src"
Get-ChildItem  $startingPath\*.cs -Recurse | Select FullName | Foreach-Object { AddHeaderToSourceFile($_.FullName)}
Get-ChildItem  $startingPath\*.fs -Recurse | Select FullName | Foreach-Object { AddHeaderToSourceFile($_.FullName)}
