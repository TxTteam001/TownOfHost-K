using AmongUs.GameOptions;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;
public sealed class Trapper : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Trapper),
            player => new Trapper(player),
            CustomRoles.Trapper,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            20200,
            SetupOptionItem,
            "tra",
            "#5a8fd0",
            from: From.TownOfHost
        );
    public Trapper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        BlockMoveTime = OptionBlockMoveTime.GetFloat();
        ta = Task.GetInt();
        kakusei = !Kakusei.GetBool() || Task.GetInt() < 1; ;
    }
    static OptionItem Kakusei;
    static OptionItem Task;
    bool kakusei;
    int ta;
    private static OptionItem OptionBlockMoveTime;
    enum OptionName
    {
        TrapperBlockMoveTime
    }

    private static float BlockMoveTime;

    private static void SetupOptionItem()
    {
        OptionBlockMoveTime = FloatOptionItem.Create(RoleInfo, 10, OptionName.TrapperBlockMoveTime, new(1f, 180f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        Kakusei = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.TaskKakusei, true, false);
        Task = FloatOptionItem.Create(RoleInfo, 12, GeneralOption.Kakuseitask, new(0f, 255f, 1f), 5f, false, Kakusei);
    }
    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (!kakusei) return;
        if (info.IsSuicide) return;

        var killer = info.AttemptKiller;
        var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
        ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
        killer.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            Main.AllPlayerSpeed[killer.PlayerId] = tmpSpeed;
            ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
            killer.MarkDirtySettings();
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
        }, BlockMoveTime, "Trapper BlockMove");
    }
    public override CustomRoles Jikaku() => kakusei ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(ta))
        {
            if (kakusei == false)
                if (!Utils.RoleSendList.Contains(Player.PlayerId))
                    Utils.RoleSendList.Add(Player.PlayerId);
            kakusei = true;
        }
        return true;
    }
}