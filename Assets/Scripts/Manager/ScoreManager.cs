using UnityEngine;
using System;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int totalScore = 0;
    [Header("Shipping Stats")]
    public int rejectedPackagesCount = 0;
    public event Action<int> OnScoreChanged;
    public event Action<int> rejectedPackagesCountChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddScore(int amount)
    {
        totalScore += amount;
        OnScoreChanged?.Invoke(totalScore);
        Debug.Log($"Total Score: {totalScore} (Change: {amount})");
    }
    public void AddRejectedCount()
    {
        rejectedPackagesCount++;
        rejectedPackagesCountChanged?.Invoke(rejectedPackagesCount);
        Debug.Log($"จำนวนพัสดุตีกลับสะสม: {rejectedPackagesCount}");
    }
}