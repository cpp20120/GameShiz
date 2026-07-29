namespace ChatAdministration.Domain.Models;

public enum Permission
{
    ChatViewSettings,
    MembersView,
    MembersRemoveWarning,
    MembersViewWarnings,
    MembersMute,
    MembersUnmute,
    MembersWarn,
    MembersBan,
    MembersUnban,
    MembersKick,
    MessagesDelete,
    MessagesPurge,
    ChatManageSettings,
    RulesView,
    RulesManage,
    CasesView,
    CasesManage,
    CasesResolve,
    AppealsView,
    AppealsResolve,
    RolesView,
    RolesManage,
    AuditView,
    AnalyticsView,
}
