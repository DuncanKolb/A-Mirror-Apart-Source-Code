using System.Collections.Generic;
using UnityEngine;

// UNIFIED SYSTEM:
// Handles SkinnedMeshRenderer (players)
// and MeshRenderer (blocks / buttons / stairs).
//
// Preserves negative hierarchy scales in the final draw matrix.
// A negative determinant uses a reversed-winding copy of the mesh
// without changing colliders or transform scales.
public class BakedMeshInstanceSystem : MonoBehaviour
{
    public static BakedMeshInstanceSystem Instance { get; private set; }

    [Header("Performance")]
    public int maxInstancesPerBatch = 1023;


    // ============================================================
    // DATA
    // ============================================================

    class RendererData
    {
        public Renderer renderer;

        // Normal baked mesh.
        public Mesh bakedMesh;

        // Same mesh with triangle winding reversed.
        public Mesh mirroredBakedMesh;

        // Used for regular MeshRenderers.
        public Mesh originalMesh;

        public Material[] materials;

        public List<Matrix4x4>[] normalMatricesPerMaterial;
        public List<Matrix4x4>[] mirroredMatricesPerMaterial;

        public bool isSkinned;

        // Path from sourceTransform down to this renderer.
        public List<string> pathFromSource;
    }


    class SourceGroup
    {
        public Animator sourceAnimator;

        public List<RendererData> renderers =
            new List<RendererData>();

        public Transform sourceTransform;

        public bool syncTransform;

        public List<CloneInstance> clones =
            new List<CloneInstance>();
    }


    class CloneInstance
    {
        public Transform transform;
        public Transform parent;

        public bool flipX;
        public bool flipZ;

        public Vector3 initialLocalOffset;

        public bool useOwnMaterial;

        // Per RendererData.
        //
        // REGULAR MESH:
        // Contains:
        // - room / ancestor scale
        // - clone root scale
        // - child position
        // - child rotation
        // - child scale
        //
        // SKINNED MESH:
        // Contains:
        // - room / ancestor scale
        // - clone root scale
        //
        // Player rotation comes from the synchronized source rotation.
        public Matrix4x4[] childLocalMatrices;
    }


    Dictionary<string, SourceGroup> sources =
        new Dictionary<string, SourceGroup>();


    // ============================================================
    // MIRROR TEST
    // ============================================================

    // Negative determinant = odd number of reflections.
    //
    // Example:
    //
    // Area D = ( 1, 1, 1)
    // Player = (1, 1,-1)
    //
    // Final determinant is negative
    bool IsMirrored(Matrix4x4 m)
    {
        Vector3 col0 = m.GetColumn(0);
        Vector3 col1 = m.GetColumn(1);
        Vector3 col2 = m.GetColumn(2);

        float det =
            Vector3.Dot(
                col0,
                Vector3.Cross(col1, col2)
            );

        return det < 0f;
    }


    // ============================================================
    // UNITY INITIALIZATION
    // ============================================================

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            //DontDestroyOnLoad(gameObject);

