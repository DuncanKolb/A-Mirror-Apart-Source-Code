// NodeWalker.cs
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

public class NodeWalker : MonoBehaviour
{
    public float speed = 5f;
    public GameObject highlightPrefab;

    public Transform currentCube;

    public List<Node> currentPath;
    public int currentNodeIndex = 0;
    private GameObject highlightIndicator;
    public List<Vector3> movementSteps = new List<Vector3>();
    public Vector3 moveStep = Vector3.zero;
    private bool okToRefresh;

    public bool walkedToMidPoint;

    private Node targetNodeStored;
    private Node pendingTargetNode = null;
    private bool pendingTarget = false;
    private bool talkingPointFired = false;

    [SerializeField] private Animator animator;
    private PlayerState currentState = PlayerState.Idle;

    private Vector3 lastPosition;
    public float moveThreshold = 0.001f;

    void Start()
    {
        targetNodeStored = FindNodeAtPosition();
        highlightIndicator = Instantiate(highlightPrefab);
        highlightIndicator.SetActive(false);
        lastPosition = transform.position;
        RayCastDown();
    }

    void Update()
    {
        if (movementSteps.Count >= 1)
        {
            //moveStep = movementSteps[currentNodeIndex + ((currentNodeIndex == movementSteps.Count - 1)? 0 : 1)];
            moveStep = movementSteps[currentNodeIndex];
        }

        HandleMouseInput();

        if ((transform.position - lastPosition).sqrMagnitude > moveThreshold * moveThreshold)
        {
            RayCastDown();
            lastPosition = transform.position;
        }

        if (okToRefresh)
        {
            HandlePathfinding();
        }

        if (currentCube.GetComponent<Node>().movingGround)
        {
            transform.parent = currentCube;//.parent;
        }
        else
        {
            transform.parent = null;
        }

        MoveToNode();
        CheckTalkingPoint();
    }

    private bool IsTextActive()
    {
        if (currentCube == null) return false;

        MirrorTextTrigger trigger = currentCube.GetComponent<MirrorTextTrigger>();

        return trigger != null && (trigger.isActive || trigger.blockingInput);
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsTextActive())
            {
                if (highlightIndicator != null)
                    highlightIndicator.SetActive(false);

                return;
            }

            Node clicked = GetClickedNode();

