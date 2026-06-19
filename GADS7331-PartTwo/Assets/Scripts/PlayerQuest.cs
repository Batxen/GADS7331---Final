using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Main quest controller: reads location data, pulls item/prefab data from <see cref="Collectibles"/>,
/// and score from <see cref="PlayerStats"/>. Press E inside the quest accept zone when no quest is active.
/// </summary>
public class PlayerQuest : MonoBehaviour
{
    #region Constants
    private const int c_SlotCount = 3;
    private const string c_DefaultQuestLogFileName = "player_quest_log.txt";
    #endregion

    #region Serialized Fields — References
    [Header("Dependencies")]
    [SerializeField]
    [Tooltip("Player object stats (score).")]
    private PlayerStats m_PlayerStats;

    [SerializeField]
    [Tooltip("Prefab and item/amount tables for collectibles.")]
    private Collectibles m_Collectibles;
    #endregion

    #region Serialized Fields — Quest log
    [Header("Quest log (.txt)")]
    [SerializeField]
    [Tooltip("Drag a .txt TextAsset from the project. Runtime writes use this file's name under PersistentDataPath.")]
    private TextAsset m_QuestLogFile;

    [Header("Quest log UI")]
    [SerializeField]
    [Tooltip("InputField under Canvas > Text Chat Input > InputField. Quest log text is shown in its Text child.")]
    private InputField m_QuestLogInputField;

    [SerializeField]
    [Tooltip("Canvas > Text Chat Input. Hidden while a quest is active (also hides Send Button).")]
    private GameObject m_TextChatInputRoot;

    [SerializeField]
    [Tooltip("Canvas > Text Chat Input > Send Button. Hidden while a quest is active.")]
    private GameObject m_SendButton;
    #endregion

    #region Serialized Fields — Location (parallel to m_Locations)
    [Header("Location colliders (index 0–2 matches m_Locations)")]
    [SerializeField] private Collider m_LocationCollider0;
    [SerializeField] private Collider m_LocationCollider1;
    [SerializeField] private Collider m_LocationCollider2;

    [Header("Quest accept zone")]
    [SerializeField]
    [Tooltip("Player must be inside this trigger collider to press E and start a quest.")]
    private Collider m_QuestAcceptCollider;

    [SerializeField]
    [Tooltip("Logs why E was accepted or ignored (useful for setup debugging).")]
    private bool m_DebugInteractInput = true;

    [Header("Quest location labels (exactly three elements)")]
    [SerializeField] private string[] m_Locations = new string[c_SlotCount];
    #endregion

    #region Private Fields
    private bool m_IsQuestActive;
    private int m_QuestScoreRequired;
    private bool m_IsPlayerInAcceptZone;
    #endregion

    #region Public Properties
    public int PlayerScore => m_PlayerStats != null ? m_PlayerStats.Score : 0;
    public bool IsQuestActive => m_IsQuestActive;
    public bool IsPlayerInAcceptZone => m_IsPlayerInAcceptZone;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        EnsureLocationArray();
        BindQuestAcceptZone();
        ResolveQuestChatUiReferences();
    }

    private void Update()
    {
        if (!WasInteractKeyPressedThisFrame())
        {
            return;
        }

        if (m_DebugInteractInput)
        {
            Debug.Log($"[{nameof(PlayerQuest)}] E key detected.");
        }

        if (m_IsQuestActive)
        {
            if (m_DebugInteractInput)
            {
                Debug.Log($"[{nameof(PlayerQuest)}] E ignored: a quest is already active.");
            }

            return;
        }

        if (!IsPlayerInQuestAcceptZone())
        {
            if (m_DebugInteractInput)
            {
                Debug.Log(
                    $"[{nameof(PlayerQuest)}] E ignored: player is not in the quest accept zone (trigger={m_IsPlayerInAcceptZone}, positionCheck={IsPlayerInsideAcceptZoneByPosition()}).");
            }

            return;
        }

        AcceptQuest();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureLocationArray();
    }
