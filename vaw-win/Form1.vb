Imports System
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json.JsonSerializer
Imports WebSocket4Net

Imports System.Runtime.InteropServices

Public Class Form1
    Private Const BYTES_TO_READ As Integer = 1023
    Private byteData(BYTES_TO_READ) As Byte
    Private byteSendData As Byte()

    Public Shared host As New String("vaw.router.example.org")
    Public Shared port As New String("1520")
    Public Shared vnchost As New String("127.0.0.1")
    Public Shared vncport As New String("5900")

    Private Shared message As New StringBuilder
    Private Shared vncConnected As Boolean = False

    ' The response from the remote device.
    Private Shared response As String = String.Empty

    ' Create a TCP/IP socket.
    Public Shared vncClient As New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    'Public Shared vncClient As System.Net.Sockets.TcpClient

    ' Create a websocket.
    Public Shared websock As New WebSocket("wss://" + host + ":" + port + "/", "binary", WebSocketVersion.DraftHybi00)

    Public Sub MainWebSock()
        ' read ini setting
        Form2.VAWReadIni()

        Dim client_id = "123654789"
        Dim client_pw = "AeF321"

        IDTextBox.Text = client_id
        PasswordTextBox.Text = client_pw

        Dim ipHostInfo As IPHostEntry = Dns.GetHostEntry(host)
        Dim ipAddress As IPAddress = ipHostInfo.AddressList(0)

        websock = New WebSocket("wss://" + ipAddress.ToString + ":" + port + "/client-" + client_id + "-" + client_pw, "base64", WebSocketVersion.Rfc6455)

        AddHandler websock.Closed, Sub() Disconnect()
        AddHandler websock.Error, Sub(s, e) wsocketError(s, e)
        AddHandler websock.Opened, Sub() wsocketConnected()

        ' Allow untrusted certificate
        websock.AllowUnstrustedCertificate = True

        ' Allow "bad"/self-signed certificate
        ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCertifications

        websock.Open()

        Return
    End Sub

    Function AcceptAllCertifications()
        Return True
    End Function

    Sub wsocketError(s, e)
        RemoveHandler websock.Error, AddressOf wsocketError
        Dim br As String = "" + Chr(10) + Chr(13) + Chr(10) + Chr(13)
        Dim msg As String = "                      VAW Server connection Error!"
        msg += br + " Please verify your connectivity, DNS resolution(if used) and/or firewall."
        msg += br + "VAW Router:" + host + port
        msg += br + DirectCast(e, SuperSocket.ClientEngine.ErrorEventArgs).Exception.Message
        MsgBox(msg)
    End Sub

    Sub wsocketConnected()
        RemoveHandler websock.Error, AddressOf wsocketError

        AddHandler websock.DataReceived, Sub(s, e) wsocketDataReceived(s, e)
        AddHandler websock.MessageReceived, Sub(s, e) wsocketMessageReceived(s, e)

        Me.ToolStripStatusLabel1.Text = "Connected."
        Console.WriteLine("Webocket connected to {0}:{1}", host, port)
    End Sub

    Sub wsocketMessageReceived(s, e)
        Dim client_b64 = DirectCast(e, WebSocket4Net.MessageReceivedEventArgs).Message
        Dim client_data = Convert.FromBase64CharArray(client_b64, 0, client_b64.Length)
        wsClientDataReceived(client_data)
    End Sub

    Sub wsocketDataReceived(s, e)
        'function used if the protocol is binary instead of base64
        Dim client_b64 = DirectCast(e, WebSocket4Net.DataReceivedEventArgs).Data
        Dim cArray(client_b64.Length) As Char
        Dim bArray() As Byte = client_b64.ToArray
        For i As Int16 = 0 To client_b64.Length
            cArray(i) = Convert.ToChar(bArray(i))
        Next

        Dim client_data = Convert.FromBase64CharArray(cArray, 0, cArray.Length)
        wsClientDataReceived(client_data)
    End Sub

    Sub wsClientDataReceived(client_data)
        Console.WriteLine("Websocket {0} bytes received", client_data.Length)
        Try
            If vncConnected Then
                vncSend(vncClient, client_data)
                'Dim sw As IO.StreamWriter
                'sw = New IO.StreamWriter(vncClient.GetStream)
                'sw.Write(client_data)
                'sw.Flush()
            Else
                Dim client_str = Encoding.UTF8.GetString(client_data)
                If (client_str.Substring(0, 1) = "{") Then
                    Dim client_json = Newtonsoft.Json.JsonConvert.DeserializeObject(Of JSON_result)(client_str)
                    If client_json.vnc = "connect" Then
                        Console.WriteLine("Connecting to VNC port...")
                        VNCConnection()
                    End If
                End If
            End If
        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try
    End Sub

    Sub wsocketDisconnect()
        Console.WriteLine("Websocket Disconnected.")
        websock.Close()
    End Sub

    Sub VNCConnection()
        ' read ini setting
        Form2.VAWReadIni()

        ' Connect to a remote device.
        ' Establish the remote endpoint for the socket.
        Try
            'Dim remoteEP As New IPEndPoint(Me.hostIPorDNS(vnchost), CInt(vncport))
            ' Connect the socket to the remote endpoint.
            vncClient = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            'vncClient = New System.Net.Sockets.TcpClient(Me.hostIPorDNS(vnchost).ToString, CInt(vncport))
            vncClient.BeginConnect(Me.hostIPorDNS(vnchost).ToString, CInt(vncport), New AsyncCallback(AddressOf vncConnectCallback), Nothing)
        Catch ex As Exception
            Me.DisconnectButton.Enabled = False
            Me.ConnectButton.Enabled = True
            Me.ToolStripStatusLabel1.Text = "VNC connection error"
            MsgBox("                      VAW VNC connection Error!" + Chr(10) + Chr(13) + Chr(10) + Chr(13) + " Please verify VNC is running." + Chr(10) + Chr(13) + Chr(10) + Chr(13) + "VAW VNC:" + vnchost + ":" + vncport + "")
            MsgBox(ex.ToString)

            Try
                Me.Disconnect()
            Catch
            End Try
            Return
        End Try

        Return
    End Sub

    Private Sub vncConnectCallback(ByVal ar As IAsyncResult)
        vncClient.EndConnect(ar)
        vncConnected = True

        Me.ToolStripStatusLabel1.Text = "VNC connected"
        Console.WriteLine("VNC connected.")

        vncClient.BeginReceive(byteData, 0, BYTES_TO_READ, SocketFlags.None, _
                                  New AsyncCallback(AddressOf vncReceiveCallback), vncClient)
    End Sub

    Sub vncReceiveCallback(ByVal ar As IAsyncResult)
        If vncClient.Connected Then

            ' Read data from the remote device.
            Dim bytesRead As Integer = vncClient.EndReceive(ar)

            ' There might be more data, so store the data received so far.
            'message.Append(Encoding.UTF7.GetString(byteData, 0, bytesRead))

            response = Convert.ToBase64String(byteData, 0, bytesRead)

            'response = message.ToString()

            message = New StringBuilder
            If Len(response) <> 0 Then
                websock.Send(response)
                Console.WriteLine("Received {0} bytes from VNC", bytesRead)
            End If

            ' Get the rest of the data.
            'Try
            ' vncClient.BeginReceive(byteData, Len(response) + 1, BYTES_TO_READ, 0, New AsyncCallback(AddressOf vncReceiveCallback), vncClient)
            'Catch
            vncClient.BeginReceive(byteData, 0, BYTES_TO_READ, 0, New AsyncCallback(AddressOf vncReceiveCallback), vncClient)
            'End Try
        End If

    End Sub 'ReceiveCallback

    Sub vncSend(ByVal client As Socket, ByVal data As Byte())
        byteSendData = data

        ' Begin sending the data to the remote device.
        client.BeginSend(byteSendData, 0, byteSendData.Length, 0, New AsyncCallback(AddressOf vncSendCallback), client)
    End Sub 'Send


    Sub vncSendCallback(ByVal ar As IAsyncResult)
        ' Retrieve the socket from the state object.
        Dim client As Socket = CType(ar.AsyncState, Socket)

        ' Complete sending the data to the remote device.
        Dim bytesSent As Integer = client.EndSend(ar)
        Console.WriteLine("Sent {0} bytes to VNC.", bytesSent)
    End Sub 'SendCallback

    Sub Disconnect()
        Try
            If vncConnected Then
                vncClient.Close()
            End If
        Catch
        End Try
        If websock.State = WebSocketState.Open Then
            wsocketDisconnect()
        End If
        Console.WriteLine("VNC Disconnected.")
        vncConnected = False
        DisconnectButton.Enabled = False
        ConnectButton.Enabled = True
        ToolStripStatusLabel1.Text = "Disconnected."
        IDTextBox.Text = ""
        PasswordTextBox.Text = ""
    End Sub

    Private Sub ConnectButton_Click(sender As Object, e As EventArgs) Handles ConnectButton.Click
        ConnectButton.Enabled = False
        DisconnectButton.Enabled = True
        Me.ToolStripStatusLabel1.Text = "Connecting..."
        MainWebSock()
    End Sub

    Private Sub DisconnectButton_Click(sender As Object, e As EventArgs) Handles DisconnectButton.Click
        Disconnect()
    End Sub

    Private Sub ExitToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ExitToolStripMenuItem.Click
        Me.Close()
    End Sub

    Private Sub ToolStripLabel1_Click(sender As Object, e As EventArgs) Handles ToolStripLabel1.Click
        AboutBox2.Show()
    End Sub

    Private Sub AdvancedToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AdvancedToolStripMenuItem.Click
        Form2.Show()
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Form2.VAWReadIni()
        System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = False

        Win32.AllocConsole()
        Console.WriteLine("VAW Debug Console:")
    End Sub

    Function base64_encode(text)
        Dim byt As Byte() = System.Text.Encoding.UTF8.GetBytes(text)
        Return Convert.ToBase64String(byt)
    End Function

    Function base64_decode(b64)
        Dim b As Byte() = Convert.FromBase64String(b64)
        Return System.Text.Encoding.UTF8.GetString(b)
    End Function

    Function hostIPorDNS(host As String) As IPAddress
        Try
            Dim ipAddr As IPAddress = IPAddress.Parse(host)
            Return ipAddr
        Catch ex As Exception
            Dim ipHostInfo As IPHostEntry = Dns.GetHostEntry(host)
            Dim ipAddr As IPAddress = ipHostInfo.AddressList(0)
            Return ipAddr
        End Try
        Throw New System.Exception("An exception has occurred.")
    End Function
End Class

Public Class JSON_result
    Public id As Integer
    Public pw As String
    Public vnc As String
End Class

' State object for reading client data asynchronously
Public Class StateObject
    ' Client  socket.
    Public workSocket As Socket = Nothing
    ' Size of receive buffer.
    Public Const BufferSize As Integer = 1024
    ' Receive buffer.
    Public buffer(BufferSize) As Byte
    ' Received data string.
    Public sb As New StringBuilder
End Class 'StateObject

Public Class Win32
    <DllImport("kernel32.dll")> Public Shared Function AllocConsole() As Boolean

    End Function
    <DllImport("kernel32.dll")> Public Shared Function FreeConsole() As Boolean

    End Function
End Class
