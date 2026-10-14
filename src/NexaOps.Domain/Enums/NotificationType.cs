namespace NexaOps.Domain.Enums;

public enum NotificationType
{
    FormSubmitted = 0,
    ApprovalRequired = 1,
    StepApproved = 2,
    SubmissionApproved = 3,
    SubmissionRejected = 4,
    SubmissionCancelled = 5,
    MemberInvited = 6,
    RoleChanged = 7,
    OwnershipTransferred = 8,
    General = 9
}
