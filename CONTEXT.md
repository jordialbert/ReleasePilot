# Release Management

Release Management controls how versions of deployable applications advance through an ordered environment pipeline.

## Language

**Application**:
An independently deployable software system managed by ReleasePilot.

**Application Version**:
A specific version of an Application that moves through the environment pipeline.
_Avoid_: Release

**Environment**:
A named stage in the ordered deployment pipeline.
_Avoid_: Stage

**Environment Pipeline**:
The fixed sequence `dev → staging → production` through which every Application Version advances without skipping.

**Pipeline Position**:
The last Environment successfully completed by an Application Version.
_Avoid_: Current Environment

**Promotion**:
An attempt to advance one Application Version to exactly the next Environment in the pipeline.
_Avoid_: Deployment

**Active Promotion**:
A Promotion that is Requested, Approved, or Deploying.
_Avoid_: In-progress Promotion

**Terminal Promotion**:
A completed, cancelled, or rolled-back Promotion that cannot transition again.
_Avoid_: Terminated Promotion

**Deployment**:
The external execution triggered by an approved Promotion.
_Avoid_: Promotion

**Approver**:
An actor authorized to approve a Promotion.

**Work Item**:
A tracked change linked to an Application Version and used as source material for release notes.
_Avoid_: Issue, ticket

**Release Notes Draft**:
Agent-produced release notes associated with a Promotion.
_Avoid_: Release notes

**Breaking Change**:
A change linked to a Work Item that requires special attention from consumers because compatibility may be affected.
