# DataLevelSecurityFor3e
**Updates the security views that support data level security in 3e**

If you are implementing data level security then you are obliged to fight with the "Security Row Level" process, which can be a distinctly unpleasant experience.
For any solution that approaches a real life situation you need to enter XOQL to define your requirement which leads to a number of problems:
* It's difficult to write.
* It's difficult to apply.
* It's difficult to version track.
* It's difficult to release.
* It's difficult to audit.

In fact, you might as well just give up now.

Alternatively, you can use this utility to effectively address all these issues.

You write your security code in OQL. You use the app.config to determine what security roles to apply the code to.
The utility will:
* compile the OQL code fragments
* convert the code to its XOQL representation
* update the NxSecuritySecuredArch table
* apply the new security views

## What's Included

The code in this respository includes some demonstration code that you can use to get started.

The supplied **app.config** file is set up to apply revised security to two of the role that are provided out-of-the-box.
The *te_admin* role will have any security definition currently applied to the Matter and Timecard archetypes removed.
It will apply revised security to the Voucher archetype.
The *te_public* role will have security applied to the Matter, Timecard and Voucher archetypes.

During compilation, the archetype assemblies listed in **commonReferenceAssemblies** will be referenced for all code fragments. 
During compilation of Voucher, the assemblies listed in **additionalReferenceAssemblies** will be additionally referenced.

The **RootPath** setting directs the utility to the root of the 3e installation, i.e. the \\server\TE_3E_Share\TE_3E_Instance directory.
The **FrameworkStaging** setting directs the utility from the RootPath to the staging directory, which is where the referenced assemblies will be picked up from.

## Security for the Matter archetype

The Matter code demonstrates a common pattern for security where access is allowed if the matter is marked as unrestricted or the current user is granted access via the Access Control List.
In this example we check if the Matter.MattCategory value is set to "Unrestricted", but there are plenty of variations in how to approach this.

Without a corresponding entry in the ACL a default access of Read-Only and Hide Confidential fields will be returned.
If the ACL indicates that access for the current user is Denied or Hidden, then the user will be locked out - in 3e the Deny permission should always trump any other.
Any other value on the ACL will be returned along with the Hide Confidential attributes setting.

Where Hide Confidential attributes is indicated, null values are substituted on the appropriate attributes. 
It is *very* important that the list of attributes that this pattern of logic is applied to, matches *exactly* the list of attributes that is marked Confidential in the IDE.
Likewise, you should only apply security to an archetype that has Row Level Security turned on in the IDE. 

Note that you do not need to output any non-confidential fields from the OQL - these will be automatically added to the resultant security view by the 3e framework.

## Security for the Timecard archetype

The Timecard code demonstrates applying the same security as Matter, which is more likely than having separate security on Timecard itself.
Note that if you do make the Timecard.Narrative attribute confidential there are problems with the Time Entry and Time Modify processes which mean that the value of the narrative can be lost.

## Security for the Voucher archetype

The Voucher code demonstrates how to secure data based on a user's default unit and permitted list of units, rather than using the ACL.
Where an archetype does not have any confidential attributes (like Voucher), it's fine to return a HideConfidential value of false for each record.

