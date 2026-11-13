using UnityEngine;
using UnityEngine.Events;

public interface IRifleDamageable { void TakeDamage(float amount); }

public class RifleDamageableTarget : MonoBehaviour, IRifleDamageable
{
    [Min(1f)] public float maxHealth = 100f;
    public bool deactivateOnDeath = true;
    public UnityEvent onDeath;
    public float Health { get; private set; }
    void OnEnable() { Health = Mathf.Max(1f, maxHealth); }
    public void TakeDamage(float amount)
    {
        if (Health <= 0f || amount <= 0f) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Health > 0f) return;
        onDeath?.Invoke();
        if (deactivateOnDeath) gameObject.SetActive(false);
    }
}
