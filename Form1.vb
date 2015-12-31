Imports System
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports Newtonsoft.Json.JsonSerializer
Imports WebSocket4Net

Imports System.Runtime.InteropServices

Public Class Form1
    Public Shared host As New String("vaw.router.example.org")
    Public Shared port As New String("1520")
    Public Shared vnchost As New String("127.0.0.1")
    Public Shared vncport As New String("5900")

    ' ManualResetEvent instances signal completion.
    Private Shared connectDone As New ManualResetEvent(False)
    Private Shared sendDone As New ManualResetEvent(False)
    Private Shared receiveDone As New ManualResetEvent(False)

    ' The response from the remote device.
    Private Shared response As String = String.Empty

    ' Create a TCP/IP socket.
    Public Shared sender As New Socket(AddressFamily.InterNetwork, _
        SocketType.Stream, ProtocolType.Tcp)

    ' Create a websocket.
    Public Shared websock As New WebSocket("wss://" + host + ":" + port + "/", "base64", WebSocketVersion.DraftHybi10)

    Public Sub MainWebSock()
        ' read ini setting
        Form2.VAWReadIni()

        Dim ipHostInfo As IPHostEntry = Dns.GetHostEntry(host)
        Dim ipAddress As IPAddress = ipHostInfo.AddressList(0)

        websock = New WebSocket("wss://" + ipAddress.ToString + ":" + port + "/", "base64", WebSocketVersion.DraftHybi10)

        ' Allow untrusted certificate
        websock.AllowUnstrustedCertificate = True

        ' Allow "bad"/self-signed certificate
        ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCertifications

        AddHandler websock.Error, Sub(s, e) wsocketError(s, e)
        AddHandler websock.DataReceived, Sub(s, e) wsocketDataReceived(s, e)
        AddHandler websock.MessageReceived, Sub(s, e) wsocketMessageReceived(s, e)
        AddHandler websock.Opened, Sub(s, e) wsocketConnected(websock)
        
        websock.Open()
    End Sub

    Function AcceptAllCertifications()
        Return True
    End Function

    Sub wsocketError(s, e)
        MsgBox(DirectCast(e, SuperSocket.ClientEngine.ErrorEventArgs).Exception.Message)
        MsgBox("                      VAW Server connection Error!" + Chr(10) + Chr(13) + Chr(10) + Chr(13) + " Please verify your connectivity, DNS resolution(if used) and/or firewall." + Chr(10) + Chr(13) + Chr(10) + Chr(13) + "VAW Router:" + host + port + "")
    End Sub

    Sub wsocketConnected(websock)
        websock.Send(base64_encode("{ " + Chr(34) + "method" + Chr(34) + ": " + Chr(34) + "client" + Chr(34) + " }"))
        Me.ToolStripStatusLabel1.Text = "Connected."
        Console.WriteLine("Webocket connected to {0}:{1}", host, port)
    End Sub

    Sub wsocketMessageReceived(s, e)
        Dim client_b64 = DirectCast(e, WebSocket4Net.MessageReceivedEventArgs).Message
        Dim client_data = base64_decode(client_b64).ToString
        ClientDataReceived(client_data)
    End Sub

    Sub wsocketDataReceived(s, e)
        'function used if the protol is binary instead of base64
        MsgBox(DirectCast(e, WebSocket4Net.DataReceivedEventArgs).Data)
    End Sub

    Public Sub ClientDataReceived(client_data)
        Console.WriteLine(client_data)
        Try
            If (client_data.Substring(0, 1) = "{") Then
                Dim client_json = Newtonsoft.Json.JsonConvert.DeserializeObject(Of JSON_result)(client_data)
                If client_json.id Then
                    IDTextBox.Text = client_json.id
                    PasswordTextBox.Text = client_json.pw
                    VNCConnection()
                ElseIf client_json.vnc = "connect" Then
                    'VNCConnection()
                End If
            ElseIf sender.Connected Then
                Console.WriteLine("Websocket data to {0}", client_data.ToString)
                Send(sender, client_data)
            End If
        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try
    End Sub

    Sub socketDisconnect()
        websock.Close()
    End Sub

    Public Shared Sub VNCConnection()
        ' read ini setting
        Form2.VAWReadIni()

        ' Data buffer for incoming data.
        Dim bytes(1024) As Byte

        ' Connect to a remote device.
        ' Establish the remote endpoint for the socket.
        Try
            Dim remoteEP As New IPEndPoint(Form1.hostIPorDNS(vnchost), CInt(vncport))
            ' Connect the socket to the remote endpoint.
            sender.ReceiveBufferSize = 1024
            sender.BeginConnect(remoteEP, New AsyncCallback(AddressOf ConnectCallback), sender)
            ' Wait for connect.
            connectDone.WaitOne()

            ' Receive the response from the remote device.
            Receive(sender)
            receiveDone.WaitOne()

        Catch ex As Exception
            Form1.DisconnectButton.Enabled = False
            Form1.ConnectButton.Enabled = True
            Form1.ToolStripStatusLabel1.Text = "VNC connection error"
            MsgBox("                      VAW VNC connection Error!" + Chr(10) + Chr(13) + Chr(10) + Chr(13) + " Please verify your connectivity, DNS resolution(if used), your firewall or verify VNC is running." + Chr(10) + Chr(13) + Chr(10) + Chr(13) + "VAW VNC:" + vnchost + ":" + vncport + "")
            MsgBox(ex.ToString)
            Try
                sender.Shutdown(SocketShutdown.Both)
            Catch
                Dim unun = 1
            End Try
            sender.Close()

            ' Create a new TCP/IP socket.
            sender = New Socket(AddressFamily.InterNetwork, _
                SocketType.Stream, ProtocolType.Tcp)
            Return
        End Try
    End Sub

    Private Shared Sub ConnectCallback(ByVal ar As IAsyncResult)
        ' Retrieve the socket from the state object.
        Dim client As Socket = CType(ar.AsyncState, Socket)

        ' Complete the connection.
        client.EndConnect(ar)
        Form1.ToolStripStatusLabel1.Text = "VNC connected"
        Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString())

        ' Signal that the connection has been made.
        connectDone.Set()
    End Sub 'ConnectCallback

    Private Shared Sub Receive(ByVal client As Socket)

        ' Create the state object.
        Dim state As New StateObject
        state.workSocket = client

        ' Begin receiving the data from the remote device.
        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, New AsyncCallback(AddressOf ReceiveCallback), state)
    End Sub 'Receive

    Private Shared Sub Send(ByVal client As Socket, ByVal data As String)
        ' Convert the string data to byte data using ASCII encoding.
        Dim byteData As Byte() = Encoding.ASCII.GetBytes(data)

        ' Begin sending the data to the remote device.
        client.BeginSend(byteData, 0, byteData.Length, 0, New AsyncCallback(AddressOf SendCallback), client)
    End Sub 'Send


    Private Shared Sub SendCallback(ByVal ar As IAsyncResult)
        ' Retrieve the socket from the state object.
        Dim client As Socket = CType(ar.AsyncState, Socket)

        ' Complete sending the data to the remote device.
        Dim bytesSent As Integer = client.EndSend(ar)
        Console.WriteLine("Sent {0} bytes to server.", bytesSent)

        ' Signal that all bytes have been sent.
        sendDone.Set()
    End Sub 'SendCallback

    Private Shared Sub ReceiveCallback(ByVal ar As IAsyncResult)

        ' Retrieve the state object and the client socket 
        ' from the asynchronous state object.
        Dim state As StateObject = CType(ar.AsyncState, StateObject)
        Dim client As Socket = state.workSocket


        ' Read data from the remote device.
        Dim bytesRead As Integer = client.EndReceive(ar)

        If bytesRead > 0 Then
            ' There might be more data, so store the data received so far.
            state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead))
            Console.WriteLine("Received data from VNC:{0}", state.sb.ToString)
            ' Get the rest of the data.
            client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, New AsyncCallback(AddressOf ReceiveCallback), state)

            ' FIXME TODO
            response = state.sb.ToString()
            Console.WriteLine("VNC data sent1:{0}", response)
            websock.Send(Form1.base64_encode(response))
        Else
            ' All the data has arrived; put it in response.
            If state.sb.Length > 1 Then
                response = state.sb.ToString()
                Console.WriteLine("VNC data sent2:{0}", response)
                websock.Send(Form1.base64_encode(response))
            End If
            ' Signal that all bytes have been received.
            receiveDone.Set()
        End If
    End Sub 'ReceiveCallback

    Sub Disconnect()
        ' Release the socket.
        Try
            sender.Shutdown(SocketShutdown.Both)
        Finally
            sender.Close()
        End Try

        ' Create a new TCP/IP socket.
        sender = New Socket(AddressFamily.InterNetwork, _
            SocketType.Stream, ProtocolType.Tcp)
    End Sub

    Private Sub ConnectButton_Click(sender As Object, e As EventArgs) Handles ConnectButton.Click
        ConnectButton.Enabled = False
        DisconnectButton.Enabled = True
        Me.ToolStripStatusLabel1.Text = "Connecting..."
        'Main()
        MainWebSock()
    End Sub

    Private Sub DisconnectButton_Click(sender As Object, e As EventArgs) Handles DisconnectButton.Click
        socketDisconnect()
        Disconnect()
        DisconnectButton.Enabled = False
        ConnectButton.Enabled = True
        ToolStripStatusLabel1.Text = "Disconnected."
        IDTextBox.Text = ""
        PasswordTextBox.Text = ""
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

' State object for receiving data from remote device.
Public Class StateObject
    ' Client socket.
    Public workSocket As Socket = Nothing
    ' Size of receive buffer.
    Public Const BufferSize As Integer = 256
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
