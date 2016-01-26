Imports System.IO
Imports System.Reflection
Imports System.Xml

Public Class CompileAndRunDomain
    Inherits AppDomainFor3E

    Public Sub New()
        Call MyBase.New()
    End Sub

    ' ReSharper disable ParameterHidesMember
    Public Function CompileAndRun(ByVal archetypeName As String, ByVal codeFile As String, ByVal dependencies As IEnumerable(Of String)) As String
        If Me.IsDisposed Then Throw New ObjectDisposedException("CompileAndRunDomain")

        If archetypeName Is Nothing Then Throw New ArgumentNullException("archetypeName")
        If String.IsNullOrWhiteSpace(archetypeName) Then Throw New ArgumentException("Parameter must have a value.", "archetypeName")
        If codeFile Is Nothing Then Throw New ArgumentNullException("codeFile")
        If String.IsNullOrWhiteSpace(codeFile) Then Throw New ArgumentException("Parameter must have a value.", "codeFile")
        If dependencies Is Nothing Then dependencies = New String() {}

        Me.ArchetypeName = archetypeName
        Me.CodeFile = codeFile
        Me.Dependencies = dependencies.ToArray()
        Call Me.DoCallBack(AddressOf Internal.CompileAndRun)

        Dim result As String = Me.ResultingXoql
        Return result
    End Function
    ' ReSharper restore ParameterHidesMember

    ' ReSharper disable once ClassNeverInstantiated.Local - class should be declared shared but this is not possible in vb.net
    Private Class Internal
        ' ---- all the below code executes in the 3e app domain

        Public Shared Sub CompileAndRun()
            Try
                ' Load 3e framework assemblies
                Dim assembliesToReference As Dictionary(Of String, String) = LoadFrameworkAssemblies()

                ' Load archetype assembly and any dependencies specified
                Call LoadArchetypeAssembly(ArchetypeName, assembliesToReference)
                For Each assemblyName As String In Dependencies
                    Call LoadArchetypeAssembly(assemblyName, assembliesToReference)
                Next

                Dim code As String = LoadCodeFromFile(CodeFile)
                Dim resultingAssembly As Assembly = BuildCodeAssembly(code, assembliesToReference)
                ResultingXoql = ConvertQueryCodeToXoql(resultingAssembly)
            Catch ex As Exception
                ExceptionToPassBack = ex
            End Try
        End Sub

        Private Shared Function LoadCodeFromFile(ByVal filePath As String) As String
            Dim basePath As String = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
            If IsRunningInVisualStudio() Then
                basePath = Path.GetDirectoryName(Path.GetDirectoryName(basePath))
            End If

            Dim pathToCode As String = Path.Combine(basePath, filePath)
            Using sr As New StreamReader(pathToCode)
                Dim result As String = sr.ReadToEnd()
                Return result
            End Using
        End Function

        Private Shared Function ConvertQueryCodeToXoql(ByVal compiledAssembly As Assembly) As String
            Dim querySession As Object = GetQuerySession()

            ' Instantiate the object defined in the code fragment and invoke it to get the object it returns
            Dim o As Object = compiledAssembly.CreateInstance("MyMain")
            Dim t As Type = o.GetType()
            Dim mi As MethodInfo = t.GetMethod("MainFunction", BindingFlags.Public Or BindingFlags.Instance)
            If mi Is Nothing Then Throw New InvalidOperationException("Failed to get information on the MainFunction method expected to be in the code file.")
            Dim parameters As Object() = {querySession}
            Dim returnedObject As Object = mi.Invoke(o, parameters)

            ' Check that the object returned corresponds to an OQL select statement
            Dim typeISelect As Type = Type.GetType("NextGen.Framework.OQL.ISelect,OQL", True)
            If Not typeISelect.IsInstanceOfType(returnedObject) Then
                Throw New InvalidOperationException("The value returned from running the compiled code did not implement the ISelect interface and therefore cannot be used to build a security view.")
            End If

            ' Convert the select object to the equivalent XOQL 
            Using sw As New StringWriter()
                Using tw As New XmlTextWriter(sw)
                    tw.Formatting = Formatting.Indented

                    Dim typeQueryMgr As Type = Type.GetType("NextGen.Framework.Managers.QueryMgr.QueryMgr,QueryMgr", True)
                    Dim miWriteOql As MethodInfo = typeQueryMgr.GetMethod("WriteOQL", BindingFlags.Public Or BindingFlags.Static)
                    If miWriteOql Is Nothing Then Throw New InvalidOperationException("Failed to get information on the WriteOQL method expected to be found in the QueryMgr object.")

                    parameters = {tw, returnedObject, Nothing}
                    Call miWriteOql.Invoke(Nothing, parameters)
                End Using

                Dim result As String = sw.ToString()
                Call Console.WriteLine("Conversion to XOQL succeeded.")
                Return result
            End Using
        End Function

        Private Shared WriteOnly Property ResultingXoql As String
            Set(value As String)
                Call SetDataForCurrentDomain(Of String)(data:=value)
            End Set
        End Property

        Private Shared ReadOnly Property ArchetypeName As String
            Get
                Return GetDataFromCurrentDomain(Of String)()
            End Get
        End Property

        Private Shared ReadOnly Property Dependencies As String()
            Get
                Return GetDataFromCurrentDomain(Of String())()
            End Get
        End Property

        Private Shared ReadOnly Property CodeFile As String
            Get
                Return GetDataFromCurrentDomain(Of String)()
            End Get
        End Property
    End Class

    Private ReadOnly Property ResultingXoql As String
        Get
            Return GetDomainData(Of String)()
        End Get
    End Property

    Private WriteOnly Property ArchetypeName As String
        Set(value As String)
            Call SetDomainData(Of String)(data:=value)
        End Set
    End Property

    Private WriteOnly Property Dependencies As String()
        Set(value As String())
            Call SetDomainData(Of String())(data:=value)
        End Set
    End Property

    Private WriteOnly Property CodeFile As String
        Set(value As String)
            Call SetDomainData(Of String)(data:=value)
        End Set
    End Property
End Class
