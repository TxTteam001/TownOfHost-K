using AmongUs.GameOptions;
using UnityEngine;
using Hazel;
using System;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Madmate;
public sealed class MadTeller : RoleBase, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadTeller),
            player => new MadTeller(player),
            CustomRoles.MadTeller,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Madmate,
            12000,
            SetupOptionItem,
            "Mt",
            introSound: () => GetIntroSound(RoleTypes.Scientist)
        );
    public MadTeller(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.ForRecompute
    )
    {
        collect = Optioncollect.GetInt();
        Max = OptionMaximum.GetFloat();
        count = 0;
        mcount = 0;
        srole = OptionRole.GetBool();
        canSeeKillFlash = Options.MadmateCanSeeKillFlash.GetBool();
        canSeeDeathReason = Options.MadmateCanSeeDeathReason.GetBool();
        Votemode = (VoteMode)OptionVoteMode.GetValue();
        onemeetingmaximum = Option1MeetingMaximum.GetFloat();
        MyTaskState.NeedTaskCount = OptionTaskTrigger.GetInt();
    }
    private static bool canSeeKillFlash;
    private static bool canSeeDeathReason;
    private static OptionItem OptionTaskTrigger;
    private static OptionItem Optioncollect;
    private static OptionItem OptionMaximum;
    private static OptionItem OptionRole;
    private static OptionItem OptionVoteMode;
    private static OptionItem Option1MeetingMaximum;
    public bool? CheckKillFlash(MurderInfo info) => canSeeKillFlash;
    public bool? CheckSeeDeathReason(PlayerControl seen) => canSeeDeathReason;
    public override CustomRoles GetFtResults(PlayerControl player) => Options.MadTellOpt();
    public float collect;
    public bool srole;
    public float Max;
    public VoteMode Votemode;
    int count;
    float onemeetingmaximum;
    float mcount;

    enum Option
    {
        TellerCollectRect,
        Ucount,
        tRole,
        Votemode,
        meetingmc
    }
    public enum VoteMode
    {
        uvote,
        SelfVote,
    }

    private static void SetupOptionItem()
    {
        Optioncollect = FloatOptionItem.Create(RoleInfo, 10, Option.TellerCollectRect, new(0f, 100f, 2f), 100f, false)
            .SetValueFormat(OptionFormat.Percent);
        OptionMaximum = FloatOptionItem.Create(RoleInfo, 11, Option.Ucount, new(1f, 99f, 1f), 1f, false)
            .SetValueFormat(OptionFormat.Times);
        OptionVoteMode = StringOptionItem.Create(RoleInfo, 12, Option.Votemode, EnumHelper.GetAllNames<VoteMode>(), 1, false);
        OptionRole = BooleanOptionItem.Create(RoleInfo, 13, Option.tRole, true, false);
        Option1MeetingMaximum = FloatOptionItem.Create(RoleInfo, 14, Option.meetingmc, new(0f, 99f, 1f), 0f, false, infinity: true)
            .SetValueFormat(OptionFormat.Times);
        OptionTaskTrigger = IntegerOptionItem.Create(RoleInfo, 15, GeneralOption.TaskTrigger, new(0, 99, 1), 1, false).SetValueFormat(OptionFormat.Pieces);
        OverrideTasksData.Create(RoleInfo, 20);
    }

    public override void Add()
        => AddS(Player);
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(count);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        count = reader.ReadInt32();
    }
    bool Check() => MyTaskState.HasCompletedEnoughCountOfTasks(OptionTaskTrigger.GetInt());
    public override void OnStartMeeting() => mcount = 0;
    public override string GetProgressText(bool comms = false, bool gamelog = false) => Utils.ColorString(!Check() ? Color.gray : Max <= count ? Color.gray : Color.cyan, $"({Max - count})");
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting && Player.IsAlive() && seer.PlayerId == seen.PlayerId && Canuseability() && Max > count && Check())
        {
            var mes = $"<color={RoleInfo.RoleColorCode}>{(Votemode == VoteMode.SelfVote ? GetString("SelfVoteRoleInfoMeg") : GetString("NomalVoteRoleInfoMeg"))}</color>";
            return isForHud ? mes : $"<size=40%>{mes}</size>";
        }
        return "";
    }
    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Canuseability()) return true;
        if (Max > count && Is(voter) && Check() && (mcount < onemeetingmaximum || onemeetingmaximum == 0))
        {
            if (Votemode == VoteMode.uvote)
            {
                if (Player.PlayerId == votedForId || votedForId == SkipId) return true;
                Uranai(votedForId);
                return false;
            }
            else
            {
                if (CheckSelfVoteMode(Player, votedForId, out var status))
                {
                    if (status is VoteStatus.Self)
                        Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.Divied"), GetString("Vote.Divied")) + GetString("VoteSkillMode"), Player.PlayerId);
                    if (status is VoteStatus.Skip)
                        Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                    if (status is VoteStatus.Vote)
                        Uranai(votedForId);
                    SetMode(Player, status is VoteStatus.Self);
                    return false;
                }
            }
        }
        return true;
    }
    public void Uranai(byte votedForId)
    {
        int chance = IRandom.Instance.Next(1, 101);
        var target = PlayerCatch.GetPlayerById(votedForId);
        if (!target.IsAlive()) return;
        count++;
        mcount++;
        if (chance < collect)
        {

            Logger.Info($"Player: {Player.name},Target: {target.name}, count: {count}(成功)", "MadTeller");
            var FtR = target.GetRoleClass()?.GetFtResults(Player); //結果を変更するかチェック
            var role = FtR is not CustomRoles.NotAssigned ? FtR.Value : target.GetCustomRole();
            var s = "です" + (role.IsImpostorTeam() ? "!" : "...");
            SendRPC();
            Utils.SendMessage(string.Format(GetString("Skill.Teller"), Utils.GetPlayerColor(target, true), srole ? "<b>" + GetString($"{role}").Color(UtilsRoleText.GetRoleColor(role)) + "</b>" : GetString($"{role.GetCustomRoleTypes()}")) + s + $"\n\n" + (onemeetingmaximum != 0 ? string.Format(GetString("RemainingOneMeetingCount"), Math.Min(onemeetingmaximum - mcount, Max - count)) : string.Format(GetString("RemainingCount"), Max - count) + (Votemode == VoteMode.SelfVote ? "\n\n" + GetString("VoteSkillFin") : "")), Player.PlayerId);
        }
        else
        {
            Logger.Info($"Player: {Player.name},Target: {target.name}, count: {count}(失敗)", "MadTeller");
            SendRPC();
            Utils.SendMessage(string.Format(GetString("Skill.MadTeller"), Utils.GetPlayerColor(target, true)) + "\n\n" + (onemeetingmaximum != 0 ? string.Format(GetString("RemainingOneMeetingCount"), Math.Min(onemeetingmaximum - mcount, Max - count)) : string.Format(GetString("RemainingCount"), Max - count) + (Votemode == VoteMode.SelfVote ? "\n\n" + GetString("VoteSkillFin") : "")), Player.PlayerId);
        }
    }
    public override bool OnCompleteTask(uint taskid)
    {
        if (Check())
        {
            Player.MarkDirtySettings();
        }

        return true;
    }
}