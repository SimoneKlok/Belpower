Imports rmcore = Nexant.CoreAPI
Imports rmbilling = Nexant.BillingAPI
Imports System.Data.SqlClient
Imports MsgType = Nexant.BillingAPI.BatchLogMsg.eType

Public Class Functions
    Public Shared oUser As rmcore.User
    Public Shared oCustomers As rmbilling.CustomerList
    Public Shared oCustomer As rmbilling.Customer
    Public Shared oAccount As rmbilling.Account
    Public Shared oLDC As rmbilling.LdcAccount
    Public Shared oCustCrit As rmbilling.CustomerCriteria = Nothing
    Public Shared Function RetrieveLDCAccountByAcctNo(ByVal strLDCAcctno As String) As Boolean
        'Retrieve LDC Account based on the LDCacctno
        Try
            oCustCrit = rmbilling.Customer.GetCustomerCriteria()
            oCustCrit.LdcAccountNumber = strLDCAcctno

            oCustomers = rmbilling.Customer.GetCustomers(oCustCrit)

            If oCustomers.Count > 0 Then
                If oCustomers.Count = 1 Then
                    oAccount = oCustomers.Item(0).GetAccountList(0)
                    oLDC = oCustomers.Item(0).GetAccountList.Item(0).GetLDCAccounts.Item(0)
                Else
                    'write batch message
                    strMessageText = "Unique LDC account not found"
                    MsgLog.WriteLog(strMessageText, 2)
                    Return False
                End If
            Else
                'write batch message
                strMessageText = "LDC account not found EAN: " & strLDCAcctno
                MsgLog.WriteLog(strMessageText, 2)
                Return False
            End If

        Catch ex As Exception
            'write batch message
            strMessageText = "Error function RetrieveLDCAccountByAcctNo() " & ex.Message
            MsgLog.WriteLog(strMessageText, 2)
        End Try

        Return True

    End Function
    Public Shared Function CreatePayment(ByVal sTRANS_REF_NO_TX As String, ByVal sTRANS_DT As String, ByVal sLDC_ACCT_NO As String, ByVal sPROVIDER_CODE As String, ByVal sPROVIDER_REF As String, ByVal sTRANS_AM As String, ByVal sPAYMENT_REF As String) As Boolean
        Try
            Dim oPayment As rmbilling.Transaction

            oPayment = Nothing

            oPayment = oLDC.GetNewCredit()

            oPayment.ReferenceNo = sTRANS_REF_NO_TX
            oPayment.TransactionDate = DateTime.Parse(sTRANS_DT)
            oPayment.Generic1Date = DateTime.Parse(sTRANS_DT)
            oPayment.Generic2Date = DateTime.Parse(sTRANS_DT)
            oPayment.TransactionAmount = (Convert.ToDecimal(sTRANS_AM.Replace(",", ".")) * -1)
            oPayment.TransactionSubTypeCode = My.Settings.PaymentSubtype
            oPayment.ServiceType = "EL"
            oPayment.VendorDBNo = oAccount.VendorDBNo
            oPayment.Comment = "Budget Meter Payment. Ref.nr. : " & sPROVIDER_REF
            oPayment.SetUserFieldValue(6, sPROVIDER_REF)
            oPayment.SetUserFieldValue(7, sPROVIDER_CODE)
            oPayment.SetUserFieldValue(8, sPAYMENT_REF)

            oPayment.Save()

        Catch ex As Exception
            'write batch message
            strMessageText = "Error function CreatePayment() " & ex.Message
            MsgLog.WriteLog(strMessageText, 2)
            Return False
        End Try

        Return True

    End Function
    Public Shared Function CancelPayment(ByVal sTRANS_DT As String, ByVal sLDC_ACCT_NO As String, ByVal sPROVIDER_CODE As String, ByVal sPROVIDER_REF As String, ByVal sTRANS_AM As String, ByVal sPAYMENT_REF As String) As Boolean
        Dim oTranscrit As rmbilling.TransactionCriteria = Nothing

        oTranscrit = rmbilling.Transaction.GetTransactionCriteria

        'Betalingen van een aansluiting ophalen
        'Bepalen of er al een betaling is met dezelfde kenmerken 
        oTranscrit.AddTypeCode("PAY")
        oTranscrit.AddSubTypeCode("BMP")
        oTranscrit.Cancel = False
        oTranscrit.FromTransDate = DateTime.Parse(sTRANS_DT)
        oTranscrit.ToTransDate = DateTime.Parse(sTRANS_DT)
        oTranscrit.AddCustomField(6, sPROVIDER_REF)
        oTranscrit.AddCustomField(7, sPROVIDER_CODE)
        oTranscrit.AddCustomField(8, sPAYMENT_REF)

        Dim colTransactions As rmbilling.TransactionList = oLDC.GetTransactionsByCriteria(oTranscrit)

        'Indien ja dan cancelen
        If colTransactions.Count > 0 Then
            If colTransactions.Count = 1 Then
                'payment found.
                Dim otransaction As rmbilling.Transaction = colTransactions.Item(0)
                otransaction.Cancel(otransaction.OriginCode)
                otransaction.Save()
            Else
                'write batch message
                strMessageText = "Unique Payment not found for EAN: " & sLDC_ACCT_NO & " with the following criteria: " & "Date: " & sTRANS_DT & " Provider code: " & sPROVIDER_CODE & " Provider ref: " & sPROVIDER_REF & " Payment ref: " & sPAYMENT_REF
                MsgLog.WriteLog(strMessageText, 2)
                Return False
            End If
        Else
            'write batch message
            strMessageText = "Payment not found for EAN: " & sLDC_ACCT_NO & " with the following criteria: " & "Date: " & sTRANS_DT & " Provider code: " & sPROVIDER_CODE & " Provider ref: " & sPROVIDER_REF & " Payment ref: " & sPAYMENT_REF
            MsgLog.WriteLog(strMessageText, 2)
            Return False
        End If

        Return True
    End Function
    Public Shared Function CreateUsage(ByVal sTRANS_REF_NO_TX As String, ByVal sTRANS_DT As String, ByVal sTRANS_AM As String) As Boolean
        Try
            Dim oUsageNew As rmbilling.MonthlyUsage
            Dim dQuantity As Decimal

            dQuantity = (Convert.ToDecimal(sTRANS_AM.Replace(",", ".")) / 1.21)

            oUsageNew = Nothing

            oUsageNew = oLDC.GetNewMonthlyUsage
            oUsageNew.ServicePeriodBeginDate = DateTime.Parse(sTRANS_DT)
            oUsageNew.ServicePeriodEndDate = DateTime.Parse(sTRANS_DT)
            oUsageNew.QualifierCode = My.Settings.Qualifiercode
            oUsageNew.QuantityDeliveredUOMCode = My.Settings.UOMcode
            oUsageNew.BillMethodCode = "INBR"
            oUsageNew.ReferenceNumber = sTRANS_REF_NO_TX
            oUsageNew.MeasSignificanceCode = My.Settings.Significancecode
            oUsageNew.QuantityDelivered = Decimal.Round(dQuantity, 2)
            oUsageNew.IntervalFreqInMinutes = 0
            oUsageNew.HasIntervals = False

            oUsageNew.Save()

        Catch ex As Exception
            'write batch message
            strMessageText = "Error function CreateUsage() EAN Code :  " & oLDC.LDCAcctNo & " " & ex.Message
            MsgLog.WriteLog(strMessageText, 2)
            Return False
        End Try

        Return True

    End Function

    Public Shared Function GetUserFieldByName(ByVal relateObject As Object, ByVal userFieldName As String) As rmcore.UserFieldData

        GetUserFieldByName = Nothing

        Try
            Dim colUserFieldData As rmcore.UserFieldDataList = relateObject.GetUserFieldDataList(userFieldName)
            If colUserFieldData.Count = 1 Then
                'found UDF
                GetUserFieldByName = colUserFieldData.Item(0)
            End If
        Catch ex As Exception
            GetUserFieldByName = Nothing
        End Try

    End Function

    '--------------------------------------------------------------------------------------------------
    'The following function writes messages to the Windows Event Log
    '--------------------------------------------------------------------------------------------------
    Public Shared Function WriteToEventLog(ByVal entry As String,
                Optional ByVal eventType As EventLogEntryType = EventLogEntryType.Information) As Boolean

        Dim objEventLog As New EventLog

        Try

            'Register the Application as an Event Source
            If Not EventLog.SourceExists(APPLICATION_NAME) Then
                EventLog.CreateEventSource(APPLICATION_NAME, "Nexant")
            End If

            'log the entry
            objEventLog.Source = APPLICATION_NAME
            objEventLog.WriteEntry(entry, eventType)

            Return True

        Catch Ex As Exception
            MsgBox(Ex)
            Return False

        End Try

    End Function


End Class


