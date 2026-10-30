using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class WeaponAnimatorAudit
{
    static WeaponAnimatorAudit() { EditorApplication.delayCall += Capture; }
    [MenuItem("Tools/Punisher/Capture Animator Audit")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists("Diagnostics/Shooting/live-animator-before.txt")) return;
        try
        {
            var report = new StringBuilder(DateTime.Now.ToString("O") + "\n");
            foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                report.AppendLine("Scene Animator " + animator.name + ": " + AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" }))
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
                report.AppendLine("CONTROLLER " + AssetDatabase.GetAssetPath(controller) + " dirty=" + EditorUtility.IsDirty(controller));
                foreach (var p in controller.parameters) report.AppendLine("PARAM " + p.name + " " + p.type);
                foreach (var layer in controller.layers)
                {
                    report.AppendLine("LAYER " + layer.name + " weight=" + layer.defaultWeight + " blend=" + layer.blendingMode + " IK=" + layer.iKPass + " mask=" + AssetDatabase.GetAssetPath(layer.avatarMask));
                    Dump(layer.stateMachine, report, "  ");
                }
            }
            Directory.CreateDirectory("Diagnostics/Shooting");
            File.WriteAllText("Diagnostics/Shooting/live-animator-before.txt", report.ToString());
        }
        catch (Exception e) { File.WriteAllText("Diagnostics/Shooting/live-animator-audit-error.txt", e.ToString()); }
    }
    static void Dump(AnimatorStateMachine machine, StringBuilder report, string indent)
    {
        report.AppendLine(indent + "MACHINE " + machine.name + " default=" + machine.defaultState?.name);
        foreach (var child in machine.states)
        {
            var s = child.state;
            report.AppendLine(indent + "STATE " + s.name + " motion=" + s.motion?.name + " path=" + AssetDatabase.GetAssetPath(s.motion) + " speed=" + s.speed + " writeDefaults=" + s.writeDefaultValues);
            foreach (var t in s.transitions) DumpTransition(s.name, t, report, indent);
        }
        foreach (var t in machine.anyStateTransitions) DumpTransition("Any State", t, report, indent);
        foreach (var child in machine.stateMachines) Dump(child.stateMachine, report, indent + "  ");
    }
    static void DumpTransition(string source, AnimatorStateTransition t, StringBuilder report, string indent)
    {
        report.AppendLine(indent + source + " -> " + t.destinationState?.name + " conditions=" + string.Join(" AND ", t.conditions.Select(c => c.parameter + ":" + c.mode + ":" + c.threshold)) + " duration=" + t.duration + " fixed=" + t.hasFixedDuration + " exit=" + t.hasExitTime + ":" + t.exitTime + " offset=" + t.offset + " interrupt=" + t.interruptionSource + " ordered=" + t.orderedInterruption);
    }
}