#endif
    #endregion

    #region Public Methods
    internal void SetPlayerInAcceptZone(bool _isInside)
    {
        m_IsPlayerInAcceptZone = _isInside;
    }

    /// <summary>
    /// Called by spawned quest items when the player touches their collider.
    /// </summary>
    public void ReportItemCollected()
    {
        if (!m_IsQuestActive)
        {
            if (m_DebugInteractInput)
            {
                Debug.LogWarning($"[{nameof(PlayerQuest)}] Pickup ignored: no quest is active.");
            }

            return;
        }

        if (m_PlayerStats == null)
        {
            Debug.LogWarning($"[{nameof(PlayerQuest)}] Pickup ignored: {nameof(m_PlayerStats)} is not assigned.");
            return;
        }

        m_PlayerStats.AddScore(1);

        if (m_DebugInteractInput)
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Pickup collected. Score {m_PlayerStats.Score}/{m_QuestScoreRequired}.");
        }

        if (m_PlayerStats.Score < m_QuestScoreRequired)
        {
            return;
        }

        m_IsQuestActive = false;
        Debug.Log("Player reached the amount needed for this quest.");
        ClearQuestLogFile();
        DisplayQuestLogInInputField(string.Empty);
        SetQuestChatUiActive(true);
    }
    #endregion

    #region Private Methods
    private void BindQuestAcceptZone()
    {
        if (m_QuestAcceptCollider == null)
        {
            return;
        }

        if (!m_QuestAcceptCollider.isTrigger)
        {
            Debug.LogWarning(
                $"{nameof(PlayerQuest)}: {nameof(m_QuestAcceptCollider)} should have Is Trigger enabled for zone detection.");
        }

        if (!m_QuestAcceptCollider.TryGetComponent(out QuestAcceptZoneListener listener))
        {
            listener = m_QuestAcceptCollider.gameObject.AddComponent<QuestAcceptZoneListener>();
        }

        string playerTag = m_Collectibles != null ? m_Collectibles.PlayerTag : "Player";
        listener.Bind(this, playerTag);
    }

    private static bool WasInteractKeyPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        return Input.GetKeyDown(KeyCode.E);
    }

    private bool IsPlayerInQuestAcceptZone()
    {
        if (m_QuestAcceptCollider == null)
        {
            if (m_DebugInteractInput)
            {
                Debug.LogWarning($"{nameof(PlayerQuest)}: Assign {nameof(m_QuestAcceptCollider)} to accept quests.");
            }

            return false;
        }

        if (m_IsPlayerInAcceptZone)
        {
            return true;
        }

        return IsPlayerInsideAcceptZoneByPosition();
    }

    private bool IsPlayerInsideAcceptZoneByPosition()
    {
        if (m_QuestAcceptCollider == null || m_PlayerStats == null)
        {
            return false;
        }

        return IsPointInsideCollider(m_QuestAcceptCollider, m_PlayerStats.transform.position);
    }

    private string GetQuestLogFilePath()
    {
        string fileName = m_QuestLogFile != null ? $"{m_QuestLogFile.name}.txt" : c_DefaultQuestLogFileName;
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private string BuildQuestLogContent(
        int _locationIndex,
        string _locationName,
        int _itemIndex,
        int _itemArrayValue,
        int _prefabSlot,
        string _prefabName,
        int _amount)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"LocationIndex={_locationIndex}");
        builder.AppendLine($"LocationName={_locationName}");
        builder.AppendLine($"ItemIndex={_itemIndex}");
        builder.AppendLine($"ItemArrayValue={_itemArrayValue}");
        builder.AppendLine($"PrefabSlot={_prefabSlot}");
        builder.AppendLine($"PrefabName={_prefabName}");
        builder.AppendLine($"Amount={_amount}");
        return builder.ToString();
    }

    private bool WriteQuestLogContentToFile(string _content)
    {
        string path = GetQuestLogFilePath();
        try
        {
            File.WriteAllText(path, _content);
            return true;
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"{nameof(PlayerQuest)}: Could not write quest log — {exception.Message}");
            return false;
        }
    }

    private string ReadQuestLogFileContents()
    {
        string path = GetQuestLogFilePath();
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"{nameof(PlayerQuest)}: Could not read quest log — {exception.Message}");
            return string.Empty;
        }
    }

    private void ResolveQuestChatUiReferences()
    {
        if (m_QuestLogInputField == null)
        {
            InputField[] inputFields = FindObjectsByType<InputField>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (InputField inputField in inputFields)
            {
                if (!IsQuestChatInputField(inputField))
                {
                    continue;
                }

                m_QuestLogInputField = inputField;
                break;
            }
        }

        if (m_TextChatInputRoot == null && m_QuestLogInputField != null)
        {
            Transform chatInputParent = m_QuestLogInputField.transform.parent;
            if (chatInputParent != null && chatInputParent.name == "Text Chat Input")
            {
                m_TextChatInputRoot = chatInputParent.gameObject;
            }
        }

        if (m_SendButton == null && m_TextChatInputRoot != null)
        {
            Transform sendButton = m_TextChatInputRoot.transform.Find("Send Button");
            if (sendButton != null)
            {
                m_SendButton = sendButton.gameObject;
            }
        }

        if (m_QuestLogInputField == null)
        {
            Debug.LogWarning(
                $"{nameof(PlayerQuest)}: Assign {nameof(m_QuestLogInputField)} (Canvas > Text Chat Input > InputField) to display the quest log.");
        }
    }

    private void SetQuestChatUiActive(bool _isActive)
    {
        if (m_TextChatInputRoot != null)
        {
            m_TextChatInputRoot.SetActive(_isActive);
            return;
        }

        if (m_QuestLogInputField != null)
        {
            m_QuestLogInputField.gameObject.SetActive(_isActive);
        }

        if (m_SendButton != null)
        {
            m_SendButton.SetActive(_isActive);
        }
    }

    private void InvokeQuestLogSendButtonClick()
    {
        if (m_QuestLogInputField == null || m_QuestLogInputField.text.Length == 0)
        {
            if (m_DebugInteractInput)
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerQuest)}] Send skipped: quest log input is empty or not assigned.");
            }

            return;
        }

        if (m_SendButton == null || !m_SendButton.TryGetComponent(out Button sendButton))
        {
            Debug.LogWarning(
                $"[{nameof(PlayerQuest)}] Send skipped: assign {nameof(m_SendButton)} (Canvas > Text Chat Input > Send Button).");
            return;
        }

        sendButton.onClick.Invoke();

        if (m_DebugInteractInput)
        {
            Debug.Log($"[{nameof(PlayerQuest)}] Send Button invoked with quest log for chat delivery.");
        }
    }

    private static bool IsQuestChatInputField(InputField _inputField)
    {
        if (_inputField == null || _inputField.transform.parent == null)
        {
            return false;
        }

        return _inputField.name == "InputField"
            && _inputField.transform.parent.name == "Text Chat Input";
    }

    private void DisplayQuestLogInInputField(string _content)
    {
        if (m_QuestLogInputField == null)
        {
            return;
        }

        m_QuestLogInputField.text = _content ?? string.Empty;
    }

    private void ClearQuestLogFile()
    {
        string path = GetQuestLogFilePath();
        try
        {
            File.WriteAllText(path, string.Empty);
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"{nameof(PlayerQuest)}: Could not clear quest log — {exception.Message}");
        }
    }

    private void EnsureLocationArray()
    {
        ResizeArray(ref m_Locations, c_SlotCount);
    }

    private static void ResizeArray(ref string[] _array, int _length)
    {
        if (_array == null)
        {
            _array = new string[_length];
            return;
        }

        if (_array.Length == _length)
        {
            return;
        }

        string[] newArray = new string[_length];
        int copyCount = Mathf.Min(_array.Length, _length);
        for (int i = 0; i < copyCount; i++)
        {
            newArray[i] = _array[i];
        }

        _array = newArray;
    }

    private void AcceptQuest()
    {
        if (m_PlayerStats == null)
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Quest activation: UNSUCCESSFUL. Location selection: SKIPPED. Item spawning: SKIPPED. Reason: {nameof(m_PlayerStats)} is not assigned.");
            return;
        }

        if (m_Collectibles == null)
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Quest activation: UNSUCCESSFUL. Location selection: SKIPPED. Item spawning: SKIPPED. Reason: {nameof(m_Collectibles)} is not assigned.");
            return;
        }

        EnsureLocationArray();

        int locationIndex = Random.Range(0, c_SlotCount);
        int itemIndex = Random.Range(0, c_SlotCount);

        string locationLabel = m_Locations != null && locationIndex < m_Locations.Length
            ? m_Locations[locationIndex]
            : $"index_{locationIndex}";

        Collider locationCollider = GetLocationCollider(locationIndex);
        if (locationCollider == null)
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Quest activation: UNSUCCESSFUL. Location selection: UNSUCCESSFUL (index {locationIndex}, label '{locationLabel}' has no collider assigned). Item spawning: SKIPPED.");
            return;
        }

        Debug.Log(
            $"[{nameof(PlayerQuest)}] Location selection: SUCCESSFUL. Index {locationIndex}, label '{locationLabel}', collider '{locationCollider.name}'.");

        int prefabSlot = Mathf.Clamp(m_Collectibles.GetItemValue(itemIndex), 0, c_SlotCount - 1);
        GameObject prefab = m_Collectibles.GetPrefab(prefabSlot);
        if (prefab == null)
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Quest activation: UNSUCCESSFUL. Location selection: SUCCESSFUL. Item spawning: UNSUCCESSFUL (prefab slot {prefabSlot} from item row {itemIndex} is not assigned).");
            return;
        }

        int spawnAmount = Mathf.Max(0, m_Collectibles.GetAmount(itemIndex));
        if (spawnAmount == 0)
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Quest activation: UNSUCCESSFUL. Location selection: SUCCESSFUL. Item spawning: UNSUCCESSFUL (m_Amount[{itemIndex}] is zero).");
            return;
        }

        string prefabName = prefab.name;

        m_PlayerStats.ResetScore();
        m_QuestScoreRequired = spawnAmount;
        m_IsQuestActive = true;

        string questLogContent = BuildQuestLogContent(
            locationIndex,
            locationLabel,
            itemIndex,
            m_Collectibles.GetItemValue(itemIndex),
            prefabSlot,
            prefabName,
            spawnAmount);

        if (WriteQuestLogContentToFile(questLogContent))
        {
            DisplayQuestLogInInputField(ReadQuestLogFileContents());
        }

        int spawnedCount = 0;
        for (int i = 0; i < spawnAmount; i++)
        {
            Vector3 spawnPosition = GetRandomPointInsideCollider(locationCollider);
            Quaternion spawnRotation = Quaternion.identity;
            GameObject instance = Instantiate(prefab, spawnPosition, spawnRotation);

            if (instance == null)
            {
                Debug.LogWarning(
                    $"[{nameof(PlayerQuest)}] Item spawning: Failed to create instance {i + 1} of {spawnAmount} ('{prefabName}').");
                continue;
            }

            if (!instance.TryGetComponent(out QuestSpawnedItem pickup))
            {
                pickup = instance.AddComponent<QuestSpawnedItem>();
            }

            pickup.Initialize(this, m_Collectibles.PlayerTag, m_DebugInteractInput);
            spawnedCount++;
        }

        bool itemSpawningSuccessful = spawnedCount == spawnAmount;
        if (itemSpawningSuccessful)
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Item spawning: SUCCESSFUL. Spawned {spawnedCount} of {spawnAmount} '{prefabName}' inside '{locationCollider.name}'.");
        }
        else
        {
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Item spawning: UNSUCCESSFUL. Spawned {spawnedCount} of {spawnAmount} '{prefabName}' inside '{locationCollider.name}'.");
            m_IsQuestActive = false;
            m_QuestScoreRequired = 0;
            m_PlayerStats.ResetScore();
        }

        if (itemSpawningSuccessful)
        {
            InvokeQuestLogSendButtonClick();
            SetQuestChatUiActive(false);
            Debug.Log(
                $"[{nameof(PlayerQuest)}] Quest activation: SUCCESSFUL. Item row {itemIndex}, prefab slot {prefabSlot}, collect {spawnAmount} to complete.");
        }
        else
        {
            SetQuestChatUiActive(true);
            Debug.Log($"[{nameof(PlayerQuest)}] Quest activation: UNSUCCESSFUL. Quest aborted because item spawning did not complete.");
        }
    }

    private Collider GetLocationCollider(int _index)
    {
        switch (_index)
        {
            case 0:
                return m_LocationCollider0;
            case 1:
                return m_LocationCollider1;
            case 2:
                return m_LocationCollider2;
            default:
                return null;
        }
    }

    private static Vector3 GetRandomPointInsideCollider(Collider _collider)
    {
        Bounds bounds = _collider.bounds;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector3 point = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z));

            if (_collider.bounds.Contains(point) && IsPointInsideCollider(_collider, point))
            {
                return point;
            }
        }

        return _collider.bounds.center;
    }

    private static bool IsPointInsideCollider(Collider _collider, Vector3 _worldPoint)
    {
        Vector3 closest = _collider.ClosestPoint(_worldPoint);
        return (closest - _worldPoint).sqrMagnitude < 0.0001f;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Log quest log file path")]
    private void DebugLogQuestLogPath()
    {
        Debug.Log(GetQuestLogFilePath());
    }
#endif
    #endregion
}

/// <summary>
/// Added to the quest accept zone collider. Notifies <see cref="PlayerQuest"/> when the player enters or exits.
/// </summary>
public class QuestAcceptZoneListener : MonoBehaviour
{
    #region Private Fields
    private PlayerQuest m_Owner;
    private string m_PlayerTag;
    #endregion

    #region Public Methods
    public void Bind(PlayerQuest _owner, string _playerTag)
    {
        m_Owner = _owner;
        m_PlayerTag = _playerTag;
    }
    #endregion

    #region Unity Lifecycle
    private void OnTriggerEnter(Collider _other)
    {
        if (m_Owner == null || !IsPlayerCollider(_other))
        {
            return;
        }

        m_Owner.SetPlayerInAcceptZone(true);
    }

    private void OnTriggerExit(Collider _other)
    {
        if (m_Owner == null || !IsPlayerCollider(_other))
        {
            return;
        }

        m_Owner.SetPlayerInAcceptZone(false);
    }

    private bool IsPlayerCollider(Collider _other)
    {
        if (_other.GetComponentInParent<PlayerStats>() != null)
        {
            return true;
        }

        return !string.IsNullOrEmpty(m_PlayerTag) && _other.CompareTag(m_PlayerTag);
    }
    #endregion
}