            Debug.Log(
                "[UnifiedBakedInstance] Initialized!"
            );
        }
        else
        {
            Destroy(gameObject);
        }
    }


    public bool HasSource(string sourceKey)
    {
        return sources.ContainsKey(sourceKey);
    }


    // ============================================================
    // REGISTER SOURCE
    // ============================================================

    public void RegisterSource(
        string sourceKey,
        Animator sourceAnimator,
        Transform sourceTransform,
        Transform sourceParent,
        bool syncTransform = true)
    {
        if (sources.ContainsKey(sourceKey))
        {
            return;
        }


        if (sourceTransform == null)
        {
            Debug.LogError(
                $"[UnifiedBaked] Source '{sourceKey}' " +
                $"missing sourceTransform!"
            );

            return;
        }


        if (sourceAnimator == null)
        {
            Debug.Log(
                $"[UnifiedBaked] Source '{sourceKey}' " +
                $"has no animator. Using sourceTransform " +
                $"for renderer search."
            );
        }


        var group =
            new SourceGroup
            {
                sourceAnimator = sourceAnimator,

                sourceTransform = sourceTransform,

                syncTransform = syncTransform
            };


        // If there is an Animator, search under it.
        // Otherwise search under sourceTransform.
        Transform rendererRoot =
            sourceAnimator != null
                ? sourceAnimator.transform
                : sourceTransform;


        var skinnedRenderers =
            rendererRoot
                .GetComponentsInChildren<
                    SkinnedMeshRenderer>();


        var meshRenderers =
            rendererRoot
                .GetComponentsInChildren<
                    MeshRenderer>();


        // ========================================================
        // SKINNED RENDERERS
        // ========================================================

        foreach (var renderer in skinnedRenderers)
        {
            var rendererData =
                new RendererData
                {
                    renderer = renderer,

                    bakedMesh = new Mesh(),

                    mirroredBakedMesh = new Mesh(),

                    materials =
                        renderer.sharedMaterials,

                    isSkinned = true,

                    pathFromSource =
                        GetPathFromAncestor(
                            sourceTransform,
                            renderer.transform
                        )
                };


            rendererData.bakedMesh.name =
                renderer.name + "_Baked";


            rendererData.mirroredBakedMesh.name =
                renderer.name + "_Baked_Mirrored";


            rendererData.normalMatricesPerMaterial =
                new List<Matrix4x4>[
                    rendererData.materials.Length
                ];


            rendererData.mirroredMatricesPerMaterial =
                new List<Matrix4x4>[
                    rendererData.materials.Length
                ];


            for (int i = 0;
                 i < rendererData.materials.Length;
                 i++)
            {
                rendererData.normalMatricesPerMaterial[i] =
                    new List<Matrix4x4>();


                rendererData.mirroredMatricesPerMaterial[i] =
                    new List<Matrix4x4>();


                if (rendererData.materials[i] != null)
                {
                    rendererData.materials[i]
                        .enableInstancing = true;
                }
            }


            group.renderers.Add(rendererData);


            // Keep real source visible.
            renderer.enabled = true;
        }


        // ========================================================
        // REGULAR MESH RENDERERS
        // ========================================================

        foreach (var renderer in meshRenderers)
        {
            var meshFilter =
                renderer.GetComponent<MeshFilter>();


            if (meshFilter == null ||
                meshFilter.sharedMesh == null)
            {
                continue;
            }


            var rendererData =
                new RendererData
                {
                    renderer = renderer,

                    bakedMesh = new Mesh(),

                    mirroredBakedMesh = new Mesh(),

                    originalMesh =
                        meshFilter.sharedMesh,

                    materials =
                        renderer.sharedMaterials,

                    isSkinned = false,

                    pathFromSource =
                        GetPathFromAncestor(
                            sourceTransform,
                            renderer.transform
                        )
                };


            rendererData.bakedMesh.name =
                renderer.name + "_Baked";


            rendererData.mirroredBakedMesh.name =
                renderer.name + "_Baked_Mirrored";


            rendererData.normalMatricesPerMaterial =
                new List<Matrix4x4>[
                    rendererData.materials.Length
                ];


            rendererData.mirroredMatricesPerMaterial =
                new List<Matrix4x4>[
                    rendererData.materials.Length
                ];


            for (int i = 0;
                 i < rendererData.materials.Length;
                 i++)
            {
                rendererData.normalMatricesPerMaterial[i] =
                    new List<Matrix4x4>();


                rendererData.mirroredMatricesPerMaterial[i] =
                    new List<Matrix4x4>();


                if (rendererData.materials[i] != null)
                {
                    rendererData.materials[i]
                        .enableInstancing = true;
                }
            }


            group.renderers.Add(rendererData);


            renderer.enabled = true;
        }


        sources[sourceKey] = group;


        Debug.Log(
            $"[UnifiedBaked] Registered source: " +
            $"{sourceKey} " +
            $"({skinnedRenderers.Length} skinned, " +
            $"{meshRenderers.Length} mesh renderers)"
        );
    }


    // ============================================================
    // HIERARCHY HELPERS
    // ============================================================

    List<string> GetPathFromAncestor(
        Transform ancestor,
        Transform descendant)
    {
        var path = new List<string>();

        Transform current = descendant;


        while (current != null &&
               current != ancestor)
        {
            path.Insert(
                0,
                current.name
            );

            current = current.parent;
        }


        return path;
    }


    Transform ResolveByPath(
        Transform root,
        List<string> path)
    {
        Transform current = root;


        foreach (var name in path)
        {
            if (current == null)
            {
                return null;
            }


            current =
                current.Find(name);
        }


        return current;
    }


    // Builds local transform from descendant up to,
    // but NOT including, ancestor.
    Matrix4x4 BuildLocalMatrixToAncestor(
        Transform descendant,
        Transform ancestor)
    {
        Matrix4x4 m =
            Matrix4x4.identity;


        Transform current =
            descendant;


        while (current != null &&
               current != ancestor)
        {
            Matrix4x4 currentLocal =
                Matrix4x4.TRS(
                    current.localPosition,
                    current.localRotation,
                    current.localScale
                );


            m =
                currentLocal * m;


            current =
                current.parent;
        }


        return m;
    }


    // Accumulate scale values through every ancestor.
    // DrawMeshInstanced does not render through the clone's
    // Transform hierarchy.
    Vector3 GetAncestorNetScale(Transform t)
    {
        Vector3 scale =
            Vector3.one;


        Transform current =
            t;


        while (current != null)
        {
            scale.x *=
                current.localScale.x;

            scale.y *=
                current.localScale.y;

            scale.z *=
                current.localScale.z;


            current =
                current.parent;
        }


        return scale;
    }


    // ============================================================
    // COPY REGULAR MESH
    // ============================================================

    void CopyMeshGeometry(
        Mesh source,
        Mesh destination)
    {
        if (source == null ||
            destination == null)
        {
            return;
        }


        destination.Clear();


        destination.indexFormat =
            source.indexFormat;


        destination.vertices =
            source.vertices;

        destination.normals =
            source.normals;

        destination.tangents =
            source.tangents;

        destination.colors =
            source.colors;

        destination.uv =
            source.uv;

        destination.uv2 =
            source.uv2;

        destination.uv3 =
            source.uv3;

        destination.uv4 =
            source.uv4;


        destination.subMeshCount =
            source.subMeshCount;


        for (int s = 0;
             s < source.subMeshCount;
             s++)
        {
            MeshTopology topology =
                source.GetTopology(s);


            int[] indices =
                source.GetIndices(s);


            destination.SetIndices(
                indices,
                topology,
                s,
                false
            );
        }


        destination.bounds =
            source.bounds;
    }


    // ============================================================
    // BUILD REVERSED-WINDING MESH
    // ============================================================

    void BuildReversedWindingMesh(
        Mesh source,
        Mesh destination)
    {
        if (source == null ||
            destination == null)
        {
            return;
        }


        destination.Clear();


        destination.indexFormat =
            source.indexFormat;


        destination.vertices =
            source.vertices;

        destination.normals =
            source.normals;

        destination.tangents =
            source.tangents;

        destination.colors =
            source.colors;

        destination.uv =
            source.uv;

        destination.uv2 =
            source.uv2;

        destination.uv3 =
            source.uv3;

        destination.uv4 =
            source.uv4;


        destination.subMeshCount =
            source.subMeshCount;


        for (int s = 0;
             s < source.subMeshCount;
             s++)
        {
            MeshTopology topology =
                source.GetTopology(s);


            int[] indices =
                source.GetIndices(s);


            if (topology ==
                MeshTopology.Triangles)
            {
                for (int i = 0;
                     i + 2 < indices.Length;
                     i += 3)
                {
                    int temp =
                        indices[i + 1];


                    indices[i + 1] =
                        indices[i + 2];


                    indices[i + 2] =
                        temp;
                }
            }


            destination.SetIndices(
                indices,
                topology,
                s,
                false
            );
        }


        destination.bounds =
            source.bounds;
    }


    // ============================================================
    // REGISTER CLONE
    // ============================================================

    public void RegisterClone(
        string sourceKey,
        Transform cloneTransform,
        Transform cloneParent,
        bool flipX = false,
        bool flipZ = false,
        bool calculateOffset = true,
        bool useOwnMaterial = false)
    {
        if (!sources.TryGetValue(
                sourceKey,
                out var group))
        {
            Debug.LogError(
                $"Source '{sourceKey}' not found!"
            );

            return;
        }


        Vector3 initialOffset =
            Vector3.zero;


        // ========================================================
        // INITIAL POSITION OFFSET
        // ========================================================

        if (calculateOffset &&
            group.sourceTransform != null &&
            cloneParent != null)
        {
            Vector3 sourceWorldPos =
                group.sourceTransform.position;


            Vector3 sourceInCloneParentSpace =
                cloneParent.InverseTransformPoint(
                    sourceWorldPos
                );


            Vector3 mirroredPos =
                sourceInCloneParentSpace;


            if (flipX)
            {
                mirroredPos.x =
                    -mirroredPos.x;
            }


            if (flipZ)
            {
                mirroredPos.z =
                    -mirroredPos.z;
            }


            Vector3 cloneCurrentLocalPos =
                cloneParent.InverseTransformPoint(
                    cloneTransform.position
                );


            initialOffset =
                cloneCurrentLocalPos -
                mirroredPos;
        }


        // ========================================================
        // CAPTURE CLONE-SPECIFIC HIERARCHY
        // ========================================================

        var childLocalMatrices =
            new Matrix4x4[
                group.renderers.Count
            ];


        // Clone's own local scale.
        //
        // PLAYER EXAMPLE:
        //
        // (1,1,-1)
        Matrix4x4 cloneScaleMatrix =
            Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                cloneTransform.localScale
            );


        // Everything ABOVE the clone.
        //
        // PLAYER EXAMPLES:
        //
        // Area A = ( 1,1,-1)
        // Area B = (-1,1, 1)
        // Area C = (-1,1,-1)
        // Area D = ( 1,1, 1)
        Vector3 ancestorScale =
            GetAncestorNetScale(
                cloneTransform.parent
            );


        Matrix4x4 ancestorScaleMatrix =
            Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                ancestorScale
            );


        for (int i = 0;
             i < group.renderers.Count;
             i++)
        {
            var rendererData =
                group.renderers[i];


            // ====================================================
            // SKINNED MESH
            // ====================================================
            //
            // Keeps ancestor, clone, and renderer-chain scale.
            // Clone Y rotation is excluded here because synchronized
            // source rotation is applied later.
            // ====================================================

            if (rendererData.isSkinned)
            {
                // Find the corresponding SkinnedMeshRenderer under the clone.
                Transform skinnedMatchedChild =
                    ResolveByPath(
                        cloneTransform,
                        rendererData.pathFromSource
                    );

                Vector3 rendererChainScale =
                    Vector3.one;

                if (skinnedMatchedChild != null)
                {
                    // Capture scale below the player root.
                    // Example: a 1.25 child scale combines with the
                    // 0.8 player-root scale to restore a net scale of 1.0.
                    rendererChainScale =
                        GetLocalNetScaleToAncestor(
                            skinnedMatchedChild,
                            cloneTransform
                        );
                }
                else
                {
                    Debug.LogWarning(
                        $"[UnifiedBaked] Skinned clone '{cloneTransform.name}' " +
                        $"has no matching renderer path for '{sourceKey}'."
                    );
                }

                Matrix4x4 rendererChainScaleMatrix =
                    Matrix4x4.TRS(
                        Vector3.zero,
                        Quaternion.identity,
                        rendererChainScale
                    );

                childLocalMatrices[i] =
                    ancestorScaleMatrix
                    *
                    cloneScaleMatrix
                    *
                    rendererChainScaleMatrix;

                continue;
            }


            // ====================================================
            // REGULAR MESH
            // ====================================================

            Transform matchedChild =
                ResolveByPath(
                    cloneTransform,
                    rendererData.pathFromSource
                );


            if (matchedChild != null)
            {
                childLocalMatrices[i] =

                    ancestorScaleMatrix

                    *

                    cloneScaleMatrix

                    *

                    BuildLocalMatrixToAncestor(
                        matchedChild,
                        cloneTransform
                    );
            }
            else
            {
                Debug.LogWarning(
                    $"[UnifiedBaked] Clone " +
                    $"'{cloneTransform.name}' " +
                    $"has no child matching path " +
                    $"for a source renderer of " +
                    $"'{sourceKey}'; falling back " +
                    $"to source child transform."
                );


                Matrix4x4 sourceScaleMatrix =
                    Matrix4x4.TRS(
                        Vector3.zero,
                        Quaternion.identity,
                        group.sourceTransform.localScale
                    );


                childLocalMatrices[i] =

                    ancestorScaleMatrix

                    *

                    sourceScaleMatrix

                    *

                    BuildLocalMatrixToAncestor(
                        rendererData.renderer.transform,
                        group.sourceTransform
                    );
            }
        }


        // ========================================================
        // CREATE CLONE INSTANCE
        // ========================================================

        var clone =
            new CloneInstance
            {
                transform =
                    cloneTransform,

                parent =
                    cloneParent,

                flipX =
                    flipX,

                flipZ =
                    flipZ,

                initialLocalOffset =
                    initialOffset,

                useOwnMaterial =
                    useOwnMaterial,

                childLocalMatrices =
                    childLocalMatrices
            };


        group.clones.Add(clone);


        // ========================================================
        // DISABLE REAL CLONE RENDERERS
        // ========================================================

        if (!useOwnMaterial)
        {
            var cloneRenderers =
                cloneTransform
                    .GetComponentsInChildren<
                        Renderer>();


            foreach (var renderer
                     in cloneRenderers)
            {
                renderer.enabled = false;
            }
        }


        Debug.Log(
            $"[UnifiedBaked] Registered clone " +
            $"for '{sourceKey}' " +
            $"(Total: {group.clones.Count}, " +
            $"useOwnMaterial: {useOwnMaterial})"
        );
    }


    // ============================================================
    // UNREGISTER CLONE
    // ============================================================

    public void UnregisterClone(
        string sourceKey,
        Transform cloneTransform)
    {
        if (sources.TryGetValue(
                sourceKey,
                out var group))
        {
            group.clones.RemoveAll(
                c =>
                    c.transform ==
                    cloneTransform
            );
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    void LateUpdate()
    {
        foreach (var kvp in sources)
        {
            var group =
                kvp.Value;


            group.clones.RemoveAll(
                c => c.transform == null
            );


            if (group.clones.Count == 0)
            {
                continue;
            }


            // ====================================================
            // BAKE / COPY SOURCE MESHES
            // ====================================================

            foreach (var rendererData
                     in group.renderers)
            {
                if (rendererData.renderer == null)
                {
                    continue;
                }


                if (rendererData.isSkinned)
                {
                    var skinned =
                        (SkinnedMeshRenderer)
                        rendererData.renderer;


                    // Source player is baked in its own local
                    // renderer space.
                    skinned.BakeMesh(
                        rendererData.bakedMesh
                    );
                }
                else
                {
                    CopyMeshGeometry(
                        rendererData.originalMesh,
                        rendererData.bakedMesh
                    );
                }


                // Every mesh gets a reversed-winding copy.
                BuildReversedWindingMesh(
                    rendererData.bakedMesh,
                    rendererData.mirroredBakedMesh
                );
            }


            // ====================================================
            // CLEAR MATRICES
            // ====================================================

            foreach (var rendererData
                     in group.renderers)
            {
                for (int i = 0;
                     i <
                     rendererData
                         .normalMatricesPerMaterial
                         .Length;
                     i++)
                {
                    rendererData
                        .normalMatricesPerMaterial[i]
                        .Clear();


                    rendererData
                        .mirroredMatricesPerMaterial[i]
                        .Clear();
                }
            }


            // ====================================================
            // CALCULATE CLONES
            // ====================================================

            foreach (var clone
                     in group.clones)
            {
                if (clone.transform == null ||
                    clone.parent == null)
                {
                    continue;
                }


                Vector3 position;
                Quaternion rotation;


                // =================================================
                // POSITION + ROTATION SYNC
                // =================================================

                if (group.syncTransform)
                {
                    Vector3 sourceWorldPos =
                        group.sourceTransform.position;


                    Vector3 sourceInCloneParentSpace =
                        clone.parent
                            .InverseTransformPoint(
                                sourceWorldPos
                            );


                    Vector3 targetLocalPos =
                        sourceInCloneParentSpace;


                    if (clone.flipX)
                    {
                        targetLocalPos.x =
                            -targetLocalPos.x;
                    }


                    if (clone.flipZ)
                    {
                        targetLocalPos.z =
                            -targetLocalPos.z;
                    }


                    targetLocalPos +=
                        clone.initialLocalOffset;


                    position =
                        clone.parent.TransformPoint(
                            targetLocalPos
                        );


                    // =============================================
                    // ROTATION
                    // =============================================
                    //
                    // One mirror reverses Y rotation.
                    // Two mirrors cancel each other out.
                    // No additional 180-degree rotation is needed.
                    // =============================================

                    float yRot =
                        group.sourceTransform
                            .eulerAngles.y;


                    if (clone.flipX ^
                        clone.flipZ)
                    {
                        yRot =
                            -yRot;
                    }


                    yRot =
                        Mathf.Repeat(
                            yRot + 180f,
                            360f
                        ) - 180f;


                    rotation =
                        Quaternion.Euler(
                            0f,
                            yRot,
                            0f
                        );
                }
                else
                {
                    position =
                        clone.transform.position;


                    rotation =
                        clone.transform.rotation;
                }


                // =================================================
                // OWN-MATERIAL CLONES
                // =================================================

                if (clone.useOwnMaterial)
                {
                    clone.transform.position =
                        position;


                    clone.transform.rotation =
                        rotation;


                    continue;
                }


                // =================================================
                // BUILD FINAL DRAW MATRIX
                // =================================================

                for (int r = 0;
                     r < group.renderers.Count;
                     r++)
                {
                    var rendererData =
                        group.renderers[r];


                    Matrix4x4 matrix;


                    // =============================================
                    // SKINNED PLAYER
                    // =============================================
                    //
                    // Combine synchronized position/rotation with
                    // the stored room, clone, and renderer scale.
                    // =============================================

                    if (rendererData.isSkinned)
                    {
                        matrix =

                            Matrix4x4.TRS(
                                position,
                                rotation,
                                Vector3.one
                            )

                            *

                            clone.childLocalMatrices[r];
                    }
                    else
                    {
                        // Regular mesh path for stairs / buttons.
                        matrix =

                            Matrix4x4.TRS(
                                position,
                                rotation,
                                Vector3.one
                            )

                            *

                            clone.childLocalMatrices[r];
                    }


                    // =================================================
                    // NORMAL VS MIRRORED WINDING
                    // =================================================

                    bool mirrored =
                        IsMirrored(matrix);


                    for (int i = 0;
                         i <
                         rendererData
                             .normalMatricesPerMaterial
                             .Length;
                         i++)
                    {
                        if (mirrored)
                        {
                            rendererData
                                .mirroredMatricesPerMaterial[i]
                                .Add(matrix);
                        }
                        else
                        {
                            rendererData
                                .normalMatricesPerMaterial[i]
                                .Add(matrix);
                        }
                    }
                }
            }


            // ====================================================
            // DRAW
            // ====================================================

            foreach (var rendererData
                     in group.renderers)
            {
                if (rendererData.bakedMesh == null)
                {
                    continue;
                }


                for (int matIndex = 0;
                     matIndex <
                     rendererData.materials.Length;
                     matIndex++)
                {
                    // Positive determinant.
                    DrawBatched(
                        rendererData.bakedMesh,
                        matIndex,
                        rendererData.materials[
                            matIndex
                        ],
                        rendererData
                            .normalMatricesPerMaterial[
                                matIndex
                            ]
                    );


                    // Negative determinant.
                    //
                    // SAME material.
                    // Reversed triangle winding.
                    DrawBatched(
                        rendererData.mirroredBakedMesh,
                        matIndex,
                        rendererData.materials[
                            matIndex
                        ],
                        rendererData
                            .mirroredMatricesPerMaterial[
                                matIndex
                            ]
                    );
                }
            }
        }
    }


    // ============================================================
    // DRAW BATCH
    // ============================================================

    void DrawBatched(
        Mesh mesh,
        int submeshIndex,
        Material material,
        List<Matrix4x4> matrices)
    {
        if (mesh == null ||
            material == null ||
            matrices == null ||
            matrices.Count == 0)
        {
            return;
        }


        if (submeshIndex < 0 ||
            submeshIndex >=
                mesh.subMeshCount)
        {
            return;
        }


        int batches =
            Mathf.CeilToInt(
                (float)matrices.Count /
                maxInstancesPerBatch
            );


        for (int batchIndex = 0;
             batchIndex < batches;
             batchIndex++)
        {
            int startIndex =
                batchIndex *
                maxInstancesPerBatch;


            int count =
                Mathf.Min(
                    maxInstancesPerBatch,
                    matrices.Count -
                    startIndex
                );


            var batchMatrices =
                matrices.GetRange(
                    startIndex,
                    count
                );


            Graphics.DrawMeshInstanced(
                mesh,
                submeshIndex,
                material,
                batchMatrices,
                null,
                UnityEngine.Rendering
                    .ShadowCastingMode.On,
                true,
                0,
                Camera.main
            );
        }
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    void OnDestroy()
    {
        foreach (var group
                 in sources.Values)
        {
            foreach (var rendererData
                     in group.renderers)
            {
                if (rendererData.bakedMesh != null)
                {
                    Destroy(
                        rendererData.bakedMesh
                    );
                }


                if (rendererData.mirroredBakedMesh != null)
                {
                    Destroy(
                        rendererData.mirroredBakedMesh
                    );
                }
            }
        }


        if (Instance == this)
        {
            Instance = null;
        }
    }


    // ============================================================
    // EDITOR DEBUG
    // ============================================================

    void OnGUI()
    {
        if (!Application.isEditor)
        {
            return;
        }


        int y = 10;


        foreach (var kvp
                 in sources)
        {
            var group =
                kvp.Value;


            GUI.Label(
                new Rect(
                    10,
                    y,
                    600,
                    20
                ),
                $"{kvp.Key}: " +
                $"{group.clones.Count} clones, " +
                $"{group.renderers.Count} renderers"
            );


            y += 20;
        }
    }
    // Gets accumulated scale from descendant up to,
    // but not including, ancestor.
    //
    // Position and rotation are ignored. This keeps any scale
    // between cloneTransform and a SkinnedMeshRenderer after BakeMesh.
    Vector3 GetLocalNetScaleToAncestor(
        Transform descendant,
        Transform ancestor)
    {
        Vector3 scale = Vector3.one;

        Transform current = descendant;

        while (current != null &&
            current != ancestor)
        {
            scale.x *= current.localScale.x;
            scale.y *= current.localScale.y;
            scale.z *= current.localScale.z;

            current = current.parent;
        }

        return scale;
    }
}