            if (clicked != null && clicked.isWalkable)
            {
                pendingTargetNode = clicked;

                pendingTarget = true;

                okToRefresh = true;

                ShowHighlightForNode(pendingTargetNode);

                // Movement click sound
                if (SFXManager.Instance != null)
                {
                    SFXManager.Instance.PlayClick();
                }
            }
            else
            {
                pendingTargetNode = null;
                pendingTarget = false;
                okToRefresh = false;

                if (highlightIndicator != null)
                    highlightIndicator.SetActive(false);
            }
        }
    }

    private Transform previousCube = null;

    private void CheckTalkingPoint()
    {
        if (currentCube == null) return;

        Node node = currentCube.GetComponent<Node>();

        if (node == null) return;

        // Reset when entering a new cube
        if (currentCube != previousCube)
        {
            talkingPointFired = false;
            previousCube = currentCube;
        }

        bool isStationary =
            (currentPath == null || currentPath.Count == 0);

        if (node.isTalkingPoint &&
            isStationary &&
            !talkingPointFired)
        {
            talkingPointFired = true;

            MirrorTextTrigger trigger =
                currentCube.GetComponent<MirrorTextTrigger>();

            if (trigger != null)
                trigger.Activate();
        }
    }

    private void HandlePathfinding()
    {
        TryApplyPendingTarget();
    }

    private void TryApplyPendingTarget()
    {
        if (!pendingTarget || pendingTargetNode == null)
        {
            if (!pendingTarget)
            {
                okToRefresh = false;
            }

            return;
        }

        Node targetNode = pendingTargetNode;

        List<Node> path = FindPathToNode(targetNode);

        if (path != null && path.Count >= 1)
        {
            // Apply path when standing exactly on a node
            if (IsPositionRounded(transform.position) && targetNode.isWalkable)
            {
                okToRefresh = false;
                pendingTarget = false;

                targetNodeStored = targetNode;

                currentPath = path;
                currentNodeIndex = 0;
                walkedToMidPoint = false;

                if (currentPath.Count > 0)
                {
                    Node lastNode =
                        currentPath[currentPath.Count - 1];

                    ShowHighlightForNode(lastNode);

                    UpdateMovementSteps();
                }
                else
                {
                    if (highlightIndicator != null)
                        highlightIndicator.SetActive(false);
                }
            }
        }
        else
        {
            // Clear invalid path request
            okToRefresh = false;
            pendingTarget = false;
            pendingTargetNode = null;

            if (highlightIndicator != null)
                highlightIndicator.SetActive(false);
        }
    }

    private void ShowHighlightForNode(Node node)
    {
        if (highlightIndicator == null || IsTextActive())
            return;

        if (node == null)
        {
            highlightIndicator.SetActive(false);
            return;
        }

        highlightIndicator.transform.position =
            node.GetWalkPoint();

        highlightIndicator.SetActive(true);

        if (node.isLadder)
        {
            Vector3 ladderEuler = node.transform.eulerAngles;

            highlightIndicator.transform.rotation =
                Quaternion.Euler(-90f, ladderEuler.y, 0f);

            Debug.Log("is ladder");

            highlightIndicator.transform.localScale =
                new Vector3(0.4f, 0.07f, 0.4f);
        }
        else if (node.isStair)
        {
            Vector3 stairEuler = node.transform.eulerAngles;

            highlightIndicator.transform.rotation =
                Quaternion.Euler(-45f, stairEuler.y, 0f);

            highlightIndicator.transform.localScale =
                new Vector3(0.4f, 0.1f, 0.4f);
        }
        else
        {
            highlightIndicator.transform.rotation =
                Quaternion.Euler(0f, 0f, 0f);

            highlightIndicator.transform.localScale =
                new Vector3(0.4f, 0.07f, 0.4f);
        }
    }

    private void RoundPosition()
    {
        Vector3 currentPosition = transform.localPosition;

        //Debug.Log("Round");
        //Debug.Log("CloneRoundPre" + currentPosition);

        transform.localPosition = new Vector3(
            Mathf.Round(currentPosition.x * 4f) / 4f,
            Mathf.Round(currentPosition.y * 4f) / 4f,
            Mathf.Round(currentPosition.z * 4f) / 4f
        );

        //Debug.Log("after round" + transform.position);
    }

    private void ResetPathfindingData()
    {
        if (!pendingTarget)
        {
            okToRefresh = false;
        }

        movementSteps.Clear();
        moveStep = Vector3.zero;
        currentNodeIndex = 0;

        RoundPosition();

        if (IsPositionRounded(transform.position))
        {
            ResetAllMirroredNodes();
        }

        currentPath?.Clear();
    }

    private void ResetAllMirroredNodes()
    {
        string[] playerDuplicates =
        {
            "Player Duplicate A",
            "Player Duplicate B",
            "Player Duplicate C",
            "Player Duplicate D",
            "Player Duplicate E",
            "Player Duplicate F",
            "Player Duplicate G",
            "Player Duplicate H"
        };

        foreach (var playerName in playerDuplicates)
        {
            var player = GameObject.Find(playerName);

            if (player != null)
            {
                //Debug.Log(transform.position);
                player.GetComponent<MirroredNodeWalker2>()?.ResetIndex();
            }
        }
    }

    private bool IsPositionRounded(Vector3 position)
    {
        int normalNodeLayer = LayerMask.NameToLayer("NormalNodes");
        int button1Layer = LayerMask.NameToLayer("Buttons1");
        int button2Layer = LayerMask.NameToLayer("Buttons2");
        int button3Layer = LayerMask.NameToLayer("Buttons3");
        int button4Layer = LayerMask.NameToLayer("Buttons4");
        int button5Layer = LayerMask.NameToLayer("Buttons5");
        int button6Layer = LayerMask.NameToLayer("Buttons6");
        int button7Layer = LayerMask.NameToLayer("Buttons7");
        int button8Layer = LayerMask.NameToLayer("Buttons8");
        int button9Layer = LayerMask.NameToLayer("Buttons9");
        int button10Layer = LayerMask.NameToLayer("Buttons10");

        Node[] allNodes =
            GameObject.FindObjectsByType<Node>(
                FindObjectsSortMode.None
            );

        Node[] nodeObjects = allNodes.Where(n =>
        {
            int layer = n.gameObject.layer;

            return layer == normalNodeLayer ||
                   layer == button1Layer ||
                   layer == button2Layer ||
                   layer == button3Layer ||
                   layer == button4Layer ||
                   layer == button5Layer ||
                   layer == button6Layer ||
                   layer == button7Layer ||
                   layer == button8Layer ||
                   layer == button9Layer ||
                   layer == button10Layer;
        }).ToArray();

        foreach (Node obj in nodeObjects)
        {
            if (obj.gameObject.layer == normalNodeLayer ||
                obj.gameObject.layer == button1Layer ||
                obj.gameObject.layer == button2Layer ||
                obj.gameObject.layer == button3Layer ||
                obj.gameObject.layer == button4Layer ||
                obj.gameObject.layer == button5Layer ||
                obj.gameObject.layer == button6Layer ||
                obj.gameObject.layer == button7Layer ||
                obj.gameObject.layer == button8Layer ||
                obj.gameObject.layer == button9Layer ||
                obj.gameObject.layer == button10Layer)
            {
                Vector3 nodePosition = obj.GetWalkPoint();

                if (Mathf.Approximately(position.x, nodePosition.x) &&
                    Mathf.Approximately(position.y, nodePosition.y) &&
                    Mathf.Approximately(position.z, nodePosition.z))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void MoveToNode()
    {
        if (currentPath != null && currentPath.Count > 0)
        {
            // Movement sound
            if (SFXManager.Instance != null && currentCube != null)
            {
                Node currentNode =
                    currentCube.GetComponent<Node>();

                if (currentNode != null && currentNode.isLadder)
                {
                    SFXManager.Instance.PlayClimbing();
                }
                else
                {
                    SFXManager.Instance.PlayWalking();
                }
            }

            //Node targetNode = currentPath[Mathf.Min(currentNodeIndex+1, currentPath.Count - 1)];
            Node targetNode = currentPath[currentNodeIndex];

            /*
            Node currentNode = currentCube.GetComponent<Node>();

            if (targetNode == currentNode)
            {
                walkedToMidPoint = false;
                currentNodeIndex++;

                if (currentNodeIndex >= currentPath.Count)
                {
                    ResetPathfindingData();
                    highlightIndicator.SetActive(false);
                    return;
                }

                targetNode = currentPath[currentNodeIndex];
            }
            */

            Vector3 targetPosition =
                targetNode.GetComponent<Node>().GetWalkPoint();

            Vector3 direction =
                targetPosition - transform.position;

            if (targetNode.isStair)
            {
                direction.y = 0;
            }
            else if (targetNode.isLadder)
            {
                direction = targetNode.transform.forward;

                //Debug.Log(direction);
                //direction.y = 0;
            }
            else
            {
                direction.y = 0;
            }

            if (direction != Vector3.zero &&
                !currentCube.GetComponent<Node>().isLadder)
            {
                direction.Normalize();

                Quaternion look =
                    Quaternion.LookRotation(direction);

                float rawTargetY =
                    look.eulerAngles.y;

                float currentY =
                    transform.eulerAngles.y;

                // Clockwise for 180-degree ambiguity
                float resolvedTargetY =
                    Resolve180Ambiguity(
                        currentY,
                        rawTargetY,
                        preferClockwise: true
                    );

                Quaternion targetRotation =
                    Quaternion.Euler(
                        0f,
                        resolvedTargetY,
                        0f
                    );

                transform
                    .DORotateQuaternion(
                        targetRotation,
                        0.3f
                    )
                    .SetEase(Ease.OutSine);
            }

            if (Vector3.Distance(
                transform.position,
                targetPosition
            ) < 0.02f)
            {
                walkedToMidPoint = false;
                currentNodeIndex++;

                //Debug.Log("HEREIO");

                // Snap exactly to node before redirecting
                transform.position = targetPosition;

                if (pendingTarget)
                {
                    TryApplyPendingTarget();

                    if (!pendingTarget)
                    {
                        return;
                    }
                }

                if (currentNodeIndex >= currentPath.Count)
                {
                    ResetPathfindingData();
                    highlightIndicator.SetActive(false);
                    return;
                }

                moveStep =
                    movementSteps[currentNodeIndex];

                targetNode =
                    currentPath[currentNodeIndex];

                //targetNode = currentPath[Mathf.Min(currentNodeIndex + 1, currentPath.Count - 1)];

                string[] playerDuplicates =
                {
                    "Player Duplicate A",
                    "Player Duplicate B",
                    "Player Duplicate C",
                    "Player Duplicate D"
                };

                foreach (var playerName in playerDuplicates)
                {
                    var player =
                        GameObject.Find(playerName);

                    if (player != null)
                    {
                        player.GetComponent<MirroredNodeWalker2>()?.Move();
                    }
                }

                if (!currentCube
                        .GetComponent<Node>()
                        .connectedNodes
                        .Contains(targetNode) &&
                    currentCube != targetNode.transform)
                {
                    highlightIndicator.SetActive(false);

                    List<Node> path =
                        FindPathToNode(targetNodeStored);

                    ResetPathfindingData();

                    if (path != null && path.Count >= 1)
                    {
                        if (IsPositionRounded(transform.localPosition) &&
                            IsPositionRounded(targetNodeStored.transform.localPosition) &&
                            targetNodeStored.isWalkable)
                        {
                            ResetPathfindingData();

                            currentPath =
                                FindPathToNode(targetNodeStored);

                            if (currentPath.Count > 0)
                            {
                                highlightIndicator.transform.position =
                                    currentPath[
                                        currentPath.Count - 1
                                    ].transform.position;

                                highlightIndicator.SetActive(true);

                                UpdateMovementSteps();
                            }
                            else
                            {
                                highlightIndicator.SetActive(false);
                            }
                        }
                    }
                }

                if (currentNodeIndex >= currentPath.Count)
                {
                    ResetPathfindingData();
                    highlightIndicator.SetActive(false);
                }
            }

            if (currentPath != null && currentPath.Count > 0)
            {
                //targetNode = currentPath[Mathf.Min(currentNodeIndex +1, currentPath.Count - 1)];

                //targetNode = currentPath[currentNodeIndex];
                //targetPosition = targetNode.GetComponent<Node>().GetWalkPoint();
                //transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

                if (currentCube.GetComponent<Node>().isLadder)
                {
                    speed = 1.3f;
                }
                else
                {
                    speed = 2f;
                }

                if (walkedToMidPoint)
                {
                    transform.position =
                        Vector3.MoveTowards(
                            transform.position,
                            targetPosition,
                            speed * Time.deltaTime
                        );
                }
                else
                {
                    Vector3 closestMidWalkPoint =
                        FindClosestMidWalkPoint(
                            targetNode,
                            transform.position
                        );

                    if (closestMidWalkPoint != Vector3.zero)
                    {
                        targetPosition = closestMidWalkPoint;
                    }
                    else
                    {
                        Debug.Log(
                            "closest Mid walk point not found"
                        );
                    }

                    transform.position =
                        Vector3.MoveTowards(
                            transform.position,
                            targetPosition,
                            speed * Time.deltaTime
                        );

                    if (Vector3.Distance(
                        transform.position,
                        targetPosition
                    ) < 0.000001f)
                    {
                        walkedToMidPoint = true;
                    }
                }

                // ANIMATIONS

                if (currentCube.GetComponent<Node>().isLadder)
                {
                    if (targetNode.GetComponent<Node>().isLadder)
                    {
                        UpdateAnimationState(
                            PlayerState.Climbing
                        );
                    }
                    else
                    {
                        if (targetNode.GetWalkPoint().y >
                            transform.position.y)
                        {
                            UpdateAnimationState(
                                PlayerState.ClimbingTop
                            );
                        }
                        else
                        {
                            if (targetNode.GetWalkPoint().y <
                                transform.position.y)
                            {
                                UpdateAnimationState(
                                    PlayerState.ClimbingBottom
                                );
                            }
                        }
                    }
                }
                else
                {
                    if (currentCube.GetComponent<Node>().isStair)
                    {
                        if (targetNode.GetWalkPoint().y >
                            transform.position.y)
                        {
                            UpdateAnimationState(
                                PlayerState.StairsUp
                            );
                        }
                        else
                        {
                            if (targetNode.GetWalkPoint().y <
                                transform.position.y)
                            {
                                UpdateAnimationState(
                                    PlayerState.StairsDown
                                );
                            }
                        }
                    }
                    else
                    {
                        if (targetNode.isLadder)
                        {
                            float distance =
                                Vector3.Distance(
                                    transform.position,
                                    targetNode.GetWalkPoint()
                                );

                            if (distance < 0.6f)
                            {
                                if (targetNode.GetWalkPoint().y >
                                    transform.position.y)
                                {
                                    UpdateAnimationState(
                                        PlayerState.ClimbBottomReversed
                                    );
                                }
                                else if (
                                    targetNode.GetWalkPoint().y <
                                    transform.position.y
                                )
                                {
                                    UpdateAnimationState(
                                        PlayerState.ClimbTopReveresed
                                    );
                                }
                            }
                            else
                            {
                                if (targetNode.GetWalkPoint().y <
                                    transform.position.y)
                                {
                                    UpdateAnimationState(
                                        PlayerState.walkReversed
                                    );
                                }
                            }
                        }
                        else
                        {
                            UpdateAnimationState(
                                PlayerState.Walking
                            );
                        }
                    }
                }

                if (!currentCube
                        .GetComponent<Node>()
                        .connectedNodes
                        .Contains(targetNode) &&
                    currentCube != targetNode.transform)
                {
                    Debug.Log("snap back");

                    foreach (
                        Node node in
                        currentCube
                            .GetComponent<Node>()
                            .connectedNodes
                    )
                    {
                        Debug.Log(
                            "Connected to: " + node.name
                        );
                    }

                    Debug.Log(targetNode);

                    Debug.Log(
                        currentCube
                            .GetComponent<Node>()
                            .connectedNodes
                            .Contains(targetNode)
                    );

                    Debug.Log(
                        currentCube
                            .GetComponent<Node>()
                            .GetWalkPoint()
                    );

                    Debug.Log(
                        currentCube
                            .GetComponent<Node>()
                            .GetWalkPoint()
                    );

                    Debug.Log(
                        currentCube
                            .GetComponent<Node>()
                            .GetWalkPoint()
                    );

                    Debug.Log(
                        currentCube
                            .GetComponent<Node>()
                            .GetWalkPoint()
                    );

                    transform.position =
                        currentCube
                            .GetComponent<Node>()
                            .GetWalkPoint();

                    //transform.position = walkPoint;

                    Debug.Log(
                        currentCube
                            .GetComponent<Node>()
                            .GetWalkPoint()
                    );

                    ResetPathfindingData();

                    highlightIndicator.SetActive(false);
                }
            }
        }
        else
        {
            // Stop movement sound
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.StopMovement();
            }

            if (currentCube.GetComponent<Node>().isLadder)
            {
                UpdateAnimationState(
                    PlayerState.ClimbingIdle
                );
            }
            else
            {
                UpdateAnimationState(
                    PlayerState.Idle
                );
            }
        }
    }

    private Vector3 FindClosestMidWalkPoint(
        Node node,
        Vector3 currentPosition)
    {
        Vector3 closestPoint = Vector3.zero;
        float closestDistance = Mathf.Infinity;

        foreach (Vector3 point in node.midWalkPoints)
        {
            float distance =
                Vector3.Distance(
                    currentPosition,
                    point
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    public void RayCastDown()
    {
        Vector3 childPos =
            transform.GetChild(0).position;

        // Backward offset
        Vector3 behindOffset =
            -transform.forward * 0.05f;

        // Downward offset
        Vector3 downwardOffset =
            Vector3.up * 0.2f;

        Vector3 playerBottom =
            childPos +
            behindOffset +
            downwardOffset;

        Ray playerRay =
            new Ray(
                playerBottom,
                -transform.up
            );

        RaycastHit playerHit;

        Debug.DrawRay(
            playerRay.origin,
            playerRay.direction * 0.5f,
            Color.white,
            1f
        );

        if (Physics.Raycast(
            playerRay,
            out playerHit,
            0.2f))
        {
            Debug.DrawRay(
                playerRay.origin,
                playerRay.direction * playerHit.distance,
                Color.green,
                1f
            );

            if (playerHit.transform.GetComponent<Node>() != null)
            {
                currentCube =
                    playerHit.transform;

                if (playerHit.transform.GetComponent<Node>().isStair)
                {
                    // DOVirtual.Float(GetBlend(), blend, .1f, SetBlend);
                }
                else
                {
                    // DOVirtual.Float(GetBlend(), 0, .1f, SetBlend);
                }
            }
            else
            {
                if (playerHit.transform.parent != null)
                {
                    if (playerHit
                            .transform
                            .parent
                            .GetComponent<Node>() != null)
                    {
                        currentCube =
                            playerHit.transform.parent.transform;
                    }
                }
            }
        }
        else
        {
            Debug.DrawRay(
                playerRay.origin,
                playerRay.direction * 0.5f,
                Color.red,
                1f
            );

            playerRay =
                new Ray(
                    playerBottom,
                    transform.forward
                );

            Debug.DrawRay(
                playerRay.origin,
                playerRay.direction * 0.5f,
                Color.white,
                1f
            );

            if (Physics.Raycast(
                playerRay,
                out playerHit,
                0.5f))
            {
                Debug.DrawRay(
                    playerRay.origin,
                    playerRay.direction * playerHit.distance,
                    Color.green,
                    1f
                );

                if (playerHit.transform.GetComponent<Node>() != null)
                {
                    currentCube =
                        playerHit.transform;

                    if (playerHit.transform.GetComponent<Node>().isStair)
                    {
                        // DOVirtual.Float(GetBlend(), blend, .1f, SetBlend);
                    }
                    else
                    {
                        // DOVirtual.Float(GetBlend(), 0, .1f, SetBlend);
                    }
                }
                else
                {
                    if (playerHit.transform.parent != null)
                    {
                        if (playerHit
                                .transform
                                .parent
                                .GetComponent<Node>() != null)
                        {
                            currentCube =
                                playerHit.transform.parent.transform;
                        }
                    }
                }
            }
            else
            {
                Debug.DrawRay(
                    playerRay.origin,
                    playerRay.direction * 0.5f,
                    Color.red,
                    1f
                );
            }
        }
    }

    public enum PlayerState
    {
        Idle = 0,
        ClimbingIdle = 1,
        Walking = 2,
        StairsUp = 3,
        StairsDown = 4,

        Climbing = 5,
        ClimbingTop = 6,
        ClimbingBottom = 7,

        ClimbTopReveresed = 8,
        ClimbBottomReversed = 9,
        walkReversed = 10
    }

    void UpdateAnimationState(PlayerState newState)
    {
        if (currentState != newState)
        {
            //Debug.Log(newState+ " " +transform.position);

            animator.SetInteger(
                "State",
                (int)newState
            );

            currentState = newState;
        }
    }

    Node FindNodeAtPosition()
    {
        Node[] allNodes =
            GameObject.FindObjectsByType<Node>(
                FindObjectsSortMode.None
            );

        foreach (Node node in allNodes)
        {
            if (node.transform.position ==
                transform.position)
            {
                return node;
            }
        }

        return null;
    }

    Node GetClickedNode()
    {
        Ray ray =
            Camera.main.ScreenPointToRay(
                Input.mousePosition
            );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            Mathf.Infinity))
        {
            Node clickedNode =
                hit.transform.GetComponent<Node>();

            if (clickedNode == null)
                return null;

            int normalNodesLayer =
                LayerMask.NameToLayer("NormalNodes");

            int button1Layer =
                LayerMask.NameToLayer("Buttons1");

            int button2Layer =
                LayerMask.NameToLayer("Buttons2");

            int button3Layer =
                LayerMask.NameToLayer("Buttons3");

            int button4Layer =
                LayerMask.NameToLayer("Buttons4");

            int button5Layer =
                LayerMask.NameToLayer("Buttons5");

            int button6Layer =
                LayerMask.NameToLayer("Buttons6");

            int button7Layer =
                LayerMask.NameToLayer("Buttons7");

            int button8Layer =
                LayerMask.NameToLayer("Buttons8");

            int button9Layer =
                LayerMask.NameToLayer("Buttons9");

            int button10Layer =
                LayerMask.NameToLayer("Buttons10");

            // Validate node layers
            Transform current = hit.transform;

            while (current != null)
            {
                int layer =
                    current.gameObject.layer;

                if (layer == normalNodesLayer ||
                    layer == button1Layer ||
                    layer == button2Layer ||
                    layer == button3Layer ||
                    layer == button4Layer ||
                    layer == button5Layer ||
                    layer == button6Layer ||
                    layer == button7Layer ||
                    layer == button8Layer ||
                    layer == button9Layer ||
                    layer == button10Layer)
                {
                    return clickedNode;
                }

                current = current.parent;
            }
        }

        return null;
    }

    List<Node> FindPathToNode(Node targetNode)
    {
        Node startNode =
            currentCube.transform.GetComponent<Node>();

        return AStarPathfinding(
            startNode,
            targetNode
        );
    }

    Node GetClosestNode()
    {
        Node[] nodes =
            GameObject.FindObjectsByType<Node>(
                FindObjectsSortMode.None
            );

        Node closestNode = null;
        float closestDistance = float.MaxValue;

        foreach (Node node in nodes)
        {
            float distance =
                Vector3.Distance(
                    transform.position,
                    node.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestNode = node;
            }
        }

        return closestNode;
    }

    List<Node> AStarPathfinding(
        Node startNode,
        Node targetNode)
    {
        var gScore =
            new Dictionary<Node, float>();

        var hScore =
            new Dictionary<Node, float>();

        var cameFrom =
            new Dictionary<Node, Node>();

        var openSet =
            new List<Node> { startNode };

        var closedSet =
            new HashSet<Node>();

        gScore[startNode] = 0;

        hScore[startNode] =
            Vector3.Distance(
                startNode.transform.position,
                targetNode.transform.position
            );

        while (openSet.Count > 0)
        {
            Node currentNode =
                openSet
                    .OrderBy(
                        node =>
                            gScore[node] +
                            hScore[node]
                    )
                    .First();

            if (currentNode == targetNode)
            {
                return RetracePath(
                    cameFrom,
                    startNode,
                    targetNode
                );
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            foreach (
                Node neighbor in
                currentNode.connectedNodes
            )
            {
                if (closedSet.Contains(neighbor) ||
                    !neighbor.isWalkable)
                    continue;

                float tentativeGScore =
                    gScore[currentNode] +
                    Vector3.Distance(
                        currentNode.transform.position,
                        neighbor.transform.position
                    );

                if (!openSet.Contains(neighbor) ||
                    tentativeGScore <
                    gScore[neighbor])
                {
                    cameFrom[neighbor] =
                        currentNode;

                    gScore[neighbor] =
                        tentativeGScore;

                    hScore[neighbor] =
                        Vector3.Distance(
                            neighbor.transform.position,
                            targetNode.transform.position
                        );

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return new List<Node>();
    }

    List<Node> RetracePath(
        Dictionary<Node, Node> cameFrom,
        Node startNode,
        Node endNode)
    {
        List<Node> path =
            new List<Node>();

        Node currentNode =
            endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);

            if (!cameFrom.TryGetValue(
                currentNode,
                out currentNode))
            {
                Debug.LogError(
                    "Error: Cannot retrace the path properly."
                );

                return new List<Node>();
            }
        }

        path.Add(startNode);

        path.Reverse();

        return path;
    }

    void UpdateMovementSteps()
    {
        movementSteps.Clear();

        Vector3 prevNodePos =
            transform.position;

        foreach (var node in currentPath)
        {
            Vector3 currentNodePos =
                node.GetWalkPoint();

            movementSteps.Add(
                currentNodePos -
                prevNodePos
            );

            prevNodePos =
                currentNodePos;
        }
    }

    // Resolve ambiguous 180-degree rotation
    float Resolve180Ambiguity(
        float currentY,
        float rawTargetY,
        bool preferClockwise)
    {
        float diff =
            Mathf.DeltaAngle(
                currentY,
                rawTargetY
            );

        if (Mathf.Abs(diff) >= 179f)
        {
            diff =
                preferClockwise
                    ? 179f
                    : -179f;
        }

        return currentY + diff;
    }
}