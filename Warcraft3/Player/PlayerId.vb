﻿Imports Tinker.Pickling

Namespace WC3
    ''' <summary>Stores a unique-per-game-per-instant player index in [1, 12].</summary>
    <DebuggerDisplay("{ToString}")>
    Public Structure PlayerId
        Implements IEquatable(Of PlayerId)

        Private ReadOnly _index As Byte

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_index >= 0)
            Contract.Invariant(_index < 12)
        End Sub

        <CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2233:OperationsShouldNotOverflow", MessageId:="index-1")>
        Public Sub New(ByVal index As Byte)
            Contract.Requires(index > 0)
            Contract.Requires(index <= 12)
            Me._index = index - CByte(1)
        End Sub

        Public ReadOnly Property Index As Byte
            Get
                Contract.Ensures(Contract.Result(Of Byte)() > 0)
                Contract.Ensures(Contract.Result(Of Byte)() <= 12)
                Return _index + CByte(1)
            End Get
        End Property

        Public Shared Operator =(ByVal value1 As PlayerId, ByVal value2 As PlayerId) As Boolean
            Return value1._index = value2._index
        End Operator
        Public Shared Operator <>(ByVal value1 As PlayerId, ByVal value2 As PlayerId) As Boolean
            Return Not value1 = value2
        End Operator

        Public Overrides Function GetHashCode() As Integer
            Return _index.GetHashCode
        End Function
        Public Overrides Function Equals(ByVal obj As Object) As Boolean
            If Not TypeOf obj Is PlayerId Then Return False
            Return Me = CType(obj, PlayerId)
        End Function
        Public Overloads Function Equals(ByVal other As PlayerId) As Boolean Implements IEquatable(Of PlayerId).Equals
            Return Me = other
        End Function

        Public Overrides Function ToString() As String
            Return Index.ToString(CultureInfo.InvariantCulture)
        End Function
    End Structure

    Public Class PlayerIdJar
        Inherits BaseJar(Of PlayerId)

        Public Sub New(ByVal name As String)
            MyBase.New(name)
        End Sub

        Public Overrides Function Pack(Of TValue As PlayerId)(ByVal value As TValue) As IPickle(Of TValue)
            Return New Pickle(Of TValue)(Name, value, {CType(value, PlayerId).Index}.ToReadableList)
        End Function
        Public Overrides Function Parse(ByVal data As IReadableList(Of Byte)) As IPickle(Of PlayerId)
            If data.Count < 1 Then Throw New PicklingNotEnoughDataException
            Dim datum = data.SubView(0, 1)
            If datum(0) < 1 OrElse datum(0) > 12 Then Throw New PicklingException("Invalid player id: {0}".Frmt(datum(0)))
            Dim value = New PlayerId(datum(0))
            Return New Pickle(Of PlayerId)(Name, value, datum)
        End Function
    End Class
End Namespace