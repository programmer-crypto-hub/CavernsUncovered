using UnityEngine;
using Fusion;
using System.Collections;

public class PlayerCombatController : NetworkBehaviour
{
    [Header("Connections")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private PlayerAnimationController playerAnimationController;

    [Header("Effects")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform attackEffectPoint;
    [SerializeField] private GameObject attackStartEffectPrefab;
    [SerializeField] private GameObject attackActionEffectPrefab;

    [Header("UI and SFX")]
    [SerializeField] private CanvasGroup blindingScreenGroup;
    [SerializeField] private AudioClip whooshSFX;
    [SerializeField] private AudioClip bladeHitSFX;

    [SerializeField] private float meleeAttackRange = 50f;

    private Vector3 _returnPosition;
    private bool _hasCollidedWithBoss;

    [Networked] private int AttackTriggerCount { get; set; }
    private int _localAttackCount;
    private bool _isAttackInProgress;

    private Transform _cachedBossTransform;

    public override void Spawned()
    {
        if (weaponManager == null || playerStats == null || playerAnimationController == null)
        {
            Debug.LogWarning("PlayerCombatController: One or more required components are missing! Assigning by default.");
            weaponManager = GetComponent<WeaponManager>();
            playerStats = GetComponent<PlayerStats>();
            playerAnimationController = GetComponent<PlayerAnimationController>();
        }
        GameObject blindingCanvasObject = GameObject.Find("BlindingScreen");
        blindingScreenGroup = GameObject.Find("BlindingScreen").GetComponent<CanvasGroup>();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            if (data.attackPressed && Object.HasInputAuthority)
            {
                if (CanStartAttack()) TryStartAttack();
            }

            if (data.diceRollPressed && DiceRoller.Instance != null)
            {
                DiceRoller.Instance.RequestRollDice();
            }

            if (data.endTurnPressed && GameSession.Instance != null)
            {
                GameSession.Instance.RPC_RequestEndTurn();
            }
        }
    }


    public override void Render()
    {
        // Обнаруживаем изменение сетевого счетчика атак и включаем визуал у всех клиентов
        if (AttackTriggerCount > _localAttackCount)
        {
            _localAttackCount = AttackTriggerCount;
            TryStartAttack();
        }
    }

    public bool TryStartAttack()
    {
        _hasCollidedWithBoss = false; // Сбрасываем флаг столкновения при каждой новой попытке атаки
        if (!Object.HasInputAuthority || !CanStartAttack()) return false;

        if (_cachedBossTransform != null)
        {
            var boss = FindFirstObjectByType<BossController>();
            if (boss != null)
            {
                _cachedBossTransform = boss.transform;
            }
        }
        // Инкрементируем сетевой счетчик (Хост примет это мгновенно)
        AttackTriggerCount++;
        Debug.Log("Incremented AttackTriggerCount. New value: " + AttackTriggerCount);
        _isAttackInProgress = true;
        _hasCollidedWithBoss = false;

        // Если это атака ближнего боя — запускаем плавный подлет к боссу перед взмахом меча!
        //if (ResolveAttackAnimationType() == PlayerAnimationController.AttackAnimationType.Melee && _cachedBossTransform != null)
        //{
        StartCoroutine(DashTowardsBossRoutine());
        //}

        return true;
    }

    private IEnumerator DashTowardsBossRoutine()
    {
        _hasCollidedWithBoss = false;

        GameObject bossGo = GameObject.Find("BossPrefab");
        if (bossGo == null)
        {
            foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name.Contains("Boss") && !go.name.Contains("Spawner")) { bossGo = go; break; }
            }
        }

        _returnPosition = transform.position;
        Vector3 targetPos = bossGo.transform.position;
        targetPos.y = transform.position.y;

        Vector3 directionToBoss = Vector3.left;
        directionToBoss.y = 0f;
        Vector3 dashDestination = targetPos + (directionToBoss * 10f);
        dashDestination.y = transform.position.y;

        if (blindingScreenGroup != null)
        {
            // Мгновенно делаем экран черным
            blindingScreenGroup.alpha = 1f;
        }

        // Играем звук телепорта / вжух
        if (whooshSFX != null)
        {
            Vector3 earPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(whooshSFX, earPosition, 1.0f); // 1.0f — максимальная громкость
        }

