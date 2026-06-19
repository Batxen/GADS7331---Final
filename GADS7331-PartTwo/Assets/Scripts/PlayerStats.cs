using UnityEngine;

/// <summary>
/// Player score; attach to the player object. Other systems update score through this component.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("Current score (e.g. pickups this quest).")]
    private int m_Score;
    #endregion

    #region Public Properties
    public int Score => m_Score;
    #endregion

    #region Public Methods
    public void ResetScore()
    {
        m_Score = 0;
    }

    public void AddScore(int _amount)
    {
        m_Score += _amount;
        Debug.Log($"[{nameof(PlayerStats)}] Score updated to {m_Score}.");
    }
    #endregion
}
