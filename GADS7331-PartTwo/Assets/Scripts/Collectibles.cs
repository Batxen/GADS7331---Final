using UnityEngine;

/// <summary>
/// Item prefabs and per-slot collection data (item indices, amounts, pickup tag) used by quests.
/// </summary>
public class Collectibles : MonoBehaviour
{
    #region Constants
    public const int SlotCount = 3;
    #endregion

    #region Serialized Fields
    [Header("Item data (exactly three elements each)")]
    [SerializeField] private int[] m_Item = new int[SlotCount];
    [SerializeField] private int[] m_Amount = new int[SlotCount];

    [Header("Item prefabs (m_Item[row] uses 0, 1, or 2 to pick one of these)")]
    [SerializeField] private GameObject m_ItemPrefab0;
    [SerializeField] private GameObject m_ItemPrefab1;
    [SerializeField] private GameObject m_ItemPrefab2;

    [Header("Pickup detection")]
    [SerializeField]
    [Tooltip("Spawned collectibles accept contact from objects with this tag.")]
    private string m_PlayerTag = "Player";
    #endregion

    #region Public Properties
    public string PlayerTag => m_PlayerTag;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        EnsureArrays();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureArrays();
    }
#endif
    #endregion

    #region Public Methods
    public int GetItemValue(int _row)
    {
        return m_Item[_row];
    }

    public int GetAmount(int _row)
    {
        return m_Amount[_row];
    }

    public GameObject GetPrefab(int _slot)
    {
        switch (_slot)
        {
            case 0:
                return m_ItemPrefab0;
            case 1:
                return m_ItemPrefab1;
            case 2:
                return m_ItemPrefab2;
            default:
                return null;
        }
    }
    #endregion

    #region Private Methods
    private void EnsureArrays()
    {
        ResizeArray(ref m_Item, SlotCount);
        ResizeArray(ref m_Amount, SlotCount);
    }

    private static void ResizeArray(ref int[] _array, int _length)
    {
        if (_array == null)
        {
            _array = new int[_length];
            return;
        }

        if (_array.Length == _length)
        {
            return;
        }

        int[] newArray = new int[_length];
        int copyCount = Mathf.Min(_array.Length, _length);
        for (int i = 0; i < copyCount; i++)
        {
            newArray[i] = _array[i];
        }

        _array = newArray;
    }
    #endregion
}

/// <summary>
/// Added to spawned collectible instances. Detects player contact via trigger or collision.
/// </summary>
public class QuestSpawnedItem : MonoBehaviour
{
    #region Private Fields
    private PlayerQuest m_Owner;
    private string m_PlayerTag;
    private bool m_DebugPickupCollision;
    private bool m_HasBeenCollected;
    #endregion

    #region Public Methods
    public void Initialize(PlayerQuest _owner, string _playerTag, bool _debugPickupCollision = true)
    {
        m_Owner = _owner;
        m_PlayerTag = _playerTag;
        m_DebugPickupCollision = _debugPickupCollision;
        EnsurePickupPhysics();
    }
    #endregion

    #region Unity Lifecycle
    private void OnTriggerEnter(Collider _other)
    {
        TryCollectFromContact(_other, "OnTriggerEnter");
    }

    private void OnCollisionEnter(Collision _collision)
    {
        TryCollectFromContact(_collision.collider, "OnCollisionEnter");
    }
    #endregion

    #region Private Methods
    private void EnsurePickupPhysics()
    {
        if (!TryGetComponent(out Rigidbody rigidbody))
        {
            rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = 0.5f;
            return;
        }

        foreach (Collider collider in colliders)
        {
            collider.isTrigger = true;
        }
    }

    private void TryCollectFromContact(Collider _other, string _eventName)
    {
        if (m_HasBeenCollected)
        {
            return;
        }

        if (m_DebugPickupCollision)
        {
            Debug.Log(
                $"[QuestSpawnedItem] {_eventName} on '{name}' with '{_other.name}' (tag={_other.tag}, layer={LayerMask.LayerToName(_other.gameObject.layer)}).");
        }

        if (m_Owner == null)
        {
            if (m_DebugPickupCollision)
            {
                Debug.LogWarning($"[QuestSpawnedItem] Pickup ignored: {nameof(PlayerQuest)} reference is missing.");
            }

            return;
        }

        if (!IsPlayerCollider(_other))
        {
            if (m_DebugPickupCollision)
            {
                Debug.Log(
                    $"[QuestSpawnedItem] Contact ignored: '{_other.name}' is not the player (no {nameof(PlayerStats)} in hierarchy, tag is not '{m_PlayerTag}').");
            }

            return;
        }

        if (m_DebugPickupCollision)
        {
            Debug.Log($"[QuestSpawnedItem] Player contact confirmed on '{name}'. Collecting item.");
        }

        m_HasBeenCollected = true;
        m_Owner.ReportItemCollected();
        Destroy(gameObject);
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
