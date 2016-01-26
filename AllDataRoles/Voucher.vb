'*** RAW CODE ***
Imports NextGen.Framework.OQL
Imports NextGen.Framework.Managers.QueryMgr
Imports NextGen.Framework.Managers
Imports NextGen.Application

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

        Dim tVoucher As Query.Voucher = Query.Voucher.Current.MakeAlias(db)
        Dim aclQ As ISelect = GetAggregatedPermissions(db)
        Dim subQAlias As Query.NxAcl = Query.NxAcl.Current.MakeAlias(db)

        ' Main select
        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tVoucher)
        Call selectQ.WithSubquery(subQAlias, aclQ)
        Call selectQ.OuterJoin(tVoucher, subQAlias, tVoucher.VoucherID.IsEqualTo(subQAlias.ParentItemID))

        ' RowAccessLevel
        Call selectQ.Values(Exp.Coalesce(subQAlias.AccessLevel, RowAccessLevel.ReadOnly).As("RowAccessLevel"))

        ' IsConfidentialRow - As no columns on this archetype are secured, this is outputing a constant 0
        Call selectQ.Values(Exp.Value(False).As("IsConfidentialRow"))

        Return selectQ
    End Function

    ' Subquery that aggregates the explicit permissions, and makes them look like the NxAcl table
    Private Function GetAggregatedPermissions(ByVal db As IDatabase) As ISelect
        Dim tVoucher As Query.Voucher = Query.Voucher.Current.MakeAlias(db)
        Dim permittedUnits As ISelect = GetPermittedUnitList(db)

        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tVoucher)
        Call selectQ.Distinct()

        Call selectQ.Values(tVoucher.VoucherID.As("ParentItemID"))
        Call selectQ.Values(Exp.Value(RowAccessLevel.ReadWrite).As("AccessLevel"))
        Call selectQ.Values(Exp.Value(False).As("HideConfidential"))
        Call selectQ.Where(tVoucher.Office1.NxUnit.IsIn(permittedUnits))

        Return selectQ
    End Function

    Private Function GetPermittedUnitList(ByVal db As IDatabase) As ISelect
        Dim unitFilterQ As ISelect = GetUnitsInUserFilter(db)
        Dim defaultUnitQ As ISelect = GetDefaultUnit(db)

        Dim result As ISelect = unitFilterQ.Union(defaultUnitQ)
        Return result
    End Function

    Private Function GetUnitsInUserFilter(ByVal db As IDatabase) As ISelect
        Dim tNxFWKUser As Query.NxFWKUser = Query.NxFWKUser.Current.MakeAlias(db)

        Dim subQuery As ISelect = QueryBuilder.SelectFrom(tNxFwkUser)
        Call subQuery.Values(tNxFwkUser.UnitFilter)
        Call subQuery.Where(tNxFwkUser.NxFwkUserID.IsEqualTo(Exp.User))

        Dim tNxUnitFilterItem As Query.NxUnitFilterItem = Query.NxUnitFilterItem.Current.MakeAlias(db)
        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tNxUnitFilterItem)
        Call selectQ.Values(tNxUnitFilterItem.NxUnit.As("NxUnit"))
        Call selectQ.Where(tNxUnitFilterItem.FilterCode.IsEqualTo(subQuery.Result))
        Call selectQ.Where(tNxUnitFilterItem.NxUnit.IsNotNULL())

        Return selectQ
    End Function

    Private Function GetDefaultUnit(ByVal db As IDatabase) As ISelect
        Dim tNxFWKUser As Query.NxFWKUser = Query.NxFWKUser.Current.MakeAlias(db)

        Dim selectQ As ISelect = QueryBuilder.SelectFrom(tNxFWKUser)
        Call selectQ.Values(tNxFWKUser.DefaultUnit.As("NxUnit"))
        Call selectQ.Where(tNxFWKUser.NxFWKUserID.IsEqualTo(Exp.User))
        Call selectQ.Where(tNxFwkUser.DefaultUnit.IsNotNULL())

        Return selectQ
    End Function
End Class
