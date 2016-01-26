Imports System.Reflection

<Serializable> Public Class ApplyNewSecurity
    Inherits AppDomainFor3E

    Public Sub New(ByVal listOfRoles As IEnumerable(Of String), ByVal listOfArchetypes As IEnumerable(Of String))
        Call MyBase.New()

        Me.ListOfRoleNames = listOfRoles.ToArray()
        Me.ListOfArchetypeNames = listOfArchetypes.ToArray()
        Call Me.DoCallBack(AddressOf Internal.Initialise)
    End Sub

    Public Sub ClearSecurityView(ByVal role As String, ByVal archetype As String)
        If Me.IsDisposed Then Throw New ObjectDisposedException("SaveAndApplySecurityView")

        If role Is Nothing Then Throw New ArgumentNullException("role")
        If String.IsNullOrWhiteSpace(role) Then Throw New ArgumentException("role")
        If archetype Is Nothing Then Throw New ArgumentNullException("archetype")
        If String.IsNullOrWhiteSpace(archetype) Then Throw New ArgumentException("archetype")

        Me.RoleName = role
        Me.ArchetypeName = archetype
        Me.Xoql = Nothing
        Me.ClearSecurity = True
        Call Me.DoCallBack(AddressOf Internal.SetAndApplySecurityView)
    End Sub

    Public Sub SaveAndApplySecurityView(ByVal role As String, ByVal archetype As String, ByVal xoqlQuery As String)
        If Me.IsDisposed Then Throw New ObjectDisposedException("SaveAndApplySecurityView")

        If role Is Nothing Then Throw New ArgumentNullException("role")
        If String.IsNullOrWhiteSpace(role) Then Throw New ArgumentException("role")
        If archetype Is Nothing Then Throw New ArgumentNullException("archetype")
        If String.IsNullOrWhiteSpace(archetype) Then Throw New ArgumentException("archetype")
        If xoqlQuery Is Nothing Then Throw New ArgumentNullException("xoqlQuery")
        If String.IsNullOrWhiteSpace(xoqlQuery) Then Throw New ArgumentException("xoqlQuery")

        Me.RoleName = role
        Me.ArchetypeName = archetype
        Me.Xoql = xoqlQuery
        Me.ClearSecurity = False
        Call Me.DoCallBack(AddressOf Internal.SetAndApplySecurityView)
    End Sub

    ' ReSharper disable once ClassNeverInstantiated.Local - class should be declared shared but this is not possible in vb.net
    Private Class Internal
        ' ---- all the below code executes in the 3e app domain

        Public Shared Sub Initialise()
            Try
                Dim internalAssembly As Assembly = CompileInternalOqlCode()
                Dim querySession As Object = GetQuerySession()

                Dim dbRoles = GetDbRoles(internalAssembly, querySession, ListOfRoleNames)
                Dim missingDbRole As String = ListOfRoleNames.FirstOrDefault(Function(name) Not dbRoles.ContainsKey(name))
                If missingDbRole IsNot Nothing Then Throw New InvalidOperationException("The DB role with the name " + missingDbRole + " was not found in the NxSecurityDbRole table.")

                Dim archetypes = GetArchetypes(internalAssembly, querySession, ListOfArchetypeNames)
                Dim missingArchetype As String = ListOfArchetypeNames.FirstOrDefault(Function(name) Not archetypes.ContainsKey(name))
                If missingArchetype IsNot Nothing Then Throw New InvalidOperationException("The archetype with the name " + missingArchetype + " was not found.")

                RoleGuids = dbRoles
                ArchetypeGuids = archetypes
                AssemblyName = internalAssembly.GetName().Name
            Catch ex As Exception
                ExceptionToPassBack = ex
            End Try
        End Sub

        Private Shared Function CompileInternalOqlCode() As Assembly
            Dim code As String = My.Resources.InternalOqlCode

            Dim referencedAssemblies As Dictionary(Of String, String) = LoadFrameworkAssemblies()
            Call LoadArchetypeAssembly("NxSecurityDBRole", referencedAssemblies)
            Call LoadArchetypeAssembly("NxSecuritySecuredArch", referencedAssemblies)
            Dim result As Assembly = BuildCodeAssembly(code, referencedAssemblies)

            Call LoadAssembly("OwnedObjectMgr", referencedAssemblies)       ' This is needed by SaveAndApplySecuritySettings
            Return result
        End Function

        Private Shared Function GetDbRoles(ByVal internalAssembly As Assembly, ByVal querySession As Object, ByVal listOfRoles As IEnumerable(Of String)) As Dictionary(Of String, Guid)
            Dim t As Type = internalAssembly.GetType("InternalOql")
            Dim mi As MethodInfo = t.GetMethod("GetDbRoles", BindingFlags.Public Or BindingFlags.Static)
            If mi Is Nothing Then Throw New InvalidOperationException("Failed to get methodinfo on GetDbRoles.")
            Dim parameters As Object() = {querySession, listOfRoles}
            Dim result = CType(mi.Invoke(Nothing, parameters), Dictionary(Of String, Guid))
            Return result
        End Function

        Private Shared Function GetArchetypes(ByVal internalAssembly As Assembly, ByVal querySession As Object, ByVal listOfArchetypes As IEnumerable(Of String)) As Dictionary(Of String, Guid)
            Dim t As Type = internalAssembly.GetType("InternalOql")
            Dim mi As MethodInfo = t.GetMethod("GetArchetypes", BindingFlags.Public Or BindingFlags.Static)
            If mi Is Nothing Then Throw New InvalidOperationException("Failed to get methodinfo on GetArchetypes.")
            Dim parameters As Object() = {querySession, listOfArchetypes}
            Dim result = CType(mi.Invoke(Nothing, parameters), Dictionary(Of String, Guid))
            Return result
        End Function

        Public Shared Sub SetAndApplySecurityView()
            ' ReSharper disable InconsistentNaming
            Const nxDocumentType_Archetype As Integer = 1
            Const isCustom_True As Boolean = True
            ' ReSharper restore InconsistentNaming
            Dim parameters As Object()

            Try
                Dim querySession As Object = GetQuerySession()

                Dim dbRoleGuid As Guid = RoleGuids(RoleName)
                Dim archetypeGuid As Guid = ArchetypeGuids(ArchetypeName)

                Dim internalOqlAssembly As Assembly = AppDomain.CurrentDomain.GetAssemblies().Single(Function(a) a.GetName().Name = AssemblyName)
                Dim t As Type = internalOqlAssembly.GetType("InternalOql", True)
                Dim mi As MethodInfo
                If ClearSecurity Then
                    ' Delete the current entry in NxSecuritySecuredArch
                    mi = t.GetMethod("DeleteSecuredArchetype", BindingFlags.Public Or BindingFlags.Static)
                    If mi Is Nothing Then Throw New InvalidOperationException("Failed to get information on DeleteSecuredArchetype method.")
                    parameters = {querySession, dbRoleGuid, archetypeGuid}
                Else
                    ' Save the new xoql definition to the NxSecuritySecuredArch table
                    mi = t.GetMethod("SetSecuredArchetypeViewXoql", BindingFlags.Public Or BindingFlags.Static)
                    If mi Is Nothing Then Throw New InvalidOperationException("Failed to get information on SetSecuredArchetypeViewXoql method.")
                    parameters = {querySession, dbRoleGuid, archetypeGuid, Xoql}
                End If
                If InSafeMode Then
                    Call Console.WriteLine("Skipping the update of the 3e database.")
                Else
                    Call Console.WriteLine("Updating the NxSecuritySecuredArch table...")
                    Call mi.Invoke(Nothing, parameters)
                End If

                ' Get the definition of the archetype object
                Dim typeOwnedObjectMgr As Type = Type.GetType("NextGen.Framework.Managers.OwnedObjectMgr.NxOwnedObjectMgr,OwnedObjectMgr", True)
                Dim miGetAppObject As MethodInfo = typeOwnedObjectMgr.GetMethod("GetAppObject", BindingFlags.Public Or BindingFlags.Static)
                If miGetAppObject Is Nothing Then Throw New InvalidOperationException("Failed to get methodinfo on GetAppObject.")
                parameters = {ArchetypeName, nxDocumentType_Archetype, isCustom_True}
                Dim baseArchetype As Object = miGetAppObject.Invoke(Nothing, parameters)

                ' Create an InstanceMgr object
                Dim typeInstanceMgr As Type = Type.GetType("NextGen.Framework.Managers.InstanceMgr.InstanceMgr,InstanceMgr", True)
                Dim miCreateInstance As MethodInfo = typeInstanceMgr.GetMethod("Create", BindingFlags.Public Or BindingFlags.Static)
                If miCreateInstance Is Nothing Then Throw New InvalidOperationException("Failed to get methodinfo on Create.")
                parameters = {querySession}
                Dim instanceMgr As Object = miCreateInstance.Invoke(Nothing, parameters)

                ' And finally apply the security
                Dim miCreateSecuredArchetypeViewForRole As MethodInfo = typeInstanceMgr.GetMethod("CreateSecuredArchetypeViewForRole", BindingFlags.Public Or BindingFlags.Instance)
                If miCreateSecuredArchetypeViewForRole Is Nothing Then Throw New InvalidOperationException("Failed to get methodinfo on CreateSecuredArchetypeViewForRole.")
                parameters = {RoleName, baseArchetype}
                If InSafeMode Then
                    Call Console.WriteLine("Skipping the creation of updated security views")
                Else
                    Call Console.WriteLine("Creating updated security views")
                    Call miCreateSecuredArchetypeViewForRole.Invoke(instanceMgr, parameters)
                End If

            Catch ex As Exception
                ExceptionToPassBack = ex
            End Try
        End Sub

        Private Shared ReadOnly Property ListOfRoleNames As String()
            Get
                Return GetDataFromCurrentDomain(Of String())()
            End Get
        End Property

        Private Shared ReadOnly Property ListOfArchetypeNames As String()
            Get
                Return GetDataFromCurrentDomain(Of String())()
            End Get
        End Property

        Private Shared ReadOnly Property RoleName As String
            Get
                Return GetDataFromCurrentDomain(Of String)()
            End Get
        End Property

        Private Shared ReadOnly Property ArchetypeName As String
            Get
                Return GetDataFromCurrentDomain(Of String)()
            End Get
        End Property

        Private Shared ReadOnly Property Xoql As String
            Get
                Return GetDataFromCurrentDomain(Of String)()
            End Get
        End Property

        Private Shared ReadOnly Property ClearSecurity As Boolean
            Get
                Return GetDataFromCurrentDomain(Of Boolean)()
            End Get
        End Property

        Private Shared Property ArchetypeGuids As Dictionary(Of String, Guid)
            Get
                Return GetDataFromCurrentDomain(Of Dictionary(Of String, Guid))()
            End Get

            Set(value As Dictionary(Of String, Guid))
                Call SetDataForCurrentDomain(Of Dictionary(Of String, Guid))(data:=value)
            End Set
        End Property

        Private Shared Property RoleGuids As Dictionary(Of String, Guid)
            Get
                Return GetDataFromCurrentDomain(Of Dictionary(Of String, Guid))()
            End Get

            Set(value As Dictionary(Of String, Guid))
                Call SetDataForCurrentDomain(Of Dictionary(Of String, Guid))(data:=value)
            End Set
        End Property

        Private Shared Property AssemblyName As String
            Get
                Return GetDataFromCurrentDomain(Of String)()
            End Get

            Set(value As String)
                Call SetDataForCurrentDomain(Of String)(data:=value)
            End Set
        End Property
    End Class

    Private WriteOnly Property ListOfRoleNames As String()
        Set(value As String())
            Call SetDomainData(Of String())(data:=value)
        End Set
    End Property

    Private WriteOnly Property ListOfArchetypeNames As String()
        Set(value As String())
            Call SetDomainData(Of String())(data:=value)
        End Set
    End Property

    Private WriteOnly Property RoleName As String
        Set(value As String)
            Call SetDomainData(Of String)(data:=value)
        End Set
    End Property

    Private WriteOnly Property ArchetypeName As String
        Set(value As String)
            Call SetDomainData(Of String)(data:=value)
        End Set
    End Property

    Private WriteOnly Property Xoql As String
        Set(value As String)
            Call SetDomainData(Of String)(data:=value)
        End Set
    End Property

    Private WriteOnly Property ClearSecurity As Boolean
        Set(value As Boolean)
            Call SetDomainData(Of Boolean)(data:=value)
        End Set
    End Property
End Class
