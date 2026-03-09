using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    // ── 道具数据（跨场景持久） ────────────────────────────────
    [Header("Item Database（跨场景）")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    /// <summary>道具收集事件</summary>
    public event Action<ItemData> OnItemCollected;

    // ── 证据 & 庭审数据（Court 场景使用） ─────────────────────
    [Header("Evidence Database（庭审用）")]
    [SerializeField] private List<EvidenceData> allEvidence = new List<EvidenceData>();

    [Header("Court Topics")]
    [SerializeField] private List<CourtTopic> courtTopics = new List<CourtTopic>();

    private Dictionary<string, int> topicPersuasion = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 由 GameBootstrapper 创建时，父对象已标记 DontDestroyOnLoad，子对象自动继承
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeCourtData();
    }

    // =========================
    // ��ʼ��
    // =========================

    private void InitializeCourtData()
    {
        topicPersuasion.Clear();

        foreach (var topic in courtTopics)
        {
            topicPersuasion[topic.id] = 0;
        }
    }

    // =========================
    // Evidence ���
    // =========================

    public void UnlockEvidence(string evidenceId)
    {
        EvidenceData evidence = allEvidence.Find(e => e.id == evidenceId);
        if (evidence == null)
        {
            Debug.LogWarning($"[DataManager] Evidence not found: {evidenceId}");
            return;
        }

        evidence.isUnlocked = true;
        Debug.Log($"[DataManager] Evidence Unlocked: {evidence.title}");
    }

    public List<EvidenceData> GetUnlockedEvidence()
    {
        return allEvidence.Where(e => e.isUnlocked).ToList();
    }

    public EvidenceData GetEvidenceById(string id)
    {
        return allEvidence.Find(e => e.id == id);
    }

    // =========================
    // Item 接口（跨场景）
    // =========================

    /// <summary>收集道具</summary>
    public void CollectItem(string itemId)
    {
        ItemData item = allItems.Find(i => i.id == itemId);
        if (item == null)
        {
            Debug.LogWarning($"[DataManager] Item not found: {itemId}");
            return;
        }
        if (item.isCollected) return;

        item.isCollected = true;
        item.collectTime = Time.time;
        Debug.Log($"[DataManager] Item Collected: {item.title}");
        OnItemCollected?.Invoke(item);

        // 同步到 ItemDisplaySystem
        UIManager.Instance?.ItemDisplay?.AddItem(item);
    }

    /// <summary>检查是否已收集</summary>
    public bool HasItem(string itemId)
    {
        return allItems.Exists(i => i.id == itemId && i.isCollected);
    }

    /// <summary>获取所有已收集道具</summary>
    public List<ItemData> GetCollectedItems()
    {
        return allItems.Where(i => i.isCollected).ToList();
    }

    /// <summary>通过 ID 获取道具数据</summary>
    public ItemData GetItemById(string id)
    {
        return allItems.Find(i => i.id == id);
    }

    // =========================
    // Court 接口
    // =========================

    public void SubmitEvidenceToCourt(string evidenceId)
    {
        EvidenceData evidence = GetEvidenceById(evidenceId);
        if (evidence == null || !evidence.isUnlocked)
        {
            Debug.LogWarning("[DataManager] Invalid evidence submission.");
            return;
        }

        if (!topicPersuasion.ContainsKey(evidence.relatedTopic))
        {
            Debug.LogWarning("[DataManager] Topic not found for evidence.");
            return;
        }

        topicPersuasion[evidence.relatedTopic] += evidence.persuasionValue;

        Debug.Log($"[Court] Topic {evidence.relatedTopic} persuasion = {topicPersuasion[evidence.relatedTopic]}");
    }

    public bool IsTopicResolved(string topicId)
    {
        CourtTopic topic = courtTopics.Find(t => t.id == topicId);
        if (topic == null) return false;

        return topicPersuasion[topicId] >= topic.requiredPersuasion;
    }

    // =========================
    // Debug / ������
    // =========================

    public void ResetAllData()
    {
        foreach (var e in allEvidence)
        {
            e.isUnlocked = false;
        }

        foreach (var i in allItems)
        {
            i.isCollected = false;
            i.collectTime = 0f;
        }

        InitializeCourtData();
    }

    // =========================
    // 快照 Capture / Restore
    // =========================

    /// <summary>捕获所有道具的收集状态快照。</summary>
    public List<CourtDataSnapshot.ItemSnapshot> CaptureItemSnapshots()
    {
        var list = new List<CourtDataSnapshot.ItemSnapshot>();
        foreach (var item in allItems)
        {
            list.Add(new CourtDataSnapshot.ItemSnapshot
            {
                id = item.id,
                isCollected = item.isCollected,
                collectTime = item.collectTime,
            });
        }
        return list;
    }

    /// <summary>捕获所有证据的解锁状态快照。</summary>
    public List<CourtDataSnapshot.EvidenceSnapshot> CaptureEvidenceSnapshots()
    {
        var list = new List<CourtDataSnapshot.EvidenceSnapshot>();
        foreach (var ev in allEvidence)
        {
            list.Add(new CourtDataSnapshot.EvidenceSnapshot
            {
                id = ev.id,
                isUnlocked = ev.isUnlocked,
            });
        }
        return list;
    }

    /// <summary>从快照恢复道具和证据状态。</summary>
    public void RestoreFromSnapshot(
        List<CourtDataSnapshot.ItemSnapshot> itemSnaps,
        List<CourtDataSnapshot.EvidenceSnapshot> evidenceSnaps)
    {
        // 恢复道具
        if (itemSnaps != null)
        {
            foreach (var snap in itemSnaps)
            {
                var item = allItems.Find(i => i.id == snap.id);
                if (item != null)
                {
                    item.isCollected = snap.isCollected;
                    item.collectTime = snap.collectTime;
                }
            }
        }

        // 恢复证据
        if (evidenceSnaps != null)
        {
            foreach (var snap in evidenceSnaps)
            {
                var ev = allEvidence.Find(e => e.id == snap.id);
                if (ev != null)
                {
                    ev.isUnlocked = snap.isUnlocked;
                }
            }
        }

        // 重新初始化庭审话题
        InitializeCourtData();

        Debug.Log("[DataManager] 数据已从快照恢复。");
    }
}