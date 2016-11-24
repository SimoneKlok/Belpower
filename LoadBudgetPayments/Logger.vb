Imports System.IO
Imports System
Imports CoreApi = Nexant.CoreAPI
Imports BillingAPI = Nexant.BillingAPI


''' <summary>
''' A utility class wrapping the RM Batch and BatchLogMsg classes.
''' </summary>
Public NotInheritable Class Logger
#Region "Public Methods"

    Public Sub New()
        BatchLog = BillingAPI.Batch.GetNewBatch()

        processName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name

        Dim sKey As String = String.Empty
        Try
            sKey = keyTraceOutput
            isDebugOutputOn = Convert.ToBoolean(My.Settings(sKey))

            sKey = keyExtendedLogging
            isExtendedLoggingOn = Convert.ToBoolean(My.Settings(sKey))
        Catch
            OutputStartupError((Convert.ToString("config file entry for key: ") & sKey) + " is INVALID!")
            Throw
        End Try
    End Sub

    Protected Overrides Sub Finalize()
        Try
            BatchLog.SetDone()
            BatchLog = Nothing
        Finally
            MyBase.Finalize()
        End Try
    End Sub

    Public Sub Done()
        BatchLog.SetDone()
    End Sub

    Public Sub WriteLog(ByVal sMessage As String, ByVal eSeverity As BillingAPI.BatchLogMsg.eType)
        WriteLog(sMessage, eSeverity, "", 0)
    End Sub

    Public Sub WriteLog(ByVal sMessage As String, ByVal eSeverity As BillingAPI.BatchLogMsg.eType, ByVal sRelateClassName As String, ByVal sRelateId As UInt64)
        WriteLog(sMessage, eSeverity, sRelateClassName, sRelateId, "")
    End Sub

    Public Sub WriteLog(ByVal sMessage As String, ByVal eSeverity As BillingAPI.BatchLogMsg.eType, ByVal sRelateClassName As String, ByVal nRelateId As UInt64, ByVal sRMAcctNumber As String)
        Dim sLine As String = String.Format("{0}: '{1}'", processName, sMessage)

        Dim oMsg As BillingAPI.BatchLogMsg = BatchLog.GetNewBatchLogMsg()
        oMsg.StatusCode = statusCodeOpen
        oMsg.TransType = transTypeCode
        oMsg.LoggingType = eSeverity
        oMsg.MsgText = sLine

        If sRMAcctNumber <> String.Empty Then
            oMsg.AccountNumber = sRMAcctNumber
        End If

        If sRelateClassName <> String.Empty Then
            oMsg.RelateClassName = sRelateClassName
            oMsg.RelateId = nRelateId
        End If

        oMsg.Save()

        If isDebugOutputOn = True Then
            WriteConsoleLine(sLine)
        End If
    End Sub

    ''' <summary>
    '''   Write the passed info to the log ONLY if "extended logging" is turned on
    '''   in the config file. Extended logging is almost by definition "info"
    '''   level only.
    ''' </summary>
    ''' <param name="sMessage">msg text to log</param>
    Public Sub WriteExtendedLog(ByVal sMessage As String)
        If isExtendedLoggingOn Then
            WriteLog(sMessage, BillingAPI.BatchLogMsg.eType.Info)
        End If
    End Sub

    ''' <summary>
    '''   takes params similar to string.Format(), which this method directly
    '''   calls with the passed params;
    ''' </summary>
    ''' <param name="sFormat">format string suitable for passing to string.Format</param>
    ''' <param name="values">replaceable param values suitable for passing to string.Format</param>
    Public Sub WriteExtendedLog(ByVal sFormat As String, ByVal ParamArray values As Object())
        Dim sResult As String = String.Format(sFormat, values)
        WriteExtendedLog(sResult)
    End Sub

    ''' <summary>
    ''' Write a string out to the console and to debugger output. The m_bDebug switch
    ''' is set from the XML config file, but defaults to 'false'.
    ''' </summary>
    ''' <param name="sMsg">the text to output</param>
    Public Sub WriteConsoleLine(ByVal sMsg As String)
        If isDebugOutputOn Then
            Console.WriteLine(sMsg)
            System.Diagnostics.Trace.WriteLine(sMsg)
        End If
    End Sub

    ''' <summary>
    ''' This static method is for startup code (reading the param file, logging in) to
    ''' use before the ErrorLog instance is created (the ErrorLog is now based on
    ''' the RM BatchLog, thus the app needs to be logged into the RM/HD infrastructure
    ''' before it can create an ErrorLog). This method is a static version of
    ''' WriteConsoleLine().
    ''' </summary>
    ''' <param name="sMsg">the text to output</param>
    Public Shared Sub OutputStartupError(ByVal sMsg As String)
        Console.WriteLine(sMsg)
        System.Diagnostics.Trace.WriteLine(sMsg)
    End Sub

