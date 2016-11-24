Imports MsgType = Nexant.BillingAPI.BatchLogMsg.eType

Module Main

    Public Const APPLICATION_NAME As String = "LoadBudgetPayments"
    Public BatchTransTypeCode As String = String.Empty
    Public ComUser As User = Nothing
    Public ConnectionString As String = String.Empty
    Public sFileLocation As String = String.Empty
    Public strMessageText As String = String.Empty
    Public sSeperationSymbol As String = String.Empty
    Public MsgLog As Logger

    Sub Main()
        Dim wholeFile As String
        Dim lineData() As String
        Dim fieldData() As String
        Dim bProcessedFile As Boolean
        Dim sTRANS_DT As String = String.Empty
        Dim sLDC_ACCT_NO As String = String.Empty
        Dim sPROVIDER_REF As String = String.Empty
        Dim sPROVIDER_CODE As String = String.Empty
        Dim sTRANS_AM As String = String.Empty
        Dim sCANCEL_IN As String = String.Empty
        Dim sPAYMENT_REF As String = String.Empty
        Dim sTRANS_REF_NO_TX As String = String.Empty

        Dim oldCI As System.Globalization.CultureInfo =
            System.Threading.Thread.CurrentThread.CurrentCulture
        System.Threading.Thread.CurrentThread.CurrentCulture =
            New System.Globalization.CultureInfo("en-US")

        If Not Initialize() Then
            Functions.WriteToEventLog("Could not initialize " & APPLICATION_NAME & ".  Please check the configuration.", EventLogEntryType.Error)
            'Terminate active Com User connection
            If ComUser.IsActive Then ComUser.Dispose()
            Exit Sub
        End If

        MsgLog = New Logger

        MsgLog.WriteLog("Batch checkpoint: Beginning " & APPLICATION_NAME & " processing.", MsgType.Info)

        '----------------------------------------------------------------------------------------------------
        Try
            'sFileLocation contains path to where the csv files are
            Dim dir As New IO.DirectoryInfo(sFileLocation)
            'Only process *.csv files in the directory
            For Each file As IO.FileInfo In dir.GetFiles("*.csv")
                'Process each *.csv file 
                strMessageText = "Processing file: " & file.ToString
                MsgLog.WriteLog(strMessageText, 0)

                'Filelayout *.csv
                'HEADER:    codeline;DGO code;supplier code;file date;file time; DGO payref
                'BODY:      codeline;Code supplier;payment date;payment time; energy type; EAN; provider Ref;Provider Code;Amount;Cancel(Y/N);Payment Ref
                'FOOTER:    codeline;sum debit; sum credit; transaction count
                wholeFile = My.Computer.FileSystem.ReadAllText(sFileLocation & file.ToString)
                lineData = Split(wholeFile, vbNewLine)
                'first line is headerrecord
                bProcessedFile = True

                For Each lineOfText As String In lineData
                    'skip empty lines
                    If Not Trim(lineOfText) = "" Then
                        fieldData = lineOfText.Split(sSeperationSymbol)
                        If fieldData.Length = 11 Then 'Body Lines have 11 variables
                            'Body tttt
                            'fielddata(0)=codeline
                            'fielddata(1)=Code supplier
                            'fielddata(2)=payment Date
                            'fielddata(3)=payment time
                            'fielddata(4)=energy Type
                            'fielddata(5)=EAN
                            'fielddata(6)=provider Ref
                            'fielddata(7)=Provider Code
                            'fielddata(8)=Amount
                            'fielddata(9)=Cancel(Y / N)
                            'fielddata(10)=Payment Ref
                            sTRANS_DT = fieldData(2)
                            sLDC_ACCT_NO = fieldData(5)
                            sPROVIDER_REF = fieldData(6)
                            sPROVIDER_CODE = fieldData(7)
                            sTRANS_AM = fieldData(8)
                            sCANCEL_IN = fieldData(9)
                            sPAYMENT_REF = fieldData(10)
                            If Functions.RetrieveLDCAccountByAcctNo(sLDC_ACCT_NO) Then
                                If sCANCEL_IN = "0" Then
                                    If Functions.CreatePayment(sTRANS_REF_NO_TX, sTRANS_DT, sLDC_ACCT_NO, sPROVIDER_CODE, sPROVIDER_REF, sTRANS_AM, sPAYMENT_REF) Then
                                        If Not Functions.CreateUsage(sTRANS_REF_NO_TX, sTRANS_DT, sTRANS_AM) Then
                                            bProcessedFile = False
                                        End If
                                    Else
                                        bProcessedFile = False
                                    End If
                                Else
                                    If Not Functions.CancelPayment(sTRANS_DT, sLDC_ACCT_NO, sPROVIDER_CODE, sPROVIDER_REF, sTRANS_AM, sPAYMENT_REF) Then
                                        bProcessedFile = False
                                    End If
                                End If
                            Else
                                bProcessedFile = False
                            End If
                        Else
                            If fieldData.Length = 6 Then 'HEADER lines have 5 variables
                                'HEADER
                                'fielddata(0)=codeline
                                'fielddata(1)=DGO Code 
                                'fielddata(2)=Supplier Code
                                'fielddata(3)=File Date
                                'fielddata(4)=File Time
                                'fielddata(5)=DGO Pay Ref
                                'use the DGO pay Ref from the HEADER Record in the new transaction
                                sTRANS_REF_NO_TX = fieldData(5)
                            End If
                        End If
                    End If
                Next lineOfText

                'move file to Archive folder when file processed without errors
                Dim dirArchive As IO.DirectoryInfo
                If bProcessedFile Then
                    dirArchive = New IO.DirectoryInfo(dir.ToString & "Archive\")
                    If Not dirArchive.Exists Then
                        dirArchive.Create()
                    End If
                Else
                    dirArchive = New IO.DirectoryInfo(dir.ToString & "Error\")
                    If Not dirArchive.Exists Then
                        dirArchive.Create()
                    End If
                End If

                My.Computer.FileSystem.CopyFile(sFileLocation & file.ToString, dirArchive.ToString & file.ToString)
                My.Computer.FileSystem.DeleteFile(sFileLocation & file.ToString)

            Next 'for each file

        Catch ex As Exception
            'write batch message
            strMessageText = ex.Message
            MsgLog.WriteLog(strMessageText, 2)
        End Try
        '----------------------------------------------------------------------------------------------------
        MsgLog.WriteLog("Batch checkpoint: Finished " & APPLICATION_NAME & " processing.", MsgType.Info)

        MsgLog.Done()

        If ComUser.IsActive Then ComUser.Dispose()

    End Sub
    Function Initialize() As Boolean

        BatchTransTypeCode = My.Settings.BatchTransTypeCode
        If BatchTransTypeCode = String.Empty Then BatchTransTypeCode = "BATCH"

        '---------------------------------------------------------------------------------------------
        'Login to RevenueManager COM
        '---------------------------------------------------------------------------------------------
        Try
            ComUser = CoreManager.LoginEncrypted(My.Settings.ComLoginId, My.Settings.ComPassword)
        Catch ex As Exception
            Functions.WriteToEventLog("Error connecting to RevenueManager COM. " & ex.Message, EventLogEntryType.Error)
            Return False
        End Try

        '---------------------------------------------------------------------------------------------
        'Set SQL Server Connection String
        '---------------------------------------------------------------------------------------------
        ConnectionString = "Server=" & ComUser.DBServer &
                           ";Database=" & ComUser.DatabaseName

        If My.Settings.MSS_IntegratedSecurity Then
            ConnectionString = ConnectionString & ";Integrated Security=SSPI"
        Else
            ConnectionString = ConnectionString & ";User Id=" & My.Settings.MSS_UserId & ";Password=" & My.Settings.MSS_Password
        End If

        If My.Settings.MSS_Timeout = 0 Then
            ConnectionString = ConnectionString & ";"
        Else
            ConnectionString = ConnectionString & ";Connection Timeout=" & My.Settings.MSS_Timeout & ";"
        End If

        '---------------------------------------------------------------------------------------------
        'Set Input directory
        '---------------------------------------------------------------------------------------------
        If My.Settings.InputDirectory = String.Empty Then
            sFileLocation = "c:\temp\"
        Else
            sFileLocation = My.Settings.InputDirectory
        End If

        '---------------------------------------------------------------------------------------------
        'Set seperation symbol (default ;)
        '---------------------------------------------------------------------------------------------
        If My.Settings.SeperationSymbol = String.Empty Then
            sSeperationSymbol = ";"
        Else
            sSeperationSymbol = My.Settings.SeperationSymbol
        End If

        Return True

    End Function

End Module
