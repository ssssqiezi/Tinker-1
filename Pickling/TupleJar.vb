Namespace Pickling.Jars
    '''<summary>Pickles tuples of values as dictionaries keyed by jar name.</summary>
    Public Class TupleJar
        Inherits BaseJar(Of Dictionary(Of InvariantString, Object))
        Private ReadOnly _subJars As IList(Of IJar(Of Object))

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_subJars IsNot Nothing)
        End Sub

        Public Sub New(ByVal name As InvariantString, ByVal ParamArray subJars() As IJar(Of Object))
            MyBase.New(name)
            Contract.Requires(subJars IsNot Nothing)
            Me._subJars = subJars
        End Sub

        Public Overrides Function Pack(Of TValue As Dictionary(Of InvariantString, Object))(ByVal value As TValue) As IPickle(Of TValue)
            Contract.Assume(value IsNot Nothing)
            If value.Keys.Count > _subJars.Count Then Throw New PicklingException("Too many keys in dictionary")

            'Pack
            Dim pickles = New List(Of IPickle(Of Object))
            For Each subJar In _subJars
                Contract.Assume(subJar IsNot Nothing)
                If Not value.ContainsKey(subJar.Name) Then Throw New PicklingException("Key '{0}' missing from tuple dictionary.".Frmt(subJar.Name))
                Contract.Assume(value(subJar.Name) IsNot Nothing)
                pickles.Add(subJar.Pack(value(subJar.Name)))
            Next subJar
            Return New Pickle(Of TValue)(Me.Name, value, Concat(From p In pickles Select p.Data.ToArray).AsReadableList(), Function() Pickle(Of Object).MakeListDescription(pickles))
        End Function

        Public Overrides Function Parse(ByVal data As IReadableList(Of Byte)) As IPickle(Of Dictionary(Of InvariantString, Object))
            'Parse
            Dim vals = New Dictionary(Of InvariantString, Object)
            Dim pickles = New List(Of IPickle(Of Object))
            Dim curCount = data.Count
            Dim curOffset = 0
            For Each j In _subJars
                Contract.Assume(j IsNot Nothing)
                'Value
                Dim p = j.Parse(data.SubView(curOffset, curCount))
                vals(j.Name) = p.Value
                pickles.Add(p)
                'Size
                Dim n = p.Data.Count
                curCount -= n
                curOffset += n
                If curCount < 0 Then Throw New InvalidStateException("subJar lied about data used.")
            Next j

            Return New Pickle(Of Dictionary(Of InvariantString, Object))(Me.Name, vals, data.SubView(0, curOffset), Function() Pickle(Of Object).MakeListDescription(pickles))
        End Function
    End Class
End Namespace