#End Region

#Region "Private Properties"

    Private Property BatchLog() As BillingAPI.Batch
        Get
            Return m_Batch
        End Get
        Set(ByVal value As BillingAPI.Batch)
            m_Batch = value
        End Set
    End Property

    Private Property isExtendedLoggingOn() As Boolean
        Get
            Return m_bExtendedLogging
        End Get
        Set(ByVal value As Boolean)
            m_bExtendedLogging = value
        End Set
    End Property

    Private Property isDebugOutputOn() As Boolean
        Get
            Return m_bDebugOutput
        End Get
        Set(ByVal value As Boolean)
            m_bDebugOutput = value
        End Set
    End Property

    Private ReadOnly Property keyExtendedLogging() As String
        Get
            Return m_sKeyExtendedLogging
        End Get
    End Property

    Private ReadOnly Property keyTraceOutput() As String
        Get
            Return m_sKeyTraceOutput
        End Get
    End Property

    ' the app's process name, just for putting into the log messages
    Private Property processName() As String
        Get
            Return m_sProcessName
        End Get
        Set(ByVal value As String)
            m_sProcessName = value
            m_sProcessName = m_sProcessName.Replace(".exe", "")
        End Set
    End Property

    Private ReadOnly Property statusCodeClosed() As String
        Get
            Return m_sStatusCodeClosed
        End Get
    End Property

    Private ReadOnly Property statusCodeOpen() As String
        Get
            Return m_sStatusCodeOpen
        End Get
    End Property

    Private ReadOnly Property transTypeCode() As String
        Get
            '            return CoreApi.ReferenceCode.GetReferenceCode( "LOG MESSAGE STATE", "" ).Code;
            Return m_sTransTypeCode
        End Get
    End Property

#End Region

#Region "Data members"

    ' this flag indicates whether to also output to the Console screen and debugger
    ' output, in addition to the Batch Log
    Private m_bDebugOutput As Boolean = False
    Private m_bExtendedLogging As Boolean = False
    Private m_sProcessName As String = ""

    ' the RM Batch for this log session (i.e., this run of the app)
    Private m_Batch As BillingAPI.Batch = Nothing

    ' key(s) for the config file
    Private Const m_sKeyExtendedLogging As String = "ExtendedLogging"
    Private Const m_sKeyTraceOutput As String = "TraceOutput"

    ' some string constants;
    ' the Status codes are actually ref codes in the LOG MESSAGE STATE ref domian,
    ' but I don't see any way to get them from XRefMgr, at least not any way that's
    ' better than hard-coding them here

    Private Const m_sStatusCodeClosed As String = "CLSD"
    Private Const m_sStatusCodeOpen As String = "OPEN"
    Private Const m_sTransTypeCode As String = "CDT"

#End Region

End Class
' end class ErrorLog


'=======================================================
'Service provided by Telerik (www.telerik.com)
'Conversion powered by NRefactory.
'Twitter: @telerik
'Facebook: facebook.com/telerik
'=======================================================
