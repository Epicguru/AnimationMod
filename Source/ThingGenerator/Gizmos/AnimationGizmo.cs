﻿using AAM.Data;
using AAM.Grappling;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AAM.Gizmos;

public class AnimationGizmo : Gizmo
{
    private readonly Pawn pawn;
    private readonly PawnMeleeData data;
    private readonly List<Pawn> pawns = new List<Pawn>();
    private readonly HashSet<AnimDef> except = new HashSet<AnimDef>();

    private bool forceDontUseLasso;

    public AnimationGizmo(Pawn pawn)
    {
        this.pawn = pawn;
        this.data = Current.Game.GetComponent<GameComp>().GetOrCreateData(pawn); // TODO optimize.
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        if(pawns.Count == 0)
            return DrawSingle(topLeft, maxWidth, parms);

        var butRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);

        Text.Font = GameFont.Tiny;
        MouseoverSounds.DoRegion(butRect, SoundDefOf.Mouseover_Command);

        Widgets.DrawBoxSolidWithOutline(butRect, new Color32(21, 25, 29, 255), Color.white * 0.75f);

        DrawAutoExecute(butRect.LeftHalf().TopHalf());
        DrawAutoGrapple(butRect.LeftHalf().BottomHalf());

        return default;
    }

    public override bool GroupsWith(Gizmo other)
    {
        return other is AnimationGizmo g && g != this;
    }

    public override void MergeWith(Gizmo raw)
    {
        if (raw is not AnimationGizmo other)
            return;

        pawns.Add(other.pawn);
    }

    private GizmoResult DrawSingle(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
        var butRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
        try
        {
            Text.Font = GameFont.Tiny;

            // Mouseover and highlight.
            MouseoverSounds.DoRegion(butRect, SoundDefOf.Mouseover_Command);

            bool mouseOver = Mouse.IsOver(butRect);

            Widgets.DrawBoxSolidWithOutline(butRect, new Color32(21, 25, 29, 255), Color.white * 0.75f);

            Rect left = butRect.ExpandedBy(-2).LeftPartPixels(34);
            Rect tl = left.TopPartPixels(34);
            Rect bl = left.BottomPartPixels(34);

            Rect right = butRect.ExpandedBy(-2).RightPartPixels(34);
            Rect tr = right.TopPartPixels(34);
            Rect br = right.BottomPartPixels(34);

            Rect middle = butRect.ExpandedBy(-2).ExpandedBy(-36, 0);

            Rect tm = middle.TopPartPixels(34).ExpandedBy(-2, 0);
            Rect bm = middle.BottomPartPixels(34).ExpandedBy(-2, 0);

            DrawAutoExecute(tl);
            DrawAutoGrapple(bl);
            DrawInfo(tr);
            DrawSkill(br);
            DrawExecuteTarget(tm);
            DrawLassoTarget(bm);

            string pawnName = null;
            if (Find.Selector.SelectedPawns.Count > 1)
                pawnName = pawn.NameShortColored;
            string labelCap = $"{"AAM.Gizmo.Title".Trs()}{(pawnName != null ? $"({pawnName})" : "")}";

            if (!labelCap.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                float num = Text.CalcHeight(labelCap, butRect.width);
                Rect rect2 = new(butRect.x, butRect.yMax - num + 12f, butRect.width, num);
                GUI.DrawTexture(rect2, TexUI.GrayTextBG);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(rect2, labelCap);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            TooltipHandler.TipRegion(butRect, string.Join(", ", pawns));

            GUI.color = Color.white;

            bool HasLos(IntVec3 c)
            {
                return GenSight.LineOfSight(pawn.Position, c, pawn.Map);
            }

            // Note: despite best efforts, may still display radius for other custom targeting operations when this pawn is selected.
            if (Event.current.type == EventType.Repaint && Find.Targeter.IsTargeting && Find.Targeter.IsPawnTargeting(pawn) && Find.Targeter.targetingSource == null)
            {
                if (pawn.TryGetLasso() != null)
                {
                    pawn.GetAnimManager().AddPostDraw(() =>
                    {
                        float radius = pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
                        GenDraw.DrawRadiusRing(pawn.Position, radius, Color.yellow, HasLos);
                    });
                }
            }

            var state = mouseOver ? GizmoState.Mouseover : GizmoState.Clear;
            return new GizmoResult(state, null);
        }
        catch (Exception e)
        {
            Text.Font = GameFont.Tiny;
            Widgets.DrawBoxSolid(butRect, Color.black);
            Widgets.Label(butRect, $"<color=red>Exception drawing AdvancedMelee gizmo:\n{e}</color>");
            Text.Font = GameFont.Small;
            Core.Error($"Exception drawing gizmo for {pawn.NameFullColored}!", e);
            return default;
        }
    }

    private void DrawIcon(Rect rect, Texture icon, Color color)
    {
        GUI.DrawTexture(rect, Content.IconBG);

        GUI.color = color;
        GUI.DrawTexture(rect, icon);
        GUI.color = Color.white;

        Widgets.DrawHighlightIfMouseover(rect);
    }

    private void DrawAutoExecute(Rect rect)
    {
        bool multi = pawns.Count > 0;
        bool mixed = false;
        if (multi)
        {
            var mode = data.AutoExecute;
            foreach (var other in pawns)
            {
                if (other.GetMeleeData().AutoExecute != mode)
                    mixed = true;
            }
        }

        AutoOption selected = data.AutoExecute;
        Color iconColor = selected switch
        {
            AutoOption.Default  => Color.grey,
            AutoOption.Enabled  => Color.green,
            AutoOption.Disabled => Color.red,
            _ => Color.magenta
        };
        if (mixed)
            iconColor = Color.yellow;

        DrawIcon(rect, Content.IconExecute, iconColor);

        string sThis = multi ? "these" : "this";
        string sPawns = multi ? $"{pawns.Count + 1} pawns" : "pawn";
        string sIs = multi ? $"are" : "is";
        string sHas = multi ? "have" : "has";
        var pawnCount = new NamedArgument(pawns.Count + 1, "PawnCount");

        string tooltip;
        if (mixed)
        {
            // pawns.Count + 1
            tooltip = "AAM.Gizmo.DifferingReset".Trs(pawnCount, new NamedArgument(data.AutoExecute, "Mode"));
        }
        else
        {
            string autoExecFromSettings = Core.Settings.AutoExecute ? "AAM.Gizmo.Enabled".Trs() : "AAM.Gizmo.Disabled".Trs();
            string autoExecFromSettingsColor = Core.Settings.AutoExecute ? "green" : "red";
            bool resolved = selected switch
            {
                AutoOption.Default => Core.Settings.AutoExecute,
                AutoOption.Enabled => true,
                AutoOption.Disabled => false,
                _ => throw new ArgumentOutOfRangeException()
            };
            string plural = multi ? "Plural" : "";
            string explanation = resolved
                ? $"AAM.Gizmo.Execute.ExplainResolved{plural}".Trs(pawnCount)
                : $"AAM.Gizmo.Execute.Explain{plural}".Trs(pawnCount);

            string mode = $"<color={autoExecFromSettingsColor}><b>{autoExecFromSettings}</b></color>";
            var modeArg = new NamedArgument(mode, "Mode");
            var explainationArg = new NamedArgument(explanation, "Explanation");

            tooltip = selected switch
            {
                AutoOption.Default  => $"AAM.Gizmo.Execute.Mode.Default{plural}".Trs(pawnCount, explainationArg, modeArg),
                AutoOption.Enabled  => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-execute <color=green><b>enabled</b></color>.\n\n{explanation}",
                AutoOption.Disabled => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-execute <color=red><b>disabled</b></color>.\n\n{explanation}",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        TooltipHandler.TipRegion(rect, tooltip);

        if (Widgets.ButtonInvisible(rect))
        {
            if (multi)
            {
                if(!mixed)
                    data.AutoExecute = (AutoOption)(((int)data.AutoExecute + 1) % 3);

                // Set all to first pawn's mode.
                foreach (var pawn in pawns)
                    pawn.GetMeleeData().AutoExecute = data.AutoExecute;

            }
            else
            {
                data.AutoExecute = (AutoOption) (((int) data.AutoExecute + 1) % 3);
            }
        }
    }

    private void DrawAutoGrapple(Rect rect)
    {
        bool multi = pawns.Count > 0;
        bool mixed = false;
        if (multi)
        {
            var mode = data.AutoGrapple;
            foreach (var other in pawns)
            {
                if (other.GetMeleeData().AutoGrapple != mode)
                    mixed = true;
            }
        }

        AutoOption selected = data.AutoGrapple;
        Color iconColor = selected switch
        {
            AutoOption.Default => Color.grey,
            AutoOption.Enabled => Color.green,
            AutoOption.Disabled => Color.red,
            _ => Color.magenta
        };
        if (mixed)
            iconColor = Color.yellow;

        DrawIcon(rect, Content.IconGrapple, iconColor);

        string sThis = multi ? "these" : "this";
        string sPawns = multi ? $"{pawns.Count + 1} pawns" : "pawn";
        string sIs = multi ? $"are" : "is";
        string sHas = multi ? "have" : "has";

        string tooltip;
        if (mixed)
        {
            tooltip = $"These {pawns.Count + 1} pawns have differing auto-lasso settings. Click to set them all to '{data.AutoGrapple}'.";
        }
        else
        {
            string autoExecFromSettings = Core.Settings.AutoGrapple ? "enabled" : "disabled";
            string autoExecFromSettingsColor = Core.Settings.AutoGrapple ? "green" : "red";
            bool resolved = selected switch
            {
                AutoOption.Default => Core.Settings.AutoGrapple,
                AutoOption.Enabled => true,
                AutoOption.Disabled => false,
                _ => throw new ArgumentOutOfRangeException()
            };
            string explanation = resolved
                ? $"This means that {sThis} {sPawns} will automatically pull enemies into melee range using an equipped lasso, whenever they can."
                : $"This means that {sThis} {sPawns} will <b>NOT</b> automatically lasso enemies.";

            tooltip = selected switch
            {
                AutoOption.Default  => $"{sThis.CapitalizeFirst()} {sPawns} {sIs} using the <b>default</b> auto-lasso setting, which is <color={autoExecFromSettingsColor}><b>{autoExecFromSettings}</b></color>.\n\n{explanation}",
                AutoOption.Enabled  => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-lasso <color=green><b>enabled</b></color>.\n\n{explanation}",
                AutoOption.Disabled => $"{sThis.CapitalizeFirst()} {sPawns} {sHas} auto-lasso <color=red><b>disabled</b></color>.\n\n{explanation}",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        TooltipHandler.TipRegion(rect, tooltip);

        if (Widgets.ButtonInvisible(rect))
        {
            if (multi)
            {
                if (!mixed)
                    data.AutoGrapple = (AutoOption)(((int)data.AutoGrapple + 1) % 3);

                // Set all to first pawn's mode.
                foreach (var pawn in pawns)
                    pawn.GetMeleeData().AutoGrapple = data.AutoGrapple;

            }
            else
            {
                data.AutoGrapple = (AutoOption)(((int)data.AutoGrapple + 1) % 3);
            }
        }
    }

    private void DrawInfo(Rect rect)
    {
        DrawIcon(rect, Content.IconInfo, Color.cyan);

        var weapon = pawn.GetFirstMeleeWeapon();
        // yes the " 1" is intentional: Rimworld doesn't account for RichText in text size calculation.
        string msg = $"Weapon: {(weapon == null ? "<i>None</i>" : weapon.LabelCap)}";

        TooltipHandler.TipRegion(rect, msg);
    }

    private void DrawSkill(Rect rect)
    {
        DrawIcon(rect, Content.IconSkill, Color.grey);

        TooltipHandler.TipRegion(rect, "<b>Skills:</b>\n\nThis weapon has no special skills available.\n\n<color=yellow>(Not implemented yet!)</color>");
    }

    private void DrawExecuteTarget(Rect rect)
    {
        GUI.DrawTexture(rect, Content.IconLongBG);

        bool isOffCooldown = data.IsExecutionOffCooldown();
        if (!isOffCooldown)
        {
            float pct = data.GetExecuteCooldownPct();
            Widgets.FillableBar(rect.BottomPartPixels(14).ExpandedBy(-4, -3), pct);
        }

        Widgets.DrawHighlightIfMouseover(rect);
        if (Widgets.ButtonInvisible(rect) && !Find.Targeter.IsTargeting)
        {
            if(!isOffCooldown)
            {
                string name = pawn.Name.ToStringShort;
                Messages.Message($"{name}'s execution is on cooldown!", MessageTypeDefOf.RejectInput, false);
            }
            else if (pawn.GetFirstMeleeWeapon() == null)
            {
                string name = pawn.Name.ToStringShort;
                Messages.Message($"{name} cannot execute anyone without a melee weapon!", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                forceDontUseLasso = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
                var args = new TargetingParameters()
                {
                    canTargetSelf = false,
                    canTargetBuildings = false,
                    validator = LassoTargetValidator
                };
                Find.Targeter.BeginTargeting(args, OnSelectedExecutionTarget, null, null, pawn);
            }
                
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, "Execute Target...");
        Text.Anchor = TextAnchor.UpperLeft;

        TooltipHandler.TipRegion(rect, "AAM.Gizmo.Execute.Tooltip".Trs());
    }

    private void DrawLassoTarget(Rect rect)
    {
        GUI.DrawTexture(rect, Content.IconLongBG);


        bool isOffCooldown = data.IsGrappleOffCooldown();
        if (!isOffCooldown)
        {
            float pct = data.GetGrappleCooldownPct();
            Widgets.FillableBar(rect.BottomPartPixels(14).ExpandedBy(-4, -3), pct);
        }

        Widgets.DrawHighlightIfMouseover(rect);

        if (Widgets.ButtonInvisible(rect) && !Find.Targeter.IsTargeting)
        {
            if(!GrabUtility.CanStartGrapple(pawn, out string reason, true))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                var args = new TargetingParameters()
                {
                    canTargetSelf = false,
                    canTargetBuildings = false,
                    validator = LassoTargetValidator
                };
                Find.Targeter.BeginTargeting(args, OnSelectedLassoTarget, null, null, pawn);
            }
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(rect, "Lasso Target...");
        Text.Anchor = TextAnchor.UpperLeft;

        TooltipHandler.TipRegion(rect, "Use a lasso to drag in a pawn.\nYou can lasso enemies or friendlies, even if they are downed.\nLassoing does not harm the target.");
    }

    private bool LassoTargetValidator(TargetInfo info)
    {
        return info.HasThing && info.Thing is Pawn { Dead: false, Spawned: true } p && p != pawn && (Core.Settings.AnimalsCanBeExecuted || !p.RaceProps.Animal);
    }

    private void OnSelectedLassoTarget(LocalTargetInfo info)
    {
        if (info.Thing is not Pawn target || target.Dead)
            return;

        if (target == pawn)
            return;

        var targetPos = target.Position;
        string lastReason = "AAM.Gizmo.Grapple.CannotLassoDefault".Translate(new NamedArgument("Target", target.NameShortColored));

        if (!GrabUtility.CanStartGrapple(pawn, out string reason, true))
        {
            string error = "AAM.Gizmo.Grapple.CannotLassoGeneric".Translate(
                new NamedArgument("Pawn", pawn.NameShortColored),
                new NamedArgument("Target", target.NameShortColored),
                new NamedArgument("Reason", reason.CapitalizeFirst()));

            Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return;
        }

        // Enumerates free cells in the 3x3 area around the pawn.
        // Prioritizing the cells closest to the target (minimize drag distance).
        foreach (var pos in GrabUtility.GetIdealGrappleSpots(pawn, target, false))
        {
            if (targetPos == pos)
                return; // Yes, return, not continue. If the target is already in a good spot, there is nothing to do.

            if (GrabUtility.CanStartGrapple(pawn, target, pos, out lastReason))
            {
                if (JobDriver_GrapplePawn.GiveJob(pawn, target, pos, true))
                    data.TimeSinceGrappled = 0;
                return;
            }
        }

        string error2 = "AAM.Gizmo.Grapple.CannotLassoGeneric".Translate(
            new NamedArgument("Pawn", pawn.NameShortColored),
            new NamedArgument("Target", target.NameShortColored),
            new NamedArgument("Reason", lastReason.CapitalizeFirst()));
        Messages.Message(error2, MessageTypeDefOf.RejectInput, false);
    }

    private void OnSelectedExecutionTarget(LocalTargetInfo info)
    {
        if (info.Thing is not Pawn target || target.Dead)
            return;
        if (target == pawn)
            return;

        if (!forceDontUseLasso)
            forceDontUseLasso = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        bool allowFriendlies = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (Core.Settings.WarnOfFriendlyExecution && !allowFriendlies && !target.AnimalOrWildMan() && !target.HostileTo(Faction.OfPlayerSilentFail))
        {
            Messages.Message("AAM.Gizmo.Grapple.AltForFriendlies".Trs(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        var lasso = pawn.TryGetLasso();
        bool canGrapple = !forceDontUseLasso && lasso != null && GrabUtility.CanStartGrapple(pawn, out _);
        IntVec3 targetPos = target.Position;

        // If grappling is enabled, but pawn is out of reach, send warning message
        // letting the player know they can shift+click to send the pawn walking.
        if (canGrapple)
        {
            float currDistanceSqr = (pawn.Position - targetPos).LengthHorizontalSquared;
            float maxDistance = pawn.GetStatValue(AAM_DefOf.AAM_GrappleRadius);
            if (maxDistance * maxDistance < currDistanceSqr)
            {
                Messages.Message("AAM.Gizmo.Grapple.OutOfRange".Translate(new NamedArgument(pawn.NameShortColored, "Pawn")), MessageTypeDefOf.RejectInput, false);
                return;
            }
        }

        var weaponDef = pawn.GetFirstMeleeWeapon().def;
        var possibilities = AnimDef.GetExecutionAnimationsForPawnAndWeapon(pawn, weaponDef);
        ulong occupiedMask = SpaceChecker.MakeOccupiedMask(pawn.Map, pawn.Position, out _);
            
        if (!possibilities.Any())
        {
            string reason = "AAM.Gizmo.Error.NoAnimations".Translate(
                new NamedArgument(weaponDef.LabelCap, "Weapon"),
                new NamedArgument(pawn.LabelShortCap, "Pawn"));

            string error = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
                
            Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            return;
        }

        // Step 1: Check if the pawn is in the left or right position.
        if (targetPos == pawn.Position - new IntVec3(1, 0, 0) ||
            targetPos == pawn.Position + new IntVec3(1, 0, 0))
        {
            // If the target is in one of these two spots, it may be a candidate for a simple immediate execution...
            bool flipX = targetPos.x < pawn.Position.x;

            // Check space...
            except.Clear();
            while (true)
            {
                // Pick random anim, weighted.
                var anim = possibilities.RandomElementByWeightExcept(d => d.Probability, except);
                if (anim == null)
                    break;

                except.Add(anim);

                // Do we have space for this animation?
                ulong animMask = flipX ? anim.FlipClearMask : anim.ClearMask;
                ulong result = animMask & occupiedMask; // The result should be 0.

                if (result == 0)
                {
                    // Can do the animation!
                    var startArgs = new AnimationStartParameters(anim, pawn, target)
                    {
                        ExecutionOutcome = ExecutionOutcome.Kill, // TODO derive from skill/weapon
                        FlipX = flipX,
                    };

                    if (startArgs.TryTrigger())
                    {
                        // Set execute cooldown.
                        data.TimeSinceExecuted = 0;
                    }
                    else
                    {
                        Core.Error($"Failed to start execution animation (instant) for pawn {pawn} on {target}.");
                    }

                    return;
                }
            }

            // At this point, the pawn is to the immediate left or right but none of the animations could be played due to space constraints.
            // Therefore, exit.
            Messages.Message("AAM.Gizmo.Error.NoSpace".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        // Step 2: The pawn needs to be grappled in because the target is not to the immediate left or right.
        if (!canGrapple)
        {
            // Cannot grapple.

            // Walk to and execute target.
            // Make walk job and reset all verbs (stop firing, attacking).
            var walkJob = JobMaker.MakeJob(AAM_DefOf.AAM_WalkToExecution, target);

            if (pawn.verbTracker?.AllVerbs != null)
                foreach (var verb in pawn.verbTracker.AllVerbs)
                    verb.Reset();


            if (pawn.equipment?.AllEquipmentVerbs != null)
                foreach (var verb in pawn.equipment.AllEquipmentVerbs)
                    verb.Reset();

            // Start walking.
            pawn.jobs.StartJob(walkJob, JobCondition.InterruptForced);

            if (pawn.CurJobDef == AAM_DefOf.AAM_WalkToExecution)
                return;

            Core.Error($"CRITICAL ERROR: Failed to force interrupt {pawn}'s job with execution goto job. Likely a mod conflict.");
            string reason = "AAM.Gizmo.Error.NoLasso".Translate(
                new NamedArgument(pawn.NameShortColored, "Pawn"),
                new NamedArgument(target.NameShortColored, "Target"));
            string error = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
            Messages.Message(error, MessageTypeDefOf.RejectInput, false);

            return;
        }

        // Try left and right position.
        foreach (var cell in GrabUtility.GetIdealGrappleSpots(pawn, target, true))
        {
            bool flipX = cell.x < pawn.Position.x;

            // Check line of sight and other details.
            if (!GrabUtility.CanStartGrapple(pawn, target, cell, out string lastReasonNoGrapple))
                continue;

            except.Clear();
            while (true)
            {
                // Pick random anim, weighted.
                var anim = possibilities.RandomElementByWeightExcept(d => d.Probability, except);
                if (anim == null)
                    break;

                except.Add(anim);

                // Do we have space for this animation?
                ulong animMask = flipX ? anim.FlipClearMask : anim.ClearMask;
                ulong result = animMask & occupiedMask; // The result should be 0.

                if (result == 0)
                {
                    // There is space for the animation, and line of sight for the grapple.
                    // So good to go.

                    var startParams = new AnimationStartParameters
                    {
                        Animation = anim,
                        ExecutionOutcome = ExecutionOutcome.Kill, // TODO decide based on stats, skill, weapon etc.
                        FlipX = flipX,
                        MainPawn = pawn,
                        SecondPawn = target,
                        ExtraPawns = null,
                        Map = pawn.Map,
                        RootTransform = pawn.MakeAnimationMatrix(),
                    };

                    // Give grapple job and pass in parameters which trigger the execution animation.
                    if (JobDriver_GrapplePawn.GiveJob(pawn, target, cell, true, startParams))
                        data.TimeSinceGrappled = 0;
                    else
                        Core.Error($"Failed to give grapple job to {pawn}.");

                    return;
                }
            }

            // Failed to lasso, or failed to find any space for any animation.
            if (lastReasonNoGrapple != null)
            {
                // Cannot lasso target.
                string reason = "AAM.Gizmo.Error.CannotLasso".Translate(
                    new NamedArgument(pawn.NameShortColored, "Pawn"),
                    new NamedArgument(target.NameShortColored, "Target"),
                    new NamedArgument(lastReasonNoGrapple, "Reason"));

                string error = "AAM.Gizmo.Execute.Fail".Translate(new NamedArgument(reason, "Reason"));
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                // No Space.
                Messages.Message("AAM.Gizmo.Error.NoSpace".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    public override float GetWidth(float maxWidth)
    {
        float target = pawns.Count == 0 ? 180 : 75;
        return Mathf.Min(target, maxWidth);
    }
}