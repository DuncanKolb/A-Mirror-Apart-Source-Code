using UnityEngine;

public class BakedMeshSource : MonoBehaviour
{
    [Header("Source Identification")]
    [Tooltip("Unique key for this source")]
    public string sourceKey = "";

    [Header("Components (auto-find if empty)")]
    public Animator animatorOverride;
    public Transform parentTransform;

    [Header("Sync Settings")]
    [Tooltip("Should position be mirrored to clones?")]
    public bool syncTransform = true;

    private Animator animator;
    private bool isRegistered = false;
    private int retryCount = 0;

    void Awake()
    {
        // Find animator if available
        if (animatorOverride != null)
            animator = animatorOverride;
        else
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.Log($"[BakedMeshSource] No Animator found on {name}. Registering as transform/mesh source only.");
        }

        if (parentTransform == null)
            parentTransform = transform.parent;
    }

    void Start()
    {
        TryRegister();
    }

    void TryRegister()
    {
        if (isRegistered) return;

        // Register source
        if (BakedMeshInstanceSystem.Instance != null)
        {
            BakedMeshInstanceSystem.Instance.RegisterSource(
                sourceKey,
                animator,
                transform,
                parentTransform,
                syncTransform
            );

            isRegistered = true;
            Debug.Log($"[BakedMeshSource] Registered {sourceKey}");
        }
        else
        {
            retryCount++;
            if (retryCount < 10)
            {
                Debug.LogWarning($"[BakedMeshSource] BakedMeshInstanceSystem not found, retrying... ({retryCount}/10)");
                Invoke(nameof(TryRegister), 0.1f);
            }
            else
            {
                Debug.LogError($"[BakedMeshSource] BakedMeshInstanceSystem not found in scene after 10 retries! Make sure you have a GameObject with BakedMeshInstanceSystem component in the scene.");
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.5f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3.5f, $"BAKED SOURCE: {sourceKey}");
#endif
    }
}