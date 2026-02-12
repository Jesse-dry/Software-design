using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("Evidence Database")]
    [SerializeField] private List<EvidenceData> allEvidence = new List<EvidenceData>();

    [Header("Court Topics")]
    [SerializeField] private List<CourtTopic> courtTopics = new List<CourtTopic>();

    private Dictionary<string, int> topicPersuasion = new Dictionary<string, int>();

    private void Awake()
    {
        // 单例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeCourtData();
    }

    // =========================
    // 初始化
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
    // Evidence 相关
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
    // Court 相关
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
    // Debug / 测试用
    // =========================

    public void ResetAllData()
    {
        foreach (var e in allEvidence)
        {
            e.isUnlocked = false;
        }

        InitializeCourtData();
    }
}