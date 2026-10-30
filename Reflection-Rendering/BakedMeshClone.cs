using UnityEngine;

public class BakedMeshClone : MonoBehaviour
{
    [Header("Source Settings")]
    [Tooltip("Which source to copy from")]
    public string sourceKey = "MainPlayer";

    [Header("Mirroring")]
    public bool flipX = false;
    public bool flipZ = false;

    [Header("Material")]
    [Tooltip("If checked, this clone keeps its own renderer and material instead of being instanced with the shared material. Movement/position sync is identical either way - this is the ONLY thing it changes.")]
    public bool doNotCopyMaterial = false;

    private bool isRegistered = false;
    private int retryCount = 0;

    void OnEnable()
    {
        retryCount = 0;
        StartCoroutine(TryRegister());
    }

    System.Collections.IEnumerator TryRegister()
    {
        // Wait for instance system
        while (BakedMeshInstanceSystem.Instance == null && retryCount < 30)
        {
            retryCount++;
            yield return null;
        }

        if (BakedMeshInstanceSystem.Instance == null)
        {
            Debug.LogError($"[BakedMeshClone] BakedMeshInstanceSystem not found!");
            yield break;
        }

        // Wait for source registration
        int sourceRetries = 0;
        while (!BakedMeshInstanceSystem.Instance.HasSource(sourceKey) && sourceRetries < 30)
        {
            sourceRetries++;
            yield return null;
        }

        if (!BakedMeshInstanceSystem.Instance.HasSource(sourceKey))
        {
            Debug.LogError($"[BakedMeshClone] Source '{sourceKey}' not found after waiting! Make sure there's a BakedMeshSource with sourceKey='{sourceKey}' in the scene.");
            yield break;
        }

        // Register clone
        BakedMeshInstanceSystem.Instance.RegisterClone(
            sourceKey,
            transform,
            transform.parent,
            flipX,
            flipZ,
            true,                // calculateOffset
            doNotCopyMaterial    // useOwnMaterial
        );

        isRegistered = true;
        Debug.Log($"[BakedMeshClone] Registered clone {name} for source '{sourceKey}' (doNotCopyMaterial: {doNotCopyMaterial})");
    }

    void OnDisable()
    {
        if (isRegistered && BakedMeshInstanceSystem.Instance != null)
        {
            BakedMeshInstanceSystem.Instance.UnregisterClone(sourceKey, transform);
            isRegistered = false;
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = doNotCopyMaterial ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, doNotCopyMaterial ? $"CLONE (own material): {sourceKey}" : $"CLONE: {sourceKey}");
#endif
    }
}


// Conversion helper
[ExecuteInEditMode]
public class ConvertToBakedMeshSystem : MonoBehaviour
{
    [Header("Conversion Settings")]
    public string sourceKey = "MainPlayer";

    [ContextMenu("Convert Active Room to Baked Source")]
    void ConvertToSource()
    {
        var existing = GetComponent<SimpleSourceRegister>();
        if (existing != null)
        {
            var bakedSource = gameObject.AddComponent<BakedMeshSource>();
            bakedSource.sourceKey = existing.sourceKey;
            bakedSource.animatorOverride = existing.animatorOverride;
            bakedSource.parentTransform = existing.parentTransform;
            bakedSource.syncTransform = existing.syncTransform;

            DestroyImmediate(existing);
            Debug.Log($"Converted {name} to BakedMeshSource");
        }
    }

    [ContextMenu("Convert Inactive Room to Baked Clone")]
    void ConvertToClone()
    {
        var existing = GetComponent<SimpleCloneRegister>();
        if (existing != null)
        {
            var bakedClone = gameObject.AddComponent<BakedMeshClone>();
            bakedClone.sourceKey = existing.sourceKey;
            bakedClone.flipX = existing.flipX;
            bakedClone.flipZ = existing.flipZ;

            DestroyImmediate(existing);
            Debug.Log($"Converted {name} to BakedMeshClone");
        }
    }
}