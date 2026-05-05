using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class B_PlayerDataHelper : MonoBehaviour
{
    private static B_PlayerDataHelper instance;
    private bool initialized = false;

    [Header("Player Variables - Assign these in Inspector")]
    [SerializeField, Required] private B_IntSO playerStarSO;
    [SerializeField, Required] private B_IntSO playerLevelSO;
    [SerializeField, Required] private B_IntSO playerItemHoleSO;
    [SerializeField, Required] private B_IntSO playerItemHintSO;
    [SerializeField, Required] private B_BoolSO playerAdsFreeSO;

    // Dictionary ánh xạ key → SO
    private readonly Dictionary<string, B_VariableSO_Base> itemMap = new();

    public static B_PlayerDataHelper Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<B_PlayerDataHelper>();
                if (instance == null)
                {
                    var obj = new GameObject("B_PlayerDataHelper");
                    instance = obj.AddComponent<B_PlayerDataHelper>();
                }
            }
            instance.Init();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void Init()
    {
        if (initialized) return;
        initialized = true;
        BuildItemMap();

        // Force load tất cả SO ngay khi Helper init → tránh lỗi convert data sau này
        ForceLoadAllVariables();
    }

    private void BuildItemMap()
    {
        itemMap.Clear();

        RegisterSO(playerStarSO);
        RegisterSO(playerLevelSO);
        RegisterSO(playerItemHoleSO);
        RegisterSO(playerItemHintSO);
        RegisterSO(playerAdsFreeSO);

        Debug.Log($"[B_PlayerDataHelper] Registered {itemMap.Count} player variables.");
    }

    private void RegisterSO(B_VariableSO_Base so)
    {
        if (so == null || string.IsNullOrEmpty(so.key)) return;

        if (itemMap.ContainsKey(so.key))
            Debug.LogWarning($"Duplicate key '{so.key}'");

        itemMap[so.key] = so;
    }

    // === Force load tất cả dữ liệu ngay từ đầu ===
    private void ForceLoadAllVariables()
    {
        foreach (var so in itemMap.Values)
        {
            if (so is B_VariableSO<int> intSO)
                _ = intSO.Value;        // trigger EnsureInitialized
            else if (so is B_VariableSO<bool> boolSO)
                _ = boolSO.Value;
            // Thêm các type khác nếu cần (float, string...)
        }
    }

    // ====================== Add methods ======================
    public void AddPlayerStar(int amount) => AddBySO(playerStarSO, amount, "Star");
    public void AddPlayerLevel(int amount) => AddBySO(playerLevelSO, amount, "Level");
    public void AddPlayerItemHole(int amount) => AddBySO(playerItemHoleSO, amount, "Hole");
    public void AddPlayerItemHint(int amount) => AddBySO(playerItemHintSO, amount, "Hint");
    public void SetPlayerAdsFree(bool value) { playerAdsFreeSO.Value = value; }

    public void AddItemById(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount == 0) return;

        if (itemMap.TryGetValue(id, out var so) && so is B_VariableSO<int> intSO)
        {
            int oldValue = intSO.Value;
            intSO.Value += amount;
            Debug.Log($"[PlayerData] Added {amount} to '{id}' | {oldValue} → {intSO.Value}");
        }
        else
        {
            Debug.LogWarning($"[B_PlayerDataHelper] Key '{id}' not found or not int type.");
        }
    }

    private void AddBySO(B_IntSO so, int amount, string displayName = "")
    {
        if (so == null || amount == 0) return;
        int oldValue = so.Value;
        so.Value += amount;
        Debug.Log($"[PlayerData] {displayName}: {oldValue} → {so.Value}");
    }

    public int GetPlayerStar() => playerStarSO?.Value ?? 0;
    public int GetPlayerLevel() => playerLevelSO?.Value ?? 0;
    public bool GetPlayerAdsFree() => playerAdsFreeSO?.Value ?? false;

    [Button("Test Add 50 Stars")]
    private void TestAddStars() => AddPlayerStar(50);
}