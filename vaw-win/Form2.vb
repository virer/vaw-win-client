Module modINI
    Private Declare Unicode Function WritePrivateProfileString Lib "kernel32" _
    Alias "WritePrivateProfileStringW" (ByVal lpApplicationName As String, _
    ByVal lpKeyName As String, ByVal lpString As String, _
    ByVal lpFileName As String) As Int32

    Private Declare Unicode Function GetPrivateProfileString Lib "kernel32" _
    Alias "GetPrivateProfileStringW" (ByVal lpApplicationName As String, _
    ByVal lpKeyName As String, ByVal lpDefault As String, _
    ByVal lpReturnedString As String, ByVal nSize As Int32, _
    ByVal lpFileName As String) As Int32

    Public Sub writeIni(ByVal iniFileName As String, ByVal Section As String, ByVal ParamName As String, ByVal ParamVal As String)
        Dim Result As Integer = WritePrivateProfileString(Section, ParamName, ParamVal, iniFileName)
    End Sub

    Public Function ReadIni(ByVal IniFileName As String, ByVal Section As String, ByVal ParamName As String, ByVal ParamDefault As String) As String
        Dim ParamVal As String = Space$(1024)
        Dim LenParamVal As Long = GetPrivateProfileString(Section, ParamName, ParamDefault, ParamVal, Len(ParamVal), IniFileName)
        ReadIni = Left$(ParamVal, LenParamVal)
    End Function
End Module

Public Class Form2
    Dim File = Application.StartupPath + "\vaw.ini"
    Dim Section = "Settings"
    Dim host = "host"
    Dim port = "port"
    Dim vnchost = "vnchost"
    Dim vncport = "vncport"

    Function VAWReadIni()
        Form1.host = ReadIni(File, Section, host, "vaw.router.example.org")
        Form1.port = ReadIni(File, Section, port, "443")
        Form1.vnchost = ReadIni(File, Section, vnchost, "127.0.0.1")
        Form1.vncport = ReadIni(File, Section, vncport, "5900")
        Return True
    End Function

    Private Sub Form2_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        VAWReadIni()
        TextBox1.Text = Form1.host
        TextBox2.Text = Form1.port
        TextBox3.Text = Form1.vnchost
        TextBox4.Text = Form1.vncport
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        writeIni(File, Section, host, TextBox1.Text)
        writeIni(File, Section, port, TextBox2.Text)
        writeIni(File, Section, vnchost, TextBox3.Text)
        writeIni(File, Section, vncport, TextBox4.Text)
        Form1.host = TextBox1.Text
        Form1.port = TextBox2.Text
        Form1.vnchost = TextBox3.Text
        Form1.vncport = TextBox4.Text
        Me.Close()
    End Sub

End Class