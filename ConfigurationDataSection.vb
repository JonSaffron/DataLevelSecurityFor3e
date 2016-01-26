Imports System.Configuration

' ReSharper disable once ClassNeverInstantiated.Global
Public Class ConfigurationDataSection
    Inherits ConfigurationSection

    Private Const CommonReferenceAssembliesName As String = "commonReferenceAssemblies"
    Private Const RolesName As String = "roles"
    Private Const RoleName As String = "role"
    Private Const ArchetypesName As String = "archetypes"
    Private Const ArchetypeName As String = "archetype"
    Private Const AdditionalReferenceAssembliesName As String = "additionalReferenceAssemblies"

    <ConfigurationProperty(commonReferenceAssembliesName)> _
    <ConfigurationCollection(GetType(ReferenceAssemblyCollection), AddItemName:="add")> _
    Public ReadOnly Property CommonReferenceAssemblies As ReferenceAssemblyCollection
        Get
            Dim result As ReferenceAssemblyCollection = CType(Me.Item(commonReferenceAssembliesName), ReferenceAssemblyCollection)
            Return result
        End Get
    End Property

    <ConfigurationProperty(RolesName)> _
    <ConfigurationCollection(GetType(RolesCollection), AddItemName:=RoleName)> _
    Public ReadOnly Property Roles As RolesCollection
        Get
            Dim result As RolesCollection = CType(Me.Item(RolesName), RolesCollection)
            Return result
        End Get
    End Property

    Public Class ReferenceAssemblyCollection
        Inherits ConfigurationElementCollection

        Protected Overloads Overrides Function CreateNewElement() As ConfigurationElement
            Dim result As New ReferenceAssembly()
            Return result
        End Function

        Protected Overrides Function GetElementKey(element As ConfigurationElement) As Object
            Dim ra As ReferenceAssembly = CType(element, ReferenceAssembly)
            Dim result As String = ra.Name
            Return result
        End Function
    End Class

    Public Class ReferenceAssembly
        Inherits ConfigurationElement

        <ConfigurationProperty("name", IsRequired:=True)> _
        Public ReadOnly Property Name As String
            Get
                Dim result As String = CType(Me.Item("name"), String)
                Return result
            End Get
        End Property
    End Class

    Public Class RolesCollection
        Inherits ConfigurationElementCollection

        Protected Overloads Overrides Function CreateNewElement() As ConfigurationElement
            Dim result As New RoleElement()
            Return result
        End Function

        Protected Overrides Function GetElementKey(element As ConfigurationElement) As Object
            Dim re As RoleElement = CType(element, RoleElement)
            Dim result As String = re.Name
            Return result
        End Function
    End Class

    Public Class RoleElement
        Inherits ConfigurationElement

        <ConfigurationProperty("name", IsRequired:=True)> _
        Public ReadOnly Property Name As String
            Get
                Dim result As String = CType(Me.Item("name"), String)
                Return result
            End Get
        End Property

        <ConfigurationProperty(ArchetypesName, IsRequired:=False)> _
        <ConfigurationCollection(GetType(ReferenceAssemblyCollection), AddItemName:=ArchetypeName)> _
        Public ReadOnly Property Archetypes As ArchetypesCollection
            Get
                Dim result As ArchetypesCollection = CType(Me.Item(ArchetypesName), ArchetypesCollection)
                Return result
            End Get
        End Property
    End Class

    Public Class ArchetypesCollection
        Inherits ConfigurationElementCollection

        Protected Overloads Overrides Function CreateNewElement() As ConfigurationElement
            Dim result As New ArchetypeElement()
            Return result
        End Function

        Protected Overrides Function GetElementKey(element As ConfigurationElement) As Object
            Dim ra As ArchetypeElement = CType(element, ArchetypeElement)
            Dim result As String = ra.Name
            Return result
        End Function
    End Class

    Public Class ArchetypeElement
        Inherits ConfigurationElement

        <ConfigurationProperty("name", IsRequired:=True)> _
        Public ReadOnly Property Name As String
            Get
                Dim result As String = CType(Me.Item("name"), String)
                Return result
            End Get
        End Property

        <ConfigurationProperty("codeFile", IsRequired:=False)> _
        Public ReadOnly Property CodeFile As String
            Get
                Dim result As String = CType(Me.Item("codeFile"), String)
                Return result
            End Get
        End Property

        <ConfigurationProperty("clearSecurity", IsRequired:=False)> _
        Public ReadOnly Property ClearSecurity As Boolean
            Get
                Dim result As Boolean = CType(Me.Item("clearSecurity"), Boolean)
                Return result
            End Get
        End Property

        <ConfigurationProperty(additionalReferenceAssembliesName, IsRequired:=False)> _
        <ConfigurationCollection(GetType(ReferenceAssemblyCollection), AddItemName:="add")> _
        Public ReadOnly Property AdditionalReferenceAssemblies As ReferenceAssemblyCollection
            Get
                Dim result As ReferenceAssemblyCollection = CType(Me.Item(additionalReferenceAssembliesName), ReferenceAssemblyCollection)
                Return result
            End Get
        End Property
    End Class
End Class
