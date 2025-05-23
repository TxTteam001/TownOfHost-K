using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;
public sealed class GuardMaster : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(GuardMaster),
            player => new GuardMaster(player),
            CustomRoles.GuardMaster,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            21300,
            SetupOptionItem,
            "gms",
            "#8FBC8B"
        );
    public GuardMaster(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        CanSeeCheck = OptoonCheck.GetBool();
        AddGuardCount = OptionAddGuardCount.GetInt();
        Guard = 0;
        ta = Task.GetInt();
        kakusei = !Kakusei.GetBool() || Task.GetInt() < 1; ;
    }
    private static OptionItem OptionAddGuardCount;
    private static OptionItem OptoonCheck;
    private static int AddGuardCount;
    private static bool CanSeeCheck;
    static OptionItem Kakusei;
    static OptionItem Task;
    bool kakusei;
    int ta;
    int Guard = 0;
    enum OptionName
    {
        AddGuardCount,
        MadGuardianCanSeeWhoTriedToKill
    }
    private static void SetupOptionItem()
    {
        OptoonCheck = BooleanOptionItem.Create(RoleInfo, 10, OptionName.MadGuardianCanSeeWhoTriedToKill, true, false);
        OptionAddGuardCount = FloatOptionItem.Create(RoleInfo, 11, OptionName.AddGuardCount, new(1f, 99f, 1f), 1f, false)
            .SetValueFormat(OptionFormat.Times);
        Kakusei = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.TaskKakusei, true, false);
        Task = FloatOptionItem.Create(RoleInfo, 13, GeneralOption.Kakuseitask, new(0f, 255f, 1f), 5f, false, Kakusei);
        OverrideTasksData.Create(RoleInfo, 20);
    }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
    {
        (var killer, var target) = info.AttemptTuple;
        // 直接キル出来る役職チェック
        if (Guard <= 0) return true; // ガードなしで普通にキル

        if (!NameColorManager.TryGetData(killer, target, out var value) || value != RoleInfo.RoleColorCode)
        {
            NameColorManager.Add(killer.PlayerId, target.PlayerId);
            if (CanSeeCheck)
                NameColorManager.Add(target.PlayerId, killer.PlayerId, RoleInfo.RoleColorCode);
        }
        killer.RpcProtectedMurderPlayer(target);
        if (CanSeeCheck && kakusei) target.RpcProtectedMurderPlayer(target);
        info.CanKill = false;
        Guard--;
        UtilsGameLog.AddGameLog($"GuardMaster", Utils.GetPlayerColor(Player) + ":  " + string.Format(GetString("GuardMaster.Guard"), Utils.GetPlayerColor(killer, true) + $"(<b>{UtilsRoleText.GetTrueRoleName(killer.PlayerId, false)}</b>)"));
        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : ガード残り{Guard}回", "GuardMaster");
        UtilsNotifyRoles.NotifyRoles();
        return true;
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(ta))
        {
            if (kakusei == false)
                if (!Utils.RoleSendList.Contains(Player.PlayerId))
                    Utils.RoleSendList.Add(Player.PlayerId);
            kakusei = true;
        }
        if (IsTaskFinished && Player.IsAlive())
            Guard += AddGuardCount;
        return true;
    }
    public override string GetProgressText(bool comms = false, bool gamelog = false) => CanSeeCheck ? Utils.ColorString(Guard == 0 ? UnityEngine.Color.gray : RoleInfo.RoleColor, $"({Guard})") : "";
    public override CustomRoles Jikaku() => kakusei ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
}