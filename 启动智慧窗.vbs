Option Explicit

Dim shell, folder, executable
Set shell = CreateObject("WScript.Shell")
folder = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
executable = folder & "\SmartWindow.exe"

If Not CreateObject("Scripting.FileSystemObject").FileExists(executable) Then
    MsgBox "SmartWindow.exe is missing. Keep the delivered folder intact.", 16, "Smart Window"
    WScript.Quit 1
End If

shell.CurrentDirectory = folder
shell.Run Chr(34) & executable & Chr(34), 0, False
