'*** RAW CODE ***
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports NextGen.Framework.OQL
Imports NextGen.Framework.Managers.QueryMgr
Imports NextGen.Framework.Managers
Imports NextGen.Application

Public Class InternalOql
    Public Shared Function GetDbRoles(ByVal qs As ISession, ByVal roles As IEnumerable(Of String)) As IDictionary(Of String, Guid)
        Dim listOfDbRoles As IExpression() = roles.Select(Function(item) Exp.Value(item)).ToArray()

        Dim tNxSecurityDBRole As Query.NxSecurityDBRole = Query.NxSecurityDBRole.Current

        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tNxSecurityDBRole)
        Call selectQ.Values(tNxSecurityDBRole.DBRole, tNxSecurityDBRole.NxSecurityDBRoleID)
        Call selectQ.Where(tNxSecurityDBRole.DBRole.IsIn(listOfDbRoles))

        Dim result As New Dictionary(Of String, Guid)(StringComparer.OrdinalIgnoreCase)
        Using cur As ICursor = qs.OpenCursor(selectQ)
            Do While cur.MoveNext()
                Dim dbRoleName As String = cur.GetString(tNxSecurityDBRole.DBRole)
                Dim roleId As Guid = cur.GetGuid(tNxSecurityDBRole.NxSecurityDBRoleID)
                Call result.Add(dbRoleName, roleId)
            Loop
        End Using

        Return result
    End Function

    Public Shared Function GetArchetypes(ByVal qs As ISession, ByVal archetypes As IEnumerable(Of String)) As IDictionary(Of String, Guid)
        Dim listOfArchetypes As IExpression() = archetypes.Select(Function(item) Exp.Value(item)).ToArray()

        Dim tNxFwkAppObject As Query.NxFwkAppObject = Query.NxFwkAppObject.Current
        Dim tNxFwkAppObjectType As Query.NxFwkAppObjectType = tNxFwkAppObject.NxFWKAppObjectType

        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tNxFwkAppObject)
        Call selectQ.Values(tNxFwkAppObject.AppObjectCode, tNxFwkAppObject.NxFwkAppObjectID)
        Call selectQ.Where(tNxFwkAppObject.AppObjectCode.IsIn(listOfArchetypes))
        Call selectQ.Where(tNxFwkAppObjectType.AppObjectTypeCode.IsEqualTo("Archetype"))

        Dim result As New Dictionary(Of String, Guid)(StringComparer.OrdinalIgnoreCase)
        Using cur As ICursor = qs.OpenCursor(selectQ)
            Do While cur.MoveNext()
                Dim archetypeName As String = cur.GetString(tNxFwkAppObject.AppObjectCode)
                Dim appObjectId As Guid = cur.GetGuid(tNxFwkAppObject.NxFwkAppObjectID)
                Call result.Add(archetypeName, appObjectId)
            Loop
        End Using

        Return result
    End Function

    Public Shared Sub DeleteSecuredArchetype(ByVal qs As ISession, ByVal dbRoleId As Guid, ByVal archetypeId As Guid)
        Dim tNxSecuritySecuredArch As Query.NxSecuritySecuredArch = Query.NxSecuritySecuredArch.Current

        Dim deleteQ As IDelete = QueryBuilder.Delete(tNxSecuritySecuredArch)
        Call deleteQ.Where(tNxSecuritySecuredArch.DBRole.IsEqualTo(dbRoleId))
        Call deleteQ.Where(tNxSecuritySecuredArch.ArchetypeName.IsEqualTo(archetypeId))

        Call qs.Execute(deleteQ)
    End Sub

    Public Shared Sub SetSecuredArchetypeViewXoql(ByVal qs As ISession, ByVal dbRoleId As Guid, ByVal archetypeId As Guid, ByVal xoql As String)
        Dim tNxSecuritySecuredArch As Query.NxSecuritySecuredArch = Query.NxSecuritySecuredArch.Current

        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tNxSecuritySecuredArch)
        Call selectQ.Values(Exp.Value(1))
        Call selectQ.Where(tNxSecuritySecuredArch.DBRole.IsEqualTo(dbRoleId))
        Call selectQ.Where(tNxSecuritySecuredArch.ArchetypeName.IsEqualTo(archetypeId))

        Dim insertQ As IInsert = QueryBuilder.Insert(tNxSecuritySecuredArch)
        Call insertQ.Values(Exp.NewGUID).Into(tNxSecuritySecuredArch.NxSecuritySecuredArchID)
        Call insertQ.Values(Exp.Value(dbRoleId)).Into(tNxSecuritySecuredArch.DBRole)
        Call insertQ.Values(Exp.Value(archetypeId)).Into(tNxSecuritySecuredArch.ArchetypeName)
        Call insertQ.Values(Exp.Value(5)).Into(tNxSecuritySecuredArch.DefaultAccess)    ' No access
        Call insertQ.Values(Exp.Value(xoql)).Into(tNxSecuritySecuredArch.ViewXOQL)

        Dim updateQ As IUpdate = QueryBuilder.Update(tNxSecuritySecuredArch)
        Call updateQ.Values(Exp.Value(xoql)).Into(tNxSecuritySecuredArch.ViewXOQL)
        Call updateQ.Where(tNxSecuritySecuredArch.DBRole.IsEqualTo(dbRoleId))
        Call updateQ.Where(tNxSecuritySecuredArch.ArchetypeName.IsEqualTo(archetypeId))

        Dim batch As IBatch = QueryBuilder.Batch()
        Call batch.If(selectQ.Exists).Then(updateQ).Else(insertQ)

        Call qs.Execute(batch)
    End Sub
End Class
