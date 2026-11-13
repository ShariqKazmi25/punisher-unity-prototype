using UnityEngine;

[CreateAssetMenu(menuName = "Punisher/Rifle Weapon Animation")]
public class RifleWeaponAnimationSettings : ScriptableObject
{
    public AnimationClip shootingClip;
    [Tooltip("The rifle body bone carrying the grips and barrel, relative to the rifle root.")]
    public string bodyBonePath = "Armature.003/Bone";
}
