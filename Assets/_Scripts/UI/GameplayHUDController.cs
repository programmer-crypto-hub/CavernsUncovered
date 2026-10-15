using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json.Bson;

public class GameplayHUDController : MonoBehaviour
{
    public static GameplayHUDController Instance { get; private set; }
    [Header("Data Sources")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerProgression playerProgression;
    [SerializeField] private WeaponManager weaponManager;

    [Header("HP UI Components")]
    [Tooltip("Иконка сердца. Должна иметь Image Type = Filled, Fill Method = Vertical, Fill Origin = Bottom")]
    [SerializeField] private Image hpFillImage;
    [SerializeField] public Image enemyHealthFillImage;

    [Header("Gold UI Components")]
    [SerializeField] private TextMeshProUGUI goldValueText;

    [Header("XP + Level UI Components")]
    [Tooltip("Полоса опыта. Должна иметь Image Type = Filled, Fill Method = Horizontal")]
    [SerializeField] private Image xpFillImage;
    [SerializeField] private TextMeshProUGUI levelValueText;

    [Header("Weapon HUD Components")]
    [SerializeField] private Image weaponIconImage;

    private bool _isBound = false;

    private void Start()
    {
        ResolveSourcesIfNeeded();
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void Render()
    {
        ResolveSourcesIfNeeded();
        if (playerStats == null || playerStats.Object == null || !playerStats.Object.IsValid) return;
        if (!_isBound)
        {
            BindEvents();
        }

        float maxHp = playerStats.playerData != null ? playerStats.playerData.maxHealth : 100f;
        UpdateHealthUI(playerStats.CurrentHealth, maxHp);

        if (playerStats.playerData != null)
        {
            UpdateGoldUI(playerStats.playerData.caveCoins, playerStats.playerData.maxCaveCoins);
        }

        if (playerProgression != null)
        {
            UpdateLevelUI(playerProgression.CurrentLevel);
            UpdateExperienceUI(playerProgression.CurrentXP, playerProgression.baseXPToNextLevel);
        }

        if (weaponManager != null && weaponManager.isNetworkReady)
        {
            UpdateWeaponUI(weaponManager.CurrentWeapon);
        }
    }

    private void ResolveSourcesIfNeeded()
    {
        if (playerStats == null)
        {
            foreach (var stats in FindObjectsByType<PlayerStats>(FindObjectsSortMode.None))
            {
                if (stats.Object != null && stats.Object.HasInputAuthority)
                {
                    playerStats = stats;
                    break;
                }
            }
        }

        if (playerStats != null)
        {
            if (playerProgression == null) playerProgression = playerStats.GetComponent<PlayerProgression>();
            if (weaponManager == null) weaponManager = playerStats.GetComponent<WeaponManager>();
        }
    }

    private void BindEvents()
    {
        if (_isBound) return;
        ResolveSourcesIfNeeded();

        if (playerStats != null)
        {
            GameManager.Events.OnHealthChanged += UpdateHealthUI;
            GameManager.Events.OnGoldChanged += UpdateGoldUI;
            _isBound = true;
        }

        if (playerProgression != null)
        {
            GameManager.Events.OnXPChanged += UpdateExperienceUI;
            GameManager.Events.OnLevelUp += UpdateLevelUI;
        }

        if (weaponManager != null)
            GameManager.Events.OnWeaponChanged += UpdateWeaponUI;
    }

    private void UnbindEvents()
    {
        if (!_isBound) return;

        if (playerStats != null)
        {
            GameManager.Events.OnHealthChanged -= UpdateHealthUI;
            GameManager.Events.OnGoldChanged -= UpdateGoldUI;
        }

        if (playerProgression != null)
        {
            GameManager.Events.OnXPChanged -= UpdateExperienceUI;
            GameManager.Events.OnLevelUp -= UpdateLevelUI;
        }

        if (weaponManager != null)
            GameManager.Events.OnWeaponChanged -= UpdateWeaponUI;

        _isBound = false;
    }

    private void UpdateHealthUI(float current, float max)
    {
        if (hpFillImage == null) return;
        hpFillImage.fillAmount = max > 0.01f ? Mathf.Clamp01(current / max) : 0f;
    }

    public void UpdateEnemyHealthUI(float current, float max)
    {
        if (enemyHealthFillImage == null) return;
        enemyHealthFillImage.fillAmount = max > 0.01f ? Mathf.Clamp01(current / max) : 0f;
    }   

    private void UpdateExperienceUI(float current, float required)
    {
        if (xpFillImage == null) return;
        xpFillImage.fillAmount = required > 0.01f ? Mathf.Clamp01(current / required) : 0f;
    }

    private void UpdateGoldUI(int currentGold, int maxGold)
    {
        if (currentGold > maxGold) currentGold = maxGold;
        if (goldValueText != null) goldValueText.text = currentGold.ToString();
    }

    private void UpdateLevelUI(int level)
    {
        if (playerProgression == null) return;

        int displayLevel = playerProgression.CurrentLevel;
        if (displayLevel >= playerProgression.maxLevel)
        {
            displayLevel = playerProgression.maxLevel;
        }

        if (levelValueText != null)
        {
            levelValueText.text = $"Level: {displayLevel.ToString()}";
        }

        if (xpFillImage != null)
        {
            float requiredXP = playerProgression.GetRequiredXPForNextLevel();
            float xpValue = requiredXP > 0.01f ? Mathf.Clamp01(playerProgression.CurrentXP / requiredXP) : 0f;

            if (playerProgression.CurrentLevel >= playerProgression.maxLevel)
            {
                xpValue = 1f;
            }

            xpFillImage.fillAmount = xpValue;
        }
    }

private void UpdateWeaponUI(WeaponBase weapon)
    {
        if (weaponIconImage == null) return;
        Sprite icon = weapon != null && weapon.weaponData != null ? weapon.weaponData.icon : null;
        weaponIconImage.enabled = icon != null;
        weaponIconImage.sprite = icon;
    }
}
