This is a partial import of rx.codeplex.com for mono.

Since rx.codeplex.com is massive and we only need partial source tree of it
(and due to some checkout failure on Linux [*1]), we set up another
repository for mono submodule.

This tree is hence manually imported. Though it is somewhat easy to maintain:
we can cherry-pick changes only in Rx.NET in the rx.codeplex.com.
Mostly we would not need to copy sources from the original treemanually,
but sometimes we do when a checkout involves other directories than Rx.NET.

For every original release, we should import the updates and commit to
this master, then create a branch for each release and *then* apply our
local changes (which is minimum but required) to the branch.

[*1] http://codeplex.codeplex.com/workitem/26133
