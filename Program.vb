Imports System.Configuration
Imports System.Collections.Generic
Imports System.IO

Module Program
    Public IsVerboseOutputRequired As Boolean
    Public InSafeMode As Boolean = True

    Function Main(ByVal args As String()) As Integer
        Try
            Run(args)

            Return 0
        Catch ex As Exception
            Call Console.WriteLine()
            Call Console.WriteLine(ex.ToString())

            Return 1
        Finally
            If IsRunningInVisualStudio() Then
                Call Console.WriteLine()
                Call Console.WriteLine("Press Return to continue.")
                Call Console.ReadLine()
            End If
        End Try
    End Function

    Private Sub Run(args As String())
        Call DisplayHeader(args)
        Call LoadConfiguration()

        Dim codeFragments As Dictionary(Of String, String) = CompileCodeFragments()
        Call SaveAndApplySecurityViews(codeFragments)
    End Sub

    Private Sub DisplayHeader(ByVal args As IEnumerable(Of String))
        Call Console.WriteLine("Update data level security views in 3e")
        Call Console.WriteLine()

        Dim iterator As IEnumerator(Of String) = args.GetEnumerator()
        Do While iterator.MoveNext()
            Dim currentArg As String = iterator.Current
            If currentArg.Equals("/v", StringComparison.OrdinalIgnoreCase) OrElse currentArg.Equals("/verbose", StringComparison.OrdinalIgnoreCase) Then
                IsVerboseOutputRequired = True
            End If

            If currentArg.Equals("RUN", StringComparison.Ordinal) Then
                InSafeMode = False
            End If
        Loop
    End Sub

    Private Sub LoadConfiguration()
        Dim config As ConfigurationDataSection = CType(ConfigurationManager.GetSection("securityViewSetup"), ConfigurationDataSection)

        Call Console.WriteLine("Common reference assemblies:")
        For Each item As ConfigurationDataSection.ReferenceAssembly In config.CommonReferenceAssemblies
            Call Console.WriteLine("- {0}", item.Name)
        Next
        Call Console.WriteLine()

        For Each item As ConfigurationDataSection.RoleElement In config.Roles
            Call Console.WriteLine(item.Name)

            For Each archetype As ConfigurationDataSection.ArchetypeElement In item.Archetypes
                If archetype.ClearSecurity Then
                    If Not String.IsNullOrWhiteSpace(archetype.CodeFile) Then
                        Throw New ConfigurationErrorsException("Cannot specify both clearSecurity and a codeFile for " & archetype.Name & ".")
                    End If
                    If archetype.AdditionalReferenceAssemblies IsNot Nothing AndAlso archetype.AdditionalReferenceAssemblies.Count <> 0 Then
                        Throw New ConfigurationErrorsException("Cannot specify both clearSecurity and AdditionalReferenceAssemblies for " & archetype.Name & ".")
                    End If

                    Call Console.WriteLine("- {0} *** clear security ***", archetype.Name)
                Else
                    Call Console.WriteLine("- {0} ({1})", archetype.Name, archetype.CodeFile)

                    For Each ra As ConfigurationDataSection.ReferenceAssembly In archetype.AdditionalReferenceAssemblies
                        Call Console.WriteLine("  + {0}", ra.Name)
                    Next
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' Compiles each code fragment and converts the queries contained within to an XOQL representation
    ''' </summary>
    ''' <returns>A dictionary object where each key is the path to the code file, and the value is the resultant XOQL.</returns>
    ''' <remarks>The same code file may be referenced more than once, but each file will only be compiled once.</remarks>
    Private Function CompileCodeFragments() As Dictionary(Of String, String)
        Dim config As ConfigurationDataSection = CType(ConfigurationManager.GetSection("securityViewSetup"), ConfigurationDataSection)
        Dim codeFragments As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Dim commonDependencies As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each assemblyName As ConfigurationDataSection.ReferenceAssembly In config.CommonReferenceAssemblies
            Call commonDependencies.Add(assemblyName.Name)
        Next

        For Each role As ConfigurationDataSection.RoleElement In config.Roles
            For Each archetype As ConfigurationDataSection.ArchetypeElement In role.Archetypes
                If archetype.ClearSecurity OrElse codeFragments.ContainsKey(archetype.CodeFile) Then Continue For

                Call Console.WriteLine()
                Call Console.WriteLine("Compiling {0}...", archetype.CodeFile)

                Dim referencedAssemblies As New HashSet(Of String)(commonDependencies, StringComparer.OrdinalIgnoreCase)
                For Each assemblyName As ConfigurationDataSection.ReferenceAssembly In archetype.AdditionalReferenceAssemblies
                    Call referencedAssemblies.Add(assemblyName.Name)
                Next

                Using card As New CompileAndRunDomain()
                    Dim xoql As String = card.CompileAndRun(archetype.Name, archetype.CodeFile, referencedAssemblies)
                    Call codeFragments.Add(archetype.CodeFile, xoql)
                End Using
            Next
        Next

        Return codeFragments
    End Function

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="codeFragments"></param>
    ''' <remarks></remarks>
    Private Sub SaveAndApplySecurityViews(ByVal codeFragments As IReadOnlyDictionary(Of String, String))
        Dim config As ConfigurationDataSection = CType(ConfigurationManager.GetSection("securityViewSetup"), ConfigurationDataSection)

        Dim roles As List(Of ConfigurationDataSection.RoleElement) = config.Roles.Cast(Of ConfigurationDataSection.RoleElement)().ToList()
        Dim listOfRoles = roles.Select(Function(role) role.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        Dim listOfArchetypes = roles.SelectMany(Function(role) role.Archetypes.Cast(Of ConfigurationDataSection.ArchetypeElement)().Select(Function(arch) arch.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()

        Call Console.WriteLine()
        Call Console.WriteLine("Preparing to apply security...")

        Using ans As New ApplyNewSecurity(listOfRoles, listOfArchetypes)
            For Each role As ConfigurationDataSection.RoleElement In config.Roles
                For Each archetype As ConfigurationDataSection.ArchetypeElement In role.Archetypes
                    Dim isRemovingSecurity As Boolean = archetype.ClearSecurity
                    Dim roleName As String = role.Name
                    Dim archetypeName As String = archetype.Name
                    Dim xoql As String = If(isRemovingSecurity, Nothing, codeFragments.Item(archetype.CodeFile))

                    Call Console.WriteLine()
                    Call Console.WriteLine("{0} security on {1} for {2}...", If(isRemovingSecurity, "Removing", "Applying"), archetypeName, roleName)
                    If isRemovingSecurity Then
                        Call ans.ClearSecurityView(roleName, archetypeName)
                    Else
                        Call ans.SaveAndApplySecurityView(roleName, archetypeName, xoql)
                    End If
                Next
            Next
        End Using
    End Sub

    Public Function IsRunningInVisualStudio() As Boolean
        If Process.GetCurrentProcess().MainModule.ModuleName.EndsWith(".vshost.exe", StringComparison.OrdinalIgnoreCase) Then
            Dim basePath As String = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
            If basePath.EndsWith("\bin\debug", StringComparison.OrdinalIgnoreCase) OrElse basePath.EndsWith("\bin\release", StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        End If

        Return False
    End Function
End Module
