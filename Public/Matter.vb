'*** RAW CODE ***
Imports NextGen.Framework.OQL
Imports NextGen.Framework.Managers.QueryMgr
Imports NextGen.Framework.Managers
Imports NextGen.Application
Imports NextGen.Framework.Managers.ObjectMgr.Query
Imports System

Public Class MyMain
    Enum RowAccessLevel
        Deny = 0
        Manage = 1
        ReadWrite = 2
        [ReadOnly] = 3
        Hidden = 4
    End Enum

    Public Function MainFunction(ByVal QuerySession As ISession) As IOQLObject
        ' Create an alias to force the use of the dbo schema where needed. 
        ' The resultant XOQL will mention the database name, but it will not make it into the resultant view.
        Dim db As NextGen.Framework.OQL.IDatabase = New NextGen.Framework.OQL.Symbols.Database(QuerySession.Info.Database, "dbo")

        Dim tMatter As Query.Matter = Query.Matter.Current.MakeAlias(db)
        Dim tMatterAcl As New NxACLDenormalized("Matter")

        ' Main select
        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tMatter)
        Call selectQ.OuterJoin(tMatter, tMatterAcl, tMatter.MatterID.IsEqualTo(tMatterAcl.ParentItemID).And(tMatterAcl.BaseUserID.IsEqualTo(Exp.User)))

        ' Filter - If ACL denies/hides record then this trumps the matter cateogry
        Dim caseStatement As ISimpleCase = Exp.Case()
        Call caseStatement.When(AclHidesOrDeniesAccess(tMatterAcl), Exp.Value(False))
        Call caseStatement.When(AccessIsUnrestricted(tMatter), Exp.Value(True))
        Call caseStatement.Else(Exp.Value(False))
        Call selectQ.Where(caseStatement.End().IsEqualTo(Exp.Value(True)))
        
        ' RowAccessLevel column - default to ReadOnly where not explictly set
        Call selectQ.Values(Exp.Coalesce(tMatterAcl.AccessLevel, RowAccessLevel.ReadOnly).As("RowAccessLevel"))

        ' IsConfidentialRow column - default to True (hide) where not explictly set
        Call selectQ.Values(Exp.Coalesce(tMatterAcl.HideConfidential, True).As("IsConfidentialRow"))

        ' Description column - this must also be marked IsConfidential in the IDE
        caseStatement = Exp.Case()
        Call caseStatement.When(tMatterAcl.HideConfidential.IsFalse(), tMatter.Description)
        Call caseStatement.Else(Exp.Null)
        Call selectQ.Values(caseStatement.End().As("Description"))

        ' DisplayName column - this must also be marked IsConfidential in the IDE
        caseStatement = Exp.Case()
        Call caseStatement.When(tMatterAcl.HideConfidential.IsFalse(), tMatter.DisplayName)
        Call caseStatement.Else(Exp.Null)
        Call selectQ.Values(caseStatement.End().As("DisplayName"))

        ' Narrative column - this must also be marked IsConfidential in the IDE
        caseStatement = Exp.Case()
        Call caseStatement.When(tMatterAcl.HideConfidential.IsFalse(), tMatter.Narrative)
        Call caseStatement.Else(Exp.Null)
        Call selectQ.Values(caseStatement.End().As("Narrative"))

        ' Narrative_UnformattedText column - plain text version of the Narrative column
        caseStatement = Exp.Case()
        Call caseStatement.When(tMatterAcl.HideConfidential.IsFalse(), tMatter.Narrative.UnformattedText)
        Call caseStatement.Else(Exp.Null)
        Call selectQ.Values(caseStatement.End().As("Narrative_UnformattedText"))

        Return selectQ
    End Function

    Private Function AclHidesOrDeniesAccess(ByVal tNxACLDenormalized As NxACLDenormalized) As IPredicate
        Dim result As IPredicate = tNxACLDenormalized.AccessLevel.IsIn(RowAccessLevel.Hidden, RowAccessLevel.Deny)
        Return result
    End Function

    Private Function AccessIsUnrestricted(ByVal tMatter As Query.Matter) As IPredicate
        Dim result As IPredicate = tMatter.MattCategory.IsEqualTo("Unrestricted")
        Return result
    End Function

    Class NxACLDenormalized
        Inherits NxSimpleNode

        Private Const ACL_DENORMALIZED_PREFIX As String = "NxACLDenormalized_"
        Private Const ACL_USER_ID As String = "BaseUserID"

        Private ReadOnly _accessLevel As ILeaf
        Private ReadOnly _hideConfidential As ILeaf
        Private ReadOnly _parentItemID As ILeaf
        Private ReadOnly _baseUserID As ILeaf

        Public Sub New(ByVal archetypeId As String)
            Call MyBase.New("NxACLDenormalized_" & archetypeId)

            Me._accessLevel = Me.AddInt("AccessLevel")
            Me._hideConfidential = Me.AddBoolean("HideConfidential")
            Me._parentItemID = Me.AddGUID("ParentItemID")
            Me._baseUserID = Me.AddGUID("BaseUserID")
        End Sub

        Public ReadOnly Property AccessLevel As ILeaf
            Get
                Return Me._accessLevel
            End Get
        End Property

        Public ReadOnly Property HideConfidential As ILeaf
            Get
                Return Me._hideConfidential
            End Get
        End Property

        Public ReadOnly Property ParentItemID As ILeaf
            Get
                Return Me._parentItemID
            End Get
        End Property

        Public ReadOnly Property BaseUserID As ILeaf
            Get
                Return Me._baseUserID
            End Get
        End Property
    End Class
End Class
