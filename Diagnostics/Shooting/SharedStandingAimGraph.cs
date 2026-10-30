using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;

public static class SharedStandingAimGraph
{
    public static void Configure(AnimatorController controller)
    {
        var layer = controller.layers.Single(l => l.name == "Weapon Actions");
        var machine = layer.stateMachine;
        string[] retained = { "Weapon Lowered", "Rifle Aiming Idle", "Firing Rifle2" };
        string[] moving = { "Walking", "Firing Rifle" };
        if (machine.stateMachines.Length != 0 || machine.anyStateTransitions.Length != 0 ||
            retained.Any(n => !machine.states.Any(s => s.state.name == n)) ||
            machine.states.Any(s => !retained.Contains(s.state.name) && !moving.Contains(s.state.name)))
            throw new Exception("Weapon Actions differs from the inspected graph; capture it before changing it.");
        foreach (var child in machine.states)
            foreach (var t in child.state.transitions.ToArray())
            {
                if (moving.Contains(child.state.name) || (t.destinationState != null && moving.Contains(t.destinationState.name)))
                    child.state.RemoveTransition(t);
                else
                    foreach (var condition in t.conditions.Where(c => c.parameter == "iswalking").ToArray()) t.RemoveCondition(condition);
            }
        foreach (var child in machine.states.ToArray())
            if (moving.Contains(child.state.name)) machine.RemoveState(child.state);
        if (machine.states.Sum(s => s.state.transitions.Length) != 6) throw new Exception("Expected six retained aim/fire transitions.");
        EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }
}
