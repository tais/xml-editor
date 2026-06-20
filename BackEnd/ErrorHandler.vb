Public Class ErrorHandler

    Protected Shared strLogFilename As String
    Protected Shared objTxtWriter As IO.StreamWriter
    Protected Shared blnSendToEventLog As Boolean
    Protected Shared blnWriteToConsole As Boolean
    Protected Shared blnLogFileActive As Boolean = True

    Public Shared Event FatalError()


#Region " Public Properties "
    Public Shared Property SendMessagesToEventLog() As Boolean
        Get
            Return blnSendToEventLog
        End Get
        Set(ByVal Value As Boolean)
            blnSendToEventLog = Value
        End Set
    End Property
    Public Shared Property WriteToConsole() As Boolean
        Get
            Return blnWriteToConsole
        End Get
        Set(ByVal Value As Boolean)
            blnWriteToConsole = Value
        End Set
    End Property
    Public Shared ReadOnly Property LoggingToTextFile() As Boolean
        Get
            Return blnLogFileActive
        End Get
    End Property
#End Region

#Region " Public Methods "
    Public Shared Function LogError(ByVal Exception As Exception) As String
        'Log the error information to the Application Log & a text file
        Dim strLogMsg As String
        Dim strTrace() As String
        Dim i As Integer
        Try
            With Exception
                Dim str As String = vbCrLf & vbTab & vbTab & vbTab

                strTrace = Split(.StackTrace, vbCrLf)

                strLogMsg = "Error: " & str _
                & "Source: " & .Source & str _
                & "Base Error Type: " & .GetType.ToString & str _
                & "Message: " & .Message & str _
                & "Stack Trace:  "

                For i = 0 To strTrace.GetUpperBound(0)
                    strLogMsg = strLogMsg & Trim(strTrace(i)) & str
                    If i < strTrace.GetUpperBound(0) Then strLogMsg = strLogMsg & vbTab & vbTab
                Next i


                If Not .TargetSite Is Nothing Then
                    strLogMsg = strLogMsg _
                                & "Target Site:  " & .TargetSite.Name & str
                End If

                strLogMsg = strLogMsg _
                & "Available Physical Memory:  " & My.Computer.Info.AvailablePhysicalMemory & str _
                & "Available Virtual Memory:  " & My.Computer.Info.AvailableVirtualMemory

            End With

            LogString(strLogMsg)

            Return strLogMsg
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Unhandled Error!", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1)
            Return Nothing
        End Try
    End Function

    Public Shared Function LogMessage(ByVal Message As String) As String
        'Log a message to the Application Log & a text file
        Dim strLogMsg As String
        Try
            Dim str As String = vbCrLf
            strLogMsg = "Message:  " & str & vbTab & vbTab & vbTab & Replace(Replace(Message, vbTab, ""), vbCrLf, vbCrLf & vbTab & vbTab & vbTab)

            LogString(strLogMsg, EventLogEntryType.Information)

            Return strLogMsg
        Catch ex As Exception
            Return LogError(ex)
        End Try
    End Function

    Public Shared Function LogWarning(ByVal Message As String) As String
        'Log a message to the Application Log & a text file
        Dim strLogMsg As String
        Try
            Dim str As String = vbCrLf
            strLogMsg = "Warning:  " & str & vbTab & vbTab & vbTab & Replace(Message, vbCrLf, vbCrLf & vbTab & vbTab & vbTab)

            LogString(Message, EventLogEntryType.Warning)

            Return strLogMsg

        Catch ex As Exception
            Return LogError(ex)
        End Try
    End Function

    ''' <summary>
    ''' Turns a load/parse exception into one user-readable sentence that names the file and,
    ''' for XML errors, the line and column. Used so data problems produce actionable messages
    ''' instead of raw .NET exception text.
    ''' </summary>
    Public Shared Function DescribeLoadError(ByVal ex As Exception, ByVal fileName As String) As String
        Dim where As String = If(String.IsNullOrEmpty(fileName), "A data file", "'" & fileName & "'")

        ' Drill down to the most specific cause - ReadXml/DataSet usually wrap the real error.
        Dim cause As Exception = ex
        While cause.InnerException IsNot Nothing
            cause = cause.InnerException
        End While

        If TypeOf cause Is System.Xml.XmlException Then
            Dim xe As System.Xml.XmlException = DirectCast(cause, System.Xml.XmlException)
            Return where & " is not valid XML (line " & xe.LineNumber & ", column " & xe.LinePosition & "): " & xe.Message
        ElseIf TypeOf cause Is System.IO.FileNotFoundException Then
            Return where & " could not be found."
        ElseIf TypeOf cause Is System.IO.DirectoryNotFoundException Then
            Return where & " is in a folder that does not exist: " & cause.Message
        ElseIf TypeOf cause Is System.IO.IOException Then
            Return where & " could not be read - it may be open in another program: " & cause.Message
        ElseIf TypeOf cause Is FormatException OrElse TypeOf cause Is InvalidCastException Then
            Return where & " contains a value of the wrong type (for example text where a number is expected): " & cause.Message
        ElseIf TypeOf cause Is System.Data.ConstraintException Then
            Return where & " contains invalid data (a missing required value, a duplicate id, or a reference to something that does not exist): " & cause.Message
        Else
            Return where & ": " & cause.Message
        End If
    End Function

    Public Shared Sub ShowError(ByVal ex As Exception)
        ShowError("", ex)
    End Sub

    Public Shared Sub ShowError(ByVal message As String, ByVal ex As Exception)
        ShowError(message, "", ex)
    End Sub

    Public Shared Sub ShowError(ByVal message As String, ByVal caption As String, ByVal ex As Exception)
        ShowError(message, caption, MessageBoxIcon.Error, ex)
    End Sub

    Public Shared Sub ShowError(ByVal message As String, ByVal icon As MessageBoxIcon, ByVal ex As Exception)
        ShowError(message, "", icon, ex)
    End Sub

    Public Shared Sub ShowError(ByVal message As String, ByVal caption As String, ByVal icon As MessageBoxIcon, ByVal ex As Exception)
        If caption Is Nothing OrElse caption.Length = 0 Then caption = "Error"
        If ex IsNot Nothing Then
            If (message Is Nothing OrElse message.Length = 0) Then
                message = ex.Message
            Else
                message &= vbCrLf & vbCrLf & "Error:" & vbCrLf & ex.Message
            End If
        End If
        MessageBox.Show(message, caption, MessageBoxButtons.OK, icon, MessageBoxDefaultButton.Button1)
        If ex IsNot Nothing Then LogError(ex) Else LogWarning(message)
    End Sub

    Public Shared Sub ShowError(ByVal message As String)
        ShowError(message, "")
    End Sub

    Public Shared Sub ShowError(ByVal message As String, ByVal caption As String)
        ShowError(message, caption, MessageBoxIcon.Error)
    End Sub

    Public Shared Sub ShowError(ByVal message As String, ByVal icon As MessageBoxIcon)
        ShowError(message, "", icon)
    End Sub

    Public Shared Sub ShowError(ByVal message As String, ByVal caption As String, ByVal icon As MessageBoxIcon)
        ShowError(message, caption, icon, Nothing)
    End Sub

    Public Shared Sub ShowWarning(ByVal message As String, ByVal caption As String, ByVal icon As MessageBoxIcon)
        If caption Is Nothing OrElse caption.Length = 0 Then caption = "Warning"
        MessageBox.Show(message, caption, MessageBoxButtons.OK, icon, MessageBoxDefaultButton.Button1)
        LogWarning(message)
    End Sub

    Public Shared Sub ShowWarning(ByVal message As String)
        ShowWarning(message, "")
    End Sub

    Public Shared Sub ShowWarning(ByVal message As String, ByVal caption As String)
        ShowWarning(message, caption, MessageBoxIcon.Warning)
    End Sub

    Public Shared Sub ShowWarning(ByVal message As String, ByVal icon As MessageBoxIcon)
        ShowWarning(message, "", icon)
    End Sub

    Public Shared Sub TriggerFatalError()
        ShowError("A fatal error has occured.  The application will now exit.")
        RaiseEvent FatalError()
    End Sub
#End Region

#Region " Other Methods "
    Protected Shared Sub LogString(ByVal message As String, Optional ByVal logEntryType As Diagnostics.EventLogEntryType = EventLogEntryType.Error)
        'Log the information to the Application Log & a text file

        Try
            Dim str As String = " - Memory: " & Format(GC.GetTotalMemory(False), "#,###")
            Debug.WriteLine(Now & str & " - " & message)
            If blnLogFileActive AndAlso objTxtWriter IsNot Nothing Then
                'write error to log file
                objTxtWriter.WriteLine(Now & str & " - " & message)
                objTxtWriter.WriteLine(vbCr)
            End If
            'Write error to Windows event log
            If blnSendToEventLog Then
                System.Diagnostics.EventLog.WriteEntry(System.AppDomain.CurrentDomain.FriendlyName, message, logEntryType)
            End If
            If blnWriteToConsole Then Console.WriteLine(Now & str & " - " & message)
        Catch ex As Exception
            ' Never call back into the logger here - LogError -> LogString -> here would
            ' recurse into a StackOverflow if the writer is persistently failing.
            Debug.WriteLine("Logging failed: " & ex.Message)
        End Try
    End Sub

    Protected Shared Sub StartLogFile(ByVal MaintainLastLog As Boolean)
        'setup text file writer - never let a logging failure crash the app.
        'The current directory can be read-only (e.g. when run from Program Files),
        'so fall back to the temp folder, and to no file logging at all, rather than
        'throwing out of the shared constructor (which would surface as a confusing
        'TypeInitializationException the first time any error is reported).
        Try
            strLogFilename = IO.Path.Combine(IO.Directory.GetCurrentDirectory(), "XmlEditorLog.txt")
            objTxtWriter = New IO.StreamWriter(strLogFilename, MaintainLastLog)
        Catch
            Try
                strLogFilename = IO.Path.Combine(IO.Path.GetTempPath(), "XmlEditorLog.txt")
                objTxtWriter = New IO.StreamWriter(strLogFilename, MaintainLastLog)
            Catch
                blnLogFileActive = False
                objTxtWriter = Nothing
                Return
            End Try
        End Try

        'write whatever shows in the debug window to the text file
        objTxtWriter.AutoFlush = True
        objTxtWriter.WriteLine(vbCr)
        objTxtWriter.WriteLine("******************************")
        objTxtWriter.WriteLine(vbCr)
        objTxtWriter.WriteLine(Now & " - Event Logging Started" & vbCrLf & vbTab & vbTab & vbTab _
        & "XML Editor Version: " & Application.ProductVersion.ToString & vbCrLf & vbTab & vbTab & vbTab _
        & "User Name:  " & My.User.Name & vbCrLf & vbTab & vbTab & vbTab _
        & "Computer Name:  " & My.Computer.Name & vbCrLf & vbTab & vbTab & vbTab _
        & "Network Connection:  " & My.Computer.Network.IsAvailable & vbCrLf & vbTab & vbTab & vbTab _
        & "Operating System:  " & My.Computer.Info.OSFullName)
        objTxtWriter.WriteLine(vbCr)
        objTxtWriter.WriteLine("******************************")
        objTxtWriter.WriteLine(vbCr)
        'If ClientSystemInfo.Platform = PlatformID.Win32NT Then blnSendToEventLog = True
    End Sub
#End Region

#Region " Constructors "
    Shared Sub New()
        StartLogFile(False)
    End Sub
#End Region

End Class
