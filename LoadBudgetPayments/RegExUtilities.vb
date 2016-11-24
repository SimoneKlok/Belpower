Imports System.Globalization
Imports System.Text.RegularExpressions

Public Class RegexUtilities

    Dim invalid As Boolean = False

    Public Function IsValidEmail(ByVal strIn As String) As Boolean
        invalid = False
        If String.IsNullOrEmpty(strIn) Then Return False

        ' Use IdnMapping class to convert Unicode domain names.
        strIn = Regex.Replace(strIn, "(@)(.+)$", AddressOf Me.DomainMapper)
        If invalid Then Return False

        ' Return true if strIn is in valid e-mail format. 
        Return Regex.IsMatch(strIn, _
               "^(?("")(""[^""]+?""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" + _
               "(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9]{2,17}))$",
               RegexOptions.IgnoreCase)
    End Function

    Private Function DomainMapper(ByVal match As Match) As String
        ' IdnMapping class with default property values. 
        Dim idn As New IdnMapping()

        Dim domainName As String = match.Groups(2).Value
        Try
            domainName = idn.GetAscii(domainName)
        Catch e As ArgumentException
            invalid = True
        End Try
        Return match.Groups(1).Value + domainName
    End Function

End Class
