using Fusion;
using UnityEngine;

public class BossController : EnemyBase
{
    public int bossID;

    public override void Spawned()
    {
        base.Spawned();

        if (GameSession.Instance != null)
        {
            bossID = (int)(Object.Id.Raw) + 1000;
            GameSession.Instance.RegisterBoss(bossID);
            if (GameplayHUDController.Instance != null) base.HealthBar = GameplayHUDController.Instance.enemyHealthFillImage;
            AttackCooldown = default;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner == null || !Runner.IsServer || IsDead || _isActionInProgress) return;

        if (GameSession.Instance == null || GameSession.Instance.CurrentTurnID != bossID)
        {
            if (CurrentState != EnemyState.Idle) CurrentState = EnemyState.Idle;
            return;
        }

        StartCoroutine(PlayTurnSequenceRoutine());
    }
}
