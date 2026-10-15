using System;
using System.Collections;
using UnityEngine;
using Fusion;
using UnityEngine.UI;

public class EnemyBase : NetworkBehaviour, IDamageable
{
    protected enum EnemyState { Chase, Attack, Dead, Idle }

    [Header("Enemy Data")]
    [SerializeField] public EnemyData enemyData;
    [SerializeField] public Animator enemyAnim;

    [Networked, OnChangedRender(nameof(OnBossHPChanged))] 
    public float CurrentHP { get; set; }
    [Networked] protected EnemyState CurrentState { get; set; }
    [Networked] protected TickTimer AttackCooldown { get; set; }

    private Transform _target;
    private ChangeDetector _changes;

    protected Vector3 _enemyReturnPosition;
    protected bool _isActionInProgress = false;

    public Image HealthBar;
    public bool IsDead => CurrentState == EnemyState.Dead;

    public override void Spawned()
    {
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            CurrentHP = enemyData != null ? enemyData.maxHealth : 100f;
            CurrentState = EnemyState.Idle;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Runner == null || !Runner.IsServer || IsDead || _isActionInProgress) return;

        if (_target == null)
        {
            FindClosestPlayer();
            return;
        }
    }

    protected IEnumerator PlayTurnSequenceRoutine()
    {
        _isActionInProgress = true;
        _enemyReturnPosition = transform.position; 

        if (_target == null)
        {
            FindClosestPlayer();
            FinishTurnSequence();
            yield break;
        }

        float attackRange = 50f;
        float distanceToTarget = Vector3.Distance(transform.position, _target.position);

        if (distanceToTarget > attackRange)
        {
            CurrentState = EnemyState.Chase;
            if (enemyAnim != null) enemyAnim.SetTrigger("walk_trig");

            while (distanceToTarget > attackRange && _target != null)
            {
                Vector3 direction = (_target.position - transform.position).normalized;
                direction.y = 0f;

                float giantSpeed = enemyData != null ? enemyData.moveSpeed : 350f;
                transform.position += direction * giantSpeed * Time.deltaTime;

                if (direction != Vector3.zero)
                {
                    Vector3 flatLook = Quaternion.LookRotation(direction).eulerAngles;
                    transform.rotation = Quaternion.Euler(-90f, flatLook.y, 0f);
                }

                distanceToTarget = Vector3.Distance(transform.position, _target.position);
                yield return null;
            }
        }

        if (_target != null)
        {
            CurrentState = EnemyState.Attack;
            if (enemyAnim != null) enemyAnim.SetTrigger("attack_trig");

            if (_target.TryGetComponent<IDamageable>(out var player))
            {
                player.TakeDamage(enemyData != null ? enemyData.damage : 15f);
            }
        }

        yield return new WaitForSeconds(2.5f);

        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.Teleport(_enemyReturnPosition);
        }
        else
        {
            transform.position = _enemyReturnPosition;
        }

        if (_target != null)
        {
            Vector3 facePlayerDir = (_target.position - transform.position).normalized;
            facePlayerDir.y = 0f;
            if (facePlayerDir != Vector3.zero)
            {
                Vector3 flatLook = Quaternion.LookRotation(facePlayerDir).eulerAngles;
                transform.rotation = Quaternion.Euler(-90f, flatLook.y, 90f);
            }
        }

        FinishTurnSequence();
    }

    private void FinishTurnSequence()
    {
        CurrentState = EnemyState.Idle;
        if (enemyAnim != null) enemyAnim.SetTrigger("idle_trig");

        _isActionInProgress = false;

        if (GameSession.Instance != null && Object.HasStateAuthority)
        {
            GameSession.Instance.AdvanceFromBossTurn();
            Debug.LogWarning("Boss finished its turn.");
        }
    }

    public void FindClosestPlayer()
    {
        float closestDistance = float.MaxValue;
        PlayerController closestPlayer = null;

        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = player;
            }
        }

        if (closestPlayer != null)
        {
            _target = closestPlayer.transform;
        }
    }

    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(CurrentState)) UpdateAnimations();
        }
    }
    private void UpdateAnimations()
    {
        if (enemyAnim == null) return;
        switch (CurrentState)
        {
            case EnemyState.Chase: enemyAnim.SetTrigger("walk_trig"); break;
            case EnemyState.Attack: enemyAnim.SetTrigger("attack_trig"); break;
        }
    }

    // From IDamageable
    public void TakeDamage(float damage)
    {
        Debug.Log("Starting to take damage...");
        // Урон на сервере имеет право обрабатывать исключительно Хост/Сервер
        if (Runner == null || !Runner.IsServer || IsDead) return;

        float finalDamage = 0f;
        if (DiceRoller.Instance != null && DiceRoller.Instance.GetDiceResult() > 0)
        {
            damage = DiceRoller.Instance.ConvertDiceToCombat();
            finalDamage = damage * (enemyData != null ? enemyData.damage : 10f);
            Debug.Log($"Boss is taking damage from dice! Dice result: {DiceRoller.Instance.GetDiceResult()}, converted damage: {damage}, final damage after multiplier: {finalDamage}");
        }
        // Вычитаем динамический урон из здоровья босса
        CurrentHP -= finalDamage;
        Debug.LogWarning($"[SERVER COMBAT] Босс получил урон с КУБИКА: {finalDamage}. Осталось HP: {CurrentHP}");
        Debug.LogWarning($"Check? Did the enemy take damage? If not, why? Damage is: {finalDamage}; DiceRoller is null? {DiceRoller.Instance == null}, or diceRollResult isn't more than 0? {DiceRoller.Instance.GetDiceResult() > 0}");
        // ДИНАМИЧЕСКИЙ ОБНОВИТЕЛЬ ИНТЕРФЕЙСА:
        // Находим менеджер UI на сцене прямо в секунду удара, обходя пустые поля инспектора префаба!
        var hud = FindFirstObjectByType<GameplayHUDController>();
        if (hud != null)
        {
            float maxHP = enemyData != null ? enemyData.maxHealth : 100f;

            // Вызываем ваш готовый метод обновления картинки-сердца босса!
            hud.UpdateEnemyHealthUI(CurrentHP, maxHP);
            Debug.Log("UI updated!");
        }
        else
        {
            Debug.LogError("[UI ERROR] Не удалось динамически найти GameplayHUDController на сцене подземелья!");
        }

        if (CurrentHP <= 0f) Die();
    }

    private void OnBossHPChanged()
    {
        if (HealthBar != null)
        {
            HealthBar.fillAmount = CurrentHP / enemyData.maxHealth;
        }
    }
    private void Die()
    {
        CurrentState = EnemyState.Dead;
        GameManager.Instance.RaiseEvent(GameManager.Events.OnEnemyDied);
        Invoke(nameof(DespawnBoss), 2f);
    }
    private void DespawnBoss()
    {
        if (Object != null && Runner != null) Runner.Despawn(Object);
    }
}
