// One-shot Editor migration, archived here after running in the user's live Editor.
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class RepairWeaponAnimator
{
    const string Request = "Diagnostics/Shooting/repair-weapon-animator.request";
    const string ControllerPath = "Assets/punisher/animations/Punisher.controller";
    static readonly string[] Names = { "Weapon Lowered", "Rifle Aiming Idle", "Walking", "Firing Rifle", "Firing Rifle2" };
    static RepairWeaponAnimator() { EditorApplication.delayCall += Run; }
    static void Run()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Repair(); }
        catch (Exception e) { File.WriteAllText("Diagnostics/Shooting/animator-repair-error.txt", e.ToString()); Debug.LogException(e); }
    }
    static void Repair()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers.Length != 1) throw new Exception("Expected the inspected single-layer controller; re-audit before changing it.");
        var baseLayer = controller.layers[0];
        var machine = baseLayer.stateMachine;
        string[] locomotion = { "Rifle Idle", "Rifle Walk", "Rifle Walk To Stop" };
        string[] weapon = Names.Skip(1).Concat(new[] { "Rifle Down To Aim", "Rifle Aim To Down" }).ToArray();
        if (machine.stateMachines.Length != 0 || locomotion.Any(n => !machine.states.Any(s => s.state.name == n)) ||
            machine.states.Any(s => !locomotion.Contains(s.state.name) && !weapon.Contains(s.state.name)))
            throw new Exception("The live Base Layer differs from the inspected graph. No migration applied.");
        var clips = Names.Select(n =>
        {
            string clipName = n == Names[0] ? "Rifle Idle" : n;
            string path = clipName == "Rifle Idle" ? "Assets/punisher/punisher_vest@Rifle Idle.fbx" : "Assets/punisher/animations/punisher_vest@" + clipName + ".fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            if (clip == null || !clip.humanMotion || !clip.isLooping) throw new Exception("Expected a looping Humanoid clip: " + path);
            return clip;
        }).ToArray();

        // Save the live, unsaved user graph before backing it up; copying the old
        // disk file alone would omit the user's newly wired shooting states.
        AssetDatabase.SaveAssetIfDirty(controller);
        File.Copy(ControllerPath, "Diagnostics/Shooting/Punisher-before-direct-aim.controller", false);
        Undo.RegisterCompleteObjectUndo(controller, "Set up direct rifle aiming");
        foreach (var child in machine.states)
            foreach (var transition in child.state.transitions.ToArray())
                if (transition.destinationState != null &&
                    (!locomotion.Contains(child.state.name) || !locomotion.Contains(transition.destinationState.name)))
                    child.state.RemoveTransition(transition);
        foreach (var child in machine.states.ToArray())
            if (!locomotion.Contains(child.state.name)) machine.RemoveState(child.state);

        foreach (string name in new[] { "isaiming", "isfiring" })
        {
            var existing = controller.parameters.FirstOrDefault(p => p.name == name);
            if (existing != null && existing.type != AnimatorControllerParameterType.Bool) throw new Exception(name + " must be a Bool.");
            if (existing == null) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        }
        var mask = new AvatarMask { name = "RifleUpperBody" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm,
            AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
            AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK }) mask.SetHumanoidBodyPartActive(part, true);
        AssetDatabase.CreateAsset(mask, "Assets/punisher/animations/RifleUpperBody.mask");
        var upper = new AnimatorStateMachine { name = "Weapon Actions" };
        AssetDatabase.AddObjectToAsset(upper, controller);
        controller.AddLayer(new AnimatorControllerLayer { name = "Weapon Actions", stateMachine = upper,
            avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override, defaultWeight = 0f, iKPass = true });
        var positions = new[] { new Vector3(40, 190), new Vector3(340, 80), new Vector3(340, 310), new Vector3(680, 310), new Vector3(680, 80) };
        var states = Names.Select((n, i) => upper.AddState(n, positions[i])).ToArray();
        upper.defaultState = states[0]; upper.entryPosition = new Vector3(40, 50); upper.anyStatePosition = new Vector3(40, -40);
        for (int i = 0; i < states.Length; i++) { states[i].motion = clips[i]; states[i].writeDefaultValues = false; }
        var table = new StringBuilder("| From | To | Conditions (AND) | Duration (s) |\n|---|---|---|---:|\n");
        for (int from = 0; from < states.Length; from++)
            foreach (int to in new[] { 3, 4, 2, 1, 0 })
            {
                if (from == to) continue;
                var t = states[from].AddTransition(states[to]);
                t.hasExitTime = false; t.exitTime = 0f; t.hasFixedDuration = true;
                t.duration = to >= 3 ? .04f : .1f; t.offset = 0f; t.canTransitionToSelf = false;
                t.interruptionSource = TransitionInterruptionSource.Destination; t.orderedInterruption = true;
                if (to >= 3) { Condition(t, "isfiring", true); Condition(t, "iswalking", to == 3); }
                else
                {
                    Condition(t, "isfiring", false); Condition(t, "isaiming", to != 0);
                    if (to != 0) Condition(t, "iswalking", to == 2);
                }
                table.AppendLine("| " + Names[from] + " | " + Names[to] + " | " + string.Join(", ", t.conditions.Select(c => c.parameter + "=" + (c.mode == AnimatorConditionMode.If ? "true" : "false"))) + " | " + t.duration + " |");
            }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(mask);
        AssetDatabase.SaveAssetIfDirty(mask); AssetDatabase.SaveAssetIfDirty(controller);
        File.WriteAllText("Diagnostics/Shooting/ShooterTransitions.md", "# Direct aiming and firing transitions\n\nThe saved Weapon Actions layer has five states and 20 explicit transitions. For every row: Has Exit Time OFF, Fixed Duration ON, Offset 0, Interruption Source Next State, Ordered Interruption ON, Can Transition To Self OFF. No Any State routes. Weapon Lowered uses Rifle Idle; it is a neutral fallback, not a lowering animation.\n\n" + table);
        File.WriteAllText("Diagnostics/Shooting/animator-repair-complete.txt", "Saved direct-aim controller with the original three-state locomotion layer and five-state masked Weapon Actions layer. Live user graph backup: Punisher-before-direct-aim.controller.\n");
        Debug.Log("Punisher: direct aiming/shooting layer saved; calibrated locomotion preserved.");
    }
    static void Condition(AnimatorStateTransition t, string parameter, bool value)
        => t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
}
