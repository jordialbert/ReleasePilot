namespace ReleaseManagement.Domain;

public sealed class OnlyApproverCanApprove()
    : DomainException(
        "only_approver_can_approve",
        "Only an Approver can approve a Promotion.");
