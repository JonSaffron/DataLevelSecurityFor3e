Imports System.CodeDom.Compiler
Imports System.Configuration
Imports System.IO
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Security
Imports System.Security.Permissions
Imports System.Security.Policy

<Serializable> Public MustInherit Class AppDomainFor3E
    Implements IDisposable

    Private _appDomain As AppDomain
    Private _copyOfAssembly As String

    Protected Sub New()
        Me._appDomain = CreateAppDomain()
    End Sub

    Private Function CreateAppDomain() As AppDomain
        Dim rootPath As String = ConfigurationManager.AppSettings("RootPath")
        Dim frameworkStaging As String = ConfigurationManager.AppSettings("FrameworkStaging")
        Dim pathToUtilities3EDirectory As String = Path.Combine(rootPath, "Utilities\Utilities3E")
        Dim pathToStagingDirectory As String = Path.Combine(rootPath, frameworkStaging)

        Dim ads As New AppDomainSetup()
        ads.ApplicationBase = rootPath
        ads.PrivateBinPath = String.Format("{0};{1}", pathToUtilities3EDirectory, pathToStagingDirectory)
        ads.PrivateBinPathProbe = "exclude application base path"
        ads.LoaderOptimization = LoaderOptimization.SingleDomain
        ads.DisallowBindingRedirects = True
        ads.ConfigurationFile = Path.Combine(pathToUtilities3EDirectory, "Utilities3e.exe.config")

        Dim securityEvidence As New Evidence()
        Call securityEvidence.AddHostEvidence(New Zone(SecurityZone.MyComputer))

        Dim ps As New PermissionSet(PermissionState.Unrestricted)

        Dim result As AppDomain = AppDomain.CreateDomain("CompileAndRun", securityEvidence, ads, ps)
        Me._copyOfAssembly = Path.Combine(pathToUtilities3EDirectory, Assembly.GetExecutingAssembly().GetName().Name) & ".dll"
        File.Copy(Assembly.GetExecutingAssembly().Location, Me._copyOfAssembly, True)

        Call result.SetData("PathToStagingDirectory", pathToStagingDirectory)
        Call result.SetData("PathToUtilities3EDirectory", pathToUtilities3EDirectory)
        Call result.SetData("IsVerboseOutputRequired", Program.IsVerboseOutputRequired)
        Call result.SetData("InSafeMode", Program.InSafeMode)
        Return result
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If Not IsDisposed Then
            Call AppDomain.Unload(Me._appDomain)
            Me._appDomain = Nothing
            Call File.Delete(Me._copyOfAssembly)
        End If
    End Sub

    Protected ReadOnly Property IsDisposed As Boolean
        Get
            Dim result = Me._appDomain Is Nothing
            Return result
        End Get
    End Property

    Protected Shared Function LoadFrameworkAssemblies() As Dictionary(Of String, String)
        Dim names As String() = {"OQL", "XOQL", "Rosetta", "QueryMgr", "LogMgr", "AssemblyMgr", _
            "ConfigurationMgr", "ObjectMgr", "XMLHelpers", "AppObjectMgr", "SharedQueryMgr", "ApplicationInterfaces", "ArchetypeMgr"}

        Dim result As New Dictionary(Of String, String)
        For Each assemblyName As String In names
            Call LoadAssembly(assemblyName, result)
        Next

        Return result
    End Function

    Protected Shared Sub LoadArchetypeAssembly(ByVal name As String, ByVal assemblyList As Dictionary(Of String, String))
        Dim pathToLibraries As String = CType(AppDomain.CurrentDomain.GetData("PathToStagingDirectory"), String)
        If Path.GetExtension(name) = "dll" Then name = Path.ChangeExtension(name, Nothing)

        Dim assemblyNameBase As String = String.Format("NextGen.Archetype.{0}Base", name)
        Dim fileNameBase As String = Path.Combine(pathToLibraries, assemblyNameBase) & ".dll"
        If File.Exists(fileNameBase) Then
            Call LoadAssembly(assemblyNameBase, assemblyList)
        End If

        Dim assemblyName As String = String.Format("NextGen.Archetype.{0}", name)
        Dim fileName As String = Path.Combine(pathToLibraries, assemblyName) & ".dll"
        If File.Exists(fileName) Then
            Call LoadAssembly(assemblyName, assemblyList)
            Exit Sub
        End If

        Dim msg As String = String.Format("Could not find an assembly for archetype {0}", name)
        Throw New InvalidOperationException(msg)
    End Sub

    Protected Shared Sub LoadAssembly(assemblyName As String, assemblyList As Dictionary(Of String, String))
        If IsVerboseOutputRequired Then Call Console.WriteLine("Loading " & assemblyName)

        Dim result As Assembly = Assembly.Load(assemblyName)
        If assemblyList.ContainsKey(result.FullName) Then Exit Sub
        Call assemblyList.Add(result.FullName, result.Location)

        For Each referencedAssemblyName In result.GetReferencedAssemblies()
            Call LoadAssembly(referencedAssemblyName, assemblyList)
        Next
    End Sub

    Private Shared Sub LoadAssembly(assemblyName As AssemblyName, assemblyList As Dictionary(Of String, String))
        If assemblyList.ContainsKey(assemblyName.FullName) Then Exit Sub

        If IsVerboseOutputRequired Then Call Console.WriteLine("Loading " & assemblyName.FullName)

        Dim result As Assembly
        result = Assembly.Load(assemblyName)
        If assemblyList.ContainsKey(result.FullName) Then Exit Sub
        Call assemblyList.Add(result.FullName, result.Location)
    End Sub

    Protected Shared Function BuildCodeAssembly(ByVal code As String, ByVal referencedAssemblies As Dictionary(Of String, String)) As Assembly
        Dim options As New CompilerParameters With {.GenerateInMemory = True, .GenerateExecutable = False, .IncludeDebugInformation = False}

        For Each item As KeyValuePair(Of String, String) In referencedAssemblies
            If Not String.IsNullOrWhiteSpace(item.Value) Then
                Call options.ReferencedAssemblies.Add(item.Value)
            End If
        Next

        Dim compileResults As CompilerResults
        Using provider As New VBCodeProvider()
            compileResults = provider.CompileAssemblyFromSource(options, New String() {code})
        End Using

        If Not compileResults.Errors.Cast(Of CompilerError).Any() Then
            Call Console.WriteLine("Compilation succeeded.")
            Return compileResults.CompiledAssembly
        End If

        Call Console.WriteLine("Compilation errors:")
        For Each ce As CompilerError In compileResults.Errors
            Call Console.WriteLine("- {0}", ce.ToString())
        Next

        Throw New CompileException()
    End Function

    Protected Shared Function GetQuerySession() As Object
        ' We need to use some 3e framework functionality. 
        ' Easiest thing to do is to use the same code that utilities 3e uses to get a QuerySession object.
        Dim pathToUtilities3EDirectory As String = CType(AppDomain.CurrentDomain.GetData("PathToUtilities3EDirectory"), String)
        Dim utilities As Assembly = Assembly.LoadFrom(Path.Combine(pathToUtilities3EDirectory, "Utilities3ECommon.dll"))
        Dim typeDatabase As Type = utilities.GetType("Utilities3ECommon.Database")
        Dim miGetQuerySession As MethodInfo = typeDatabase.GetMethod("GetQuerySession", BindingFlags.Public Or BindingFlags.Static, Nothing, New Type() {}, New ParameterModifier() {})
        If miGetQuerySession Is Nothing Then Throw New InvalidOperationException("Failed to get methodinfo on GetQuerySession() in Utilities3ECommon.Database object.")
        Dim result As Object = miGetQuerySession.Invoke(Nothing, New Object() {})
        Return result
    End Function

    Protected Sub DoCallBack(ByVal callBackDelegate As CrossAppDomainDelegate)
        Call ClearException()
        Call Me._appDomain.DoCallBack(callBackDelegate)
        Dim ex As Exception = Me.Exception
        If ex IsNot Nothing Then Throw ex
    End Sub

    Protected Function GetDomainData(Of T)(<CallerMemberName> Optional ByVal name As String = Nothing) As T
        Dim result As T = CType(Me._appDomain.GetData(name), T)
        Return result
    End Function

    Protected Sub SetDomainData(Of T)(<CallerMemberName> Optional ByVal name As String = Nothing, Optional ByVal data As T = Nothing)
        Call Me._appDomain.SetData(name, data)
    End Sub

    Protected Shared Function GetDataFromCurrentDomain(Of T)(<CallerMemberName> Optional ByVal name As String = Nothing) As T
        Dim result As T = CType(AppDomain.CurrentDomain.GetData(name), T)
        Return result
    End Function

    Protected Shared Sub SetDataForCurrentDomain(Of T)(<CallerMemberName> Optional ByVal name As String = Nothing, Optional ByVal data As T = Nothing)
        Call AppDomain.CurrentDomain.SetData(name, data)
    End Sub

    Private Sub ClearException()
        Call SetDomainData(Of Exception)("Exception", Nothing)
    End Sub

    Private ReadOnly Property Exception As Exception
        Get
            Return GetDomainData(Of Exception)()
        End Get
    End Property

    Protected Shared WriteOnly Property ExceptionToPassBack As Exception
        Set(value As Exception)
            Call SetDataForCurrentDomain(Of Exception)("Exception", value)
        End Set
    End Property

    Private Shared ReadOnly Property IsVerboseOutputRequired As Boolean
        Get
            Return GetDataFromCurrentDomain(Of Boolean)()
        End Get
    End Property

    Protected Shared ReadOnly Property InSafeMode As Boolean
        Get
            Return GetDataFromCurrentDomain(Of Boolean)()
        End Get
    End Property
End Class
