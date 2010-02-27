﻿Imports Tinker.Bnet

Namespace CKL
    '''<summary>Provides answers to bnet cd key authentication challenges, allowing clients to login to bnet once with the server's keys.</summary>
    Public NotInheritable Class Server
        Private Shared ReadOnly jar As New Bnet.Protocol.ProductCredentialsJar()
        Public Const PacketPrefixValue As Byte = 1

        Private ReadOnly inQueue As ICallQueue = New TaskedCallQueue
        Private ReadOnly outQueue As ICallQueue = New TaskedCallQueue

        Public ReadOnly name As InvariantString
        Private WithEvents _accepter As New ConnectionAccepter()
        Private ReadOnly _logger As New Logger()
        Private ReadOnly _keys As New AsyncViewableCollection(Of CKL.KeyEntry)(outQueue:=outQueue)
        Private ReadOnly _portHandle As PortPool.PortHandle
        Private ReadOnly _clock As IClock
        Private _keyIndex As Integer

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_accepter IsNot Nothing)
            Contract.Invariant(_logger IsNot Nothing)
            Contract.Invariant(_keys IsNot Nothing)
            Contract.Invariant(inQueue IsNot Nothing)
            Contract.Invariant(_portHandle IsNot Nothing)
            Contract.Invariant(_clock IsNot Nothing)
        End Sub

        'verification disabled due to stupid verifier (1.2.30118.5)
        <ContractVerification(False)>
        Public Sub New(ByVal name As InvariantString,
                       ByVal listenPort As PortPool.PortHandle,
                       ByVal clock As IClock)
            Contract.Assume(listenPort IsNot Nothing)
            Contract.Assume(clock IsNot Nothing)
            Me.name = name
            Me._clock = clock
            Me._portHandle = listenPort
            Me._accepter.OpenPort(listenPort.Port)
        End Sub

        Public ReadOnly Property Logger As Logger
            Get
                Contract.Ensures(Contract.Result(Of Logger)() IsNot Nothing)
                Return _logger
            End Get
        End Property

        Public Function QueueAddKey(ByVal keyName As InvariantString, ByVal cdKeyROC As String, ByVal cdKeyTFT As String) As IFuture
            Contract.Requires(cdKeyROC IsNot Nothing)
            Contract.Requires(cdKeyTFT IsNot Nothing)
            Contract.Ensures(Contract.Result(Of ifuture)() IsNot Nothing)
            Return inQueue.QueueAction(
                Sub()
                    If (From k In _keys Where k.Name = keyName).Any Then
                        Throw New InvalidOperationException("A key with the name '{0}' already exists.".Frmt(keyName))
                    End If
                    Dim key = New CKL.KeyEntry(keyName, cdKeyROC, cdKeyTFT)
                    _keys.Add(key)
                End Sub
            )
        End Function
        Public Function QueueRemoveKey(ByVal keyName As InvariantString) As IFuture
            Contract.Ensures(Contract.Result(Of ifuture)() IsNot Nothing)
            Return inQueue.QueueAction(
                Sub()
                    Dim key = (From k In _keys Where k.Name = keyName).FirstOrDefault
                    If key Is Nothing Then Throw New InvalidOperationException("No key found with the name '{0}'.".Frmt(keyName))
                    _keys.Remove(key)
                End Sub
            )
        End Function

        <ContractVerification(False)>
        Private Sub OnAcceptedConnection(ByVal sender As ConnectionAccepter,
                                         ByVal acceptedClient As Net.Sockets.TcpClient) Handles _accepter.AcceptedConnection
            Contract.Requires(sender IsNot Nothing)
            Contract.Requires(acceptedClient IsNot Nothing)
            Contract.Assume(acceptedClient.Client IsNot Nothing)
            Dim socket = New PacketSocket(stream:=acceptedClient.GetStream,
                                          localendpoint:=CType(acceptedClient.Client.LocalEndPoint, Net.IPEndPoint),
                                          remoteendpoint:=CType(acceptedClient.Client.RemoteEndPoint, Net.IPEndPoint),
                                          timeout:=10.Seconds,
                                          clock:=_clock,
                                          logger:=Me.logger)
            logger.Log("Connection from {0}.".Frmt(socket.Name), LogMessageType.Positive)

            AsyncProduceConsumeUntilError(
                producer:=AddressOf socket.AsyncReadPacket,
                consumer:=Function(packetData) inQueue.QueueAction(Sub() HandlePacket(socket, packetData)),
                errorHandler:=Sub(exception) Logger.Log("Error receiving from {0}: {1}".Frmt(socket.Name, exception.Message), LogMessageType.Problem)
            )
        End Sub
        <ContractVerification(False)>
        Private Sub HandlePacket(ByVal socket As PacketSocket, ByVal packetData As IReadableList(Of Byte))
            Contract.Requires(socket IsNot Nothing)
            Contract.Requires(packetData IsNot Nothing)
            Contract.Assume(packetData.Count >= 4)

            Dim flag = packetData(0)
            Dim id = packetData(0)
            Dim data = packetData.SubView(4)
            Dim responseData As IEnumerable(Of Byte) = Nothing
            Dim errorMessage As String = Nothing
            If flag <> PacketPrefixValue Then
                errorMessage = "Invalid header id."
            Else
                Select Case CType(id, CKLPacketId)
                    Case CKLPacketId.[Error]
                        'ignore
                    Case CKLPacketId.Keys
                        If _keys.Count <= 0 Then
                            errorMessage = "No keys to lend."
                        ElseIf data.Count <> 8 Then
                            errorMessage = "Invalid length. Require client token [4] + server token [4]."
                        Else
                            If _keyIndex >= _keys.Count Then _keyIndex = 0
                            Dim credentials = _keys(_keyIndex).GenerateCredentials(clientToken:=data.SubView(0, 4).ToUInt32,
                                                                                   serverToken:=data.SubView(4, 4).ToUInt32)
                            responseData = Concat(jar.Pack(credentials.AuthenticationROC).Data,
                                                  jar.Pack(credentials.AuthenticationTFT).Data)
                            Logger.Log("Provided key '{0}' to {1}".Frmt(_keys(_keyIndex).Name, socket.Name), LogMessageType.Positive)
                            _keyIndex += 1
                        End If
                    Case Else
                        errorMessage = "Invalid packet id."
                End Select
            End If

            If responseData IsNot Nothing Then
                socket.WritePacket({PacketPrefixValue, id}, responseData)
            End If
            If errorMessage IsNot Nothing Then
                Logger.Log("Error parsing data from client: " + errorMessage, LogMessageType.Negative)
                socket.WritePacket({PacketPrefixValue, CKLPacketId.[Error]}, System.Text.UTF8Encoding.UTF8.GetBytes(errorMessage))
            End If
        End Sub

        Public Sub [Stop]()
            _accepter.CloseAllPorts()
            If _portHandle IsNot Nothing Then _portHandle.Dispose()
        End Sub
    End Class
End Namespace
