using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class MeleeWeapon : WeaponBase
{
    private Transform attackOrigin;
    private float hitRadius = 1.5f;
    private LayerMask hitLayers;
    private NetworkObject player;

    public override void Spawned()
    {
        player = GetComponent<NetworkObject>();
    }
    public override void Attack()
    {
        if (!CanAttack() || owner != null && !player.HasInputAuthority) 
            return;

        StartAttackCooldown();

        if (weaponData == null)
        {
            Debug.LogWarning($"{name}: WeaponData isn't assigned.", this);
            return;
        }

        float radius = hitRadius > 0f ? hitRadius : Range;
        Vector3 origin = attackOrigin != null ? attackOrigin.position : (owner != null ? owner.position : transform.position);

        var animator = owner != null ? owner.GetComponentInChildren<Animator>() : null;
        if (animator != null)
        {
            animator.SetTrigger("attack_trig");
        }

        Collider[] hits = new Collider[10]; // Create a fixed size array to store hits.

        PhysicsScene networkPhysics = default;
        if (player != null && player.Runner != null)
        {
            networkPhysics = player.Runner.GetPhysicsScene();
        }

        int hitCount = 0;
        if (networkPhysics != null)
        {
            hitCount = networkPhysics.OverlapSphere(origin, radius, hits, hitLayers, QueryTriggerInteraction.UseGlobal);
        }
        else
        {
            hitCount = Physics.OverlapSphereNonAlloc(origin, radius, hits, hitLayers);
        }

        if (hitCount == 0)
        {
            Debug.Log($"{name}: melee attack — nobody was hit.");
            return;
        }
        Debug.Log($"{name}: close-range attack, hit {hitCount} object(s).");
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        for (int i = 0; i < hitCount; i++)
        {
             Collider collider = hits[i];
             if (collider == null) continue;

             Debug.Log($"Hit network object: {collider.name}");

             IDamageable damageable = collider.GetComponent<IDamageable>();
             if (damageable == null) damageable = collider.GetComponentInParent<IDamageable>();

             if (damagedTargets.Add(damageable)) damageable.TakeDamage(Damage);
        }
    }
}
