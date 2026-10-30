using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class Node : MonoBehaviour
{
    public List<Node> connectedNodes = new List<Node>();

    public bool isWalkable = true;
    public bool isStair = false;
    public bool movingGround = false;
    public bool isLadder = false;
    public bool isTalkingPoint = false;
    public List<Vector3> midWalkPoints = new List<Vector3>();

    public bool active = false;

    [Header("Manual Overrides")]
    [Tooltip("Drag in any nodes that this node should NEVER connect to, no matter what the raycasts/logic say. Blocks the connection in both directions even if only listed on one side.")]
    public List<Node> forbiddenConnections = new List<Node>();

    [Header("Node Above Check")]
    public bool checkForNodeAbove = false;
    public float nodeAboveCheckDistance = 1f;

    private Collider[] nodeCollidersToToggle;

    private void Awake()
    {
        CacheNodeCollidersToToggle();
    }

    private void Start()
    {
        UpdatePossiblePaths();
        StartCoroutine(FilterMutualConnections());
    }

    private IEnumerator FilterMutualConnections()
    {
        // Wait for all nodes to build their connections
        yield return null;

        connectedNodes = connectedNodes
            .Where(node => node.connectedNodes.Contains(this))
            .Where(node => !IsForbiddenConnection(node))
            .ToList();
    }

    private void Update()
    {
        if (active)
        {
            RoundNodeTransformValues();

            if (checkForNodeAbove)
            {
                // Ladders check forward, other nodes check above
                bool blocked = isLadder
                    ? CheckForNodeDirectlyInFront()
                    : CheckForNodeDirectlyAbove();

                if (blocked)
                {
                    connectedNodes.Clear();
                    midWalkPoints.Clear();
                    return;
                }
            }

            UpdatePossiblePaths();
            //connectedNodes = connectedNodes.Where(node => node.connectedNodes.Contains(this)).ToList();
        }
    }

    private void RoundNodeTransformValues()
    {
        transform.localPosition = RoundVector3ToHundredth(transform.localPosition);
        transform.localScale = RoundVector3ToHundredth(transform.localScale);

        Vector3 roundedLocalRotation = RoundVector3ToNearestDegree(transform.localEulerAngles);
        transform.localRotation = Quaternion.Euler(roundedLocalRotation);
    }

    private Vector3 RoundVector3ToHundredth(Vector3 value)
    {
        return new Vector3(
            Mathf.Round(value.x * 100f) / 100f,
            Mathf.Round(value.y * 100f) / 100f,
            Mathf.Round(value.z * 100f) / 100f
        );
    }

    private Vector3 RoundVector3ToNearestDegree(Vector3 value)
    {
        return new Vector3(
            Mathf.Round(value.x),
            Mathf.Round(value.y),
            Mathf.Round(value.z)
        );
    }

    private void CacheNodeCollidersToToggle()
    {
        // Box and mesh colliders used for node blocking
        nodeCollidersToToggle = GetComponentsInChildren<Collider>(true)
            .Where(collider => collider is BoxCollider || collider is MeshCollider)
            .ToArray();
    }

    private bool IsForbiddenConnection(Node other)
    {
        if (other == null) return false;
        if (forbiddenConnections.Contains(other)) return true;
        if (other.forbiddenConnections.Contains(this)) return true;
        return false;
    }

    private bool CheckForNodeDirectlyAbove()
    {
        if (!checkForNodeAbove || isLadder)
        {
            SetNodeCollidersActive(true);
            return false;
        }

        if (nodeCollidersToToggle == null || nodeCollidersToToggle.Length == 0)
        {
            CacheNodeCollidersToToggle();
        }

        // Ray starts just below walk point
        Vector3 rayStart = GetWalkPoint() - Vector3.up * 0.02f;
        Vector3 rayDirection = Vector3.up;
        float rayDistance = nodeAboveCheckDistance + 0.02f;

        Debug.DrawRay(rayStart, rayDirection * rayDistance, UnityEngine.Color.cyan);

        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            rayDirection,
            rayDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
        {
            Collider hitCollider = hit.collider;

            if (hitCollider == null)
            {
                continue;
            }

            // Ignore own colliders
            if (nodeCollidersToToggle != null && nodeCollidersToToggle.Contains(hitCollider))
            {
                continue;
            }

            // Only use box and mesh colliders
            bool hitValidColliderType =
                hitCollider is BoxCollider ||
                hitCollider is MeshCollider;

            if (!hitValidColliderType)
            {
                continue;
            }

            Node hitNode = hitCollider.GetComponent<Node>();

            if (hitNode == null)
            {
                hitNode = hitCollider.GetComponentInParent<Node>();
            }

            // Disable colliders when blocked
            if (hitNode != null && hitNode != this)
            {
                SetNodeCollidersActive(false);
                return true;
            }
        }

        SetNodeCollidersActive(true);
        return false;
    }

    private bool CheckForNodeDirectlyInFront()
    {
        if (!checkForNodeAbove)
        {
            SetNodeCollidersActive(true);
            return false;
        }

        if (nodeCollidersToToggle == null || nodeCollidersToToggle.Length == 0)
        {
            CacheNodeCollidersToToggle();
        }

        // Check ladder climb path
        Vector3 rayStart =
            transform.position +
            transform.forward * 0.02f;

        Vector3 rayDirection = -transform.forward;
        float rayDistance = nodeAboveCheckDistance + 0.02f;

        Debug.DrawRay(
            rayStart,
            rayDirection * rayDistance,
            UnityEngine.Color.magenta
        );

        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            rayDirection,
            rayDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
        {
            Collider hitCollider = hit.collider;

            if (hitCollider == null)
            {
                continue;
            }

            // Ignore own colliders
            if (nodeCollidersToToggle != null && nodeCollidersToToggle.Contains(hitCollider))
            {
                continue;
            }

            // Only use box and mesh colliders
            bool hitValidColliderType =
                hitCollider is BoxCollider ||
                hitCollider is MeshCollider;

            if (!hitValidColliderType)
            {
                continue;
            }

            Node hitNode = hitCollider.GetComponent<Node>();

            if (hitNode == null)
            {
                hitNode = hitCollider.GetComponentInParent<Node>();
            }

            // Disable colliders when blocked
            if (hitNode != null && hitNode != this)
            {
                SetNodeCollidersActive(false);
                return true;
            }
        }

        SetNodeCollidersActive(true);
        return false;
    }

    private void SetNodeCollidersActive(bool shouldBeActive)
    {
        if (nodeCollidersToToggle == null || nodeCollidersToToggle.Length == 0)
        {
            CacheNodeCollidersToToggle();
        }

        foreach (Collider collider in nodeCollidersToToggle)
        {
            if (collider != null && collider.enabled != shouldBeActive)
            {
                collider.enabled = shouldBeActive;
            }
        }
    }

    public void UpdatePossiblePaths()
    {
        connectedNodes.Clear();
        midWalkPoints.Clear();

        Vector3[] directions;

        if (isStair)
        {
            directions = new Vector3[]
            {
                transform.forward,
                -transform.forward
            };
        }
        else
        {
            if (isLadder)
            {
                directions = new Vector3[]
                {
                    transform.forward,
                    -transform.forward,
                    transform.up,
                    -transform.up
                };
            }
            else
            {
                directions = new Vector3[]
                {
                    -transform.right,
                    transform.right,
                    transform.forward,
                    -transform.forward
                };
            }
        }

        if (!isLadder)
        {
            foreach (Vector3 direction in directions)
            {
                Vector3 edgePosition = GetEdgePosition(
                    transform.position,
                    direction,
                    (!isStair || direction == transform.forward ? true : false),
                    transform.localScale.y,
                    transform.localScale.x,
                    transform.localScale.z
                );

                edgePosition = RoundVector3ToHundredth(edgePosition);

                midWalkPoints.Add(edgePosition);

                Ray ray = new Ray(edgePosition, direction);

                if (Physics.Raycast(ray, out RaycastHit hit, 0.5f))
                {
                    Node target = hit.transform.GetComponent<Node>();

                    if (target != null &&
                        !IsForbiddenConnection(target) &&
                        !connectedNodes.Contains(target))
                    {
                        connectedNodes.Add(target);
                    }
                    else
                    {
                    }
                }
            }
        }
        else
        {
            foreach (Vector3 direction in directions)
            {
                Vector3 ladderPosition = GetLadderPosition(
                    transform,
                    direction,
                    transform.localScale.y,
                    transform.localScale.x,
                    transform.localScale.z
                );

                ladderPosition = RoundVector3ToHundredth(ladderPosition);

                if (direction == transform.forward ||
                    direction == -transform.forward)
                {
                    midWalkPoints.Add(ladderPosition);
                }

                Vector3 worldDirection =
                    transform.TransformDirection(direction);

                if (direction == transform.forward)
                {
                    //Debug.Log(direction + "  " + Vector3.forward + "  " + worldDirection);
                }

                Debug.DrawRay(
                    ladderPosition,
                    direction * 0.5f,
                    UnityEngine.Color.white,
                    1f
                );

                if (Physics.Raycast(
                    ladderPosition,
                    direction,
                    out RaycastHit hit,
                    0.5f))
                {
                    Node target = hit.transform.GetComponent<Node>();

                    if (target != null &&
                        !IsForbiddenConnection(target) &&
                        !connectedNodes.Contains(target))
                    {
                        connectedNodes.Add(target);
                    }
                }
            }
        }
    }

    public Vector3 GetWalkPoint()
    {
        Vector3 walkPoint = new Vector3(
            transform.position.x,
            transform.position.y +
            (isStair || isLadder
                ? 0
                : (transform.localScale.y / 2f)),
            transform.position.z
        );

        return walkPoint;
    }

    public Vector3 GetLadderPosition(
        Transform ladderTransform,
        Vector3 direction,
        float height,
        float width,
        float depth)
    {
        Vector3 offset = Vector3.zero;

        if (direction == transform.up)
        {
            offset += ladderTransform.up * ((height * 0.7f) / 2);
        }
        else if (direction == -transform.up)
        {
            offset -= ladderTransform.up * ((height * 0.7f) / 2);
        }
        else if (direction == transform.forward)
        {
            offset += ladderTransform.up * (height / 2);
            // offset += ladderTransform.forward * (depth / 2);
        }
        else if (direction == -transform.forward)
        {
            offset -= ladderTransform.up * (height / 2);
            // offset -= ladderTransform.forward * (depth / 2);
        }

        return ladderTransform.position + offset;
    }

    public Vector3 GetEdgePosition(
        Vector3 center,
        Vector3 direction,
        bool isTop,
        float height,
        float width,
        float depth)
    {
        Vector3 offset = Vector3.zero;

        if (isTop)
        {
            offset += Vector3.up * (height / 2);
        }
        else
        {
            offset -= Vector3.up * (height / 2);
        }

        if (direction == Vector3.left)
        {
            offset -= Vector3.right * (width / 2) * 0.9f;
        }
        else if (direction == Vector3.right)
        {
            offset += Vector3.right * (width / 2) * 0.9f;
        }
        else if (direction == Vector3.forward)
        {
            offset += Vector3.forward * (depth / 2) * 0.9f;
        }
        else if (direction == Vector3.back)
        {
            offset -= Vector3.forward * (depth / 2) * 0.9f;
        }

        return center + offset;
    }

    private void OnDrawGizmos()
    {
        // Walk point
        Vector3 walkPoint = GetWalkPoint();

        // Yellow = walkable, red = blocked
        Gizmos.color = isWalkable ? Color.yellow : Color.red;
        Gizmos.DrawSphere(walkPoint, 0.2f);

        // Node connections
        Gizmos.color = Color.green;

        foreach (var node in connectedNodes)
        {
            if (node != null)
            {
                Vector3 targetWalkPoint = new Vector3(
                    node.transform.position.x,
                    node.transform.position.y +
                        (node.isStair || node.isLadder
                            ? 0f
                            : node.transform.localScale.y / 2f),
                    node.transform.position.z
                );

                Gizmos.DrawLine(walkPoint, targetWalkPoint);
            }
        }

        // Midpoints
        Gizmos.color = Color.green;

        foreach (Vector3 point in midWalkPoints)
        {
            Gizmos.DrawSphere(point, 0.1f);
        }
    }
}