        // Ждем крошечную долю секунды (0.5с) в полной темноте для создания саспенса
        yield return new WaitForSeconds(0.5f);

        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            // Вызываем телепорта! Теперь Fusion сам переместит персонажа
            netTransform.Teleport(dashDestination);
        }

        transform.position = dashDestination;
        Debug.LogWarning($"[WARP TEST] Игрок прыгнул к боссу! Новая позиция в кадре: {transform.position}");

        if (Object.HasStateAuthority && weaponManager != null)
        {
            weaponManager.PerformCurrentWeaponAttack();
            RPC_PlayActionEffects();
        }

        if (bladeHitSFX != null)
        {
            Vector3 earPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
            AudioSource.PlayClipAtPoint(bladeHitSFX, earPosition, 1.0f);
        }

        if (bossGo.TryGetComponent<EnemyBase>(out var enemyBase))
            enemyBase.TakeDamage(0f);

        // Ждем еще 0.15 секунд, чтобы звук удара совпал со вспышкой VFX эффектов
        yield return new WaitForSeconds(0.15f);

        if (blindingScreenGroup != null)
        {
            blindingScreenGroup.alpha = 0f;
        }
        TriggerVisualAttack();
        yield return new WaitForSeconds(2f);

        ReturnToOriginRoutine();
    }

    private void ReturnToOriginRoutine()
    {
        Debug.Log("[COMBAT] Начинаем путь назад на исходную позицию.");

        Vector3 directionBack = (_returnPosition - transform.position).normalized;
        directionBack.y = 0f;

        Vector3 lookAtBossAngle = new Vector3(-90f, 90f, -90f);
        //Vector3 lookBackAngle = new Vector3(90f, -90f, 90f);
        if (directionBack != Vector3.zero)
        {
            transform.rotation = Quaternion.Euler(lookAtBossAngle);
        }

        var netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.Teleport(_returnPosition);
            Debug.LogWarning($"[WARP SUCCESS] Рыцарь успешно вернулся в сеть на точку: {_returnPosition}");
        }
        else
        {
            transform.position = _returnPosition;
        }
        if (playerStats != null && playerStats.playerAnim != null)
        {
            playerStats.playerAnim.Play("Idle");
        }

        _isAttackInProgress = false;

        if (GameSession.Instance != null && Object.HasStateAuthority)
        {
            Debug.LogWarning("[COMBAT SUCCESS] Рыцарь вернулся на базу, ход передан боссу!");
        }
    }



    // 3. СИЛОВОЙ ПЕРЕХВАТ СТОЛКНОВЕНИЯ
    // Добавьте этот метод строго внутрь класса PlayerCombatController!
    private void OnCollisionEnter(Collision collision)
    {
        // Если мы летим атаковать и врезаемся в объект Врага или Босса
        if (_isAttackInProgress && !_hasCollidedWithBoss)
        {
            if (collision.gameObject.CompareTag("Enemy") || collision.gameObject.name.Contains("Boss"))
            {
                // Этот флаг мгновенно выкидывает код из цикла while в корутине!
                _hasCollidedWithBoss = true;
                Debug.LogWarning($"[PHYSICS IMPACT] Рыцарь физически коснулся тела босса ({collision.gameObject.name}). Бьем!");
            }
        }
    }

    private void TriggerVisualAttack()
    {
        var type = ResolveAttackAnimationType();
        var playerAnim = GetComponent<Animator>();
        Debug.Log("Triggering visual attack. Animation type: " + type);
        if (playerAnimationController != null)
        {
            //playerAnimationController.PlayAttack(type);
            playerAnim.SetTrigger("attack_trig");
        }

        // Воспроизводим локальный звук взмаха
        if (audioSource != null)
        {
            // audioSource.PlayOneShot(attackClip);
        }
    }

    public void HandleAttackActionAnimationEvent()
    {
        if (!Object.HasStateAuthority) return;

        if (weaponManager != null)
        {
            // Вызываем ваш готовый метод менеджера оружия!
            // Он найдет босса через OverlapSphere и нанесет урон по интерфейсу IDamageable
            weaponManager.PerformCurrentWeaponAttack();

            // Передаем всем клиентам сигнал спавнить искры/взрыв удара на боссе
            RPC_PlayActionEffects();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayActionEffects()
    {
        // Спавн искр от удара мечом на всех машинах
        if (attackActionEffectPrefab != null && attackEffectPoint != null)
        {
            Instantiate(attackActionEffectPrefab, attackEffectPoint.position, attackEffectPoint.rotation);
        }
        Debug.Log("Sword Hit VFX played.");
    }

    // ВЫЗЫВАЕТСЯ ВАШИМ ВТОРЫМ UNITY ANIMATION EVENT (В самом конце анимации взмаха)
    public void HandleAttackFinishedAnimationEvent()
    {
        _isAttackInProgress = false;
    }
    private bool CanStartAttack()
    {
        if (weaponManager == null || playerStats == null || playerStats.IsDead || _isAttackInProgress)
        {
            Debug.Log($"Can't start attack. Required components are null; WeaponManager: {weaponManager == null}, PlayerStats: {playerStats == null}" +
                $"or the attack is in progress: {_isAttackInProgress}");
            return false;
        }
        Debug.Log($"Check: CanAttack? {weaponManager.CurrentWeapon?.CanAttack() ?? false}");

        var weapon = weaponManager.CurrentWeapon;
        Debug.Log("Can start attack.");
        return weapon != null && weapon.CanAttack();
    }

    private PlayerAnimationController.AttackAnimationType ResolveAttackAnimationType()
    {
        //if (weaponManager == null || weaponManager.CurrentWeapon == null)
        //    return PlayerAnimationController.AttackAnimationType.Melee;

        //var weapon = weaponManager.CurrentWeapon;
        //if (weapon is RangedWeapon) return PlayerAnimationController.AttackAnimationType.Ranged;

        return PlayerAnimationController.AttackAnimationType.Melee;
    }
}
