''' <summary>
''' Raised when game data, lookup data or the editor configuration cannot be loaded.
''' The <see cref="Exception.Message"/> is already phrased for the end user (it names the
''' file and, where possible, the line/row), so the GUI can show it directly instead of the
''' generic "unhandled error, report this" dialog.
''' </summary>
Public Class DataLoadException
    Inherits Exception

    Public Sub New(message As String)
        MyBase.New(message)
    End Sub

    Public Sub New(message As String, inner As Exception)
        MyBase.New(message, inner)
    End Sub

End Class
