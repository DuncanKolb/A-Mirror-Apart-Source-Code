// MirroredNodeWalker2.cs
using TMPro;
using UnityEngine;
using DG.Tweening;


public class MirroredNodeWalker2 : MonoBehaviour
{
    public float speed = 5f;
    public NodeWalker originalNodeWalker;
    public bool flipX;
    public bool flipZ;
    private Node targetNode;
    public Transform playerTransform;
    public bool noStartFlip = false;
    public bool walkedToMidPoint;

    public bool move;

    public Transform currentCube;
    [SerializeField] private Animator animator;
    private PlayerState currentState = PlayerState.Idle;

    private Vector3 lastPosition;
    public float moveThreshold = 0.001f; // small tolerance to avoid jitter calls

    // Idle delay
    private float idleDelayTimer = 0f;
    [SerializeField] private float idleDelayDuration = 0.3f;
    private PlayerState pendingIdleState = PlayerState.Idle;


    void Start()
    {
        SetInitialPosition();
        lastPosition = transform.position;
        RayCastDown();
    }

    void Update()
    {
        //UpdateNodeIndex();
        if ((transform.position - lastPosition).sqrMagnitude > moveThreshold * moveThreshold)
        {
            RayCastDown();
            lastPosition = transform.position;
        }

        if (currentCube.GetComponent<Node>().movingGround)
        {
            transform.parent = currentCube;//.parent;
        }
        else
        {
            transform.parent = null;
        }
        MoveMirroredNode();
    }
    
    public void RayCastDown()
    {
        Vector3 childPos = transform.GetChild(0).position;

        // Backward offset
        Vector3 behindOffset = -transform.forward * 0.05f;

        // Downward offset
        Vector3 downwardOffset = Vector3.up * 0.2f;

        Vector3 playerBottom = childPos + behindOffset + downwardOffset;


        Ray playerRay = new Ray(playerBottom, -transform.up);
        RaycastHit playerHit;

        Debug.DrawRay(playerRay.origin, playerRay.direction * 0.5f, Color.white, 1f);

        if (Physics.Raycast(playerRay, out playerHit, 0.2f))
        {
            Debug.DrawRay(playerRay.origin, playerRay.direction * playerHit.distance, Color.green, 1f);

            if (playerHit.transform.GetComponent<Node>() != null)
            {
                currentCube = playerHit.transform;

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


                    if (playerHit.transform.parent.GetComponent<Node>() != null)
                    {

                        currentCube = playerHit.transform.parent.transform;
                    }
                }
            }
        }
        else
        {
            Debug.DrawRay(playerRay.origin, playerRay.direction * 0.5f, Color.red, 1f);

            playerRay = new Ray(playerBottom, transform.forward);

            Debug.DrawRay(playerRay.origin, playerRay.direction * 0.5f, Color.white, 1f);

            if (Physics.Raycast(playerRay, out playerHit, 0.5f))
            {
                Debug.DrawRay(playerRay.origin, playerRay.direction * playerHit.distance, Color.green, 1f);

                if (playerHit.transform.GetComponent<Node>() != null)
                {
                    currentCube = playerHit.transform;

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


                        if (playerHit.transform.parent.GetComponent<Node>() != null)
                        {

                            currentCube = playerHit.transform.parent.transform;
                        }
                    }
                }
            }
            else
            {
                Debug.DrawRay(playerRay.origin, playerRay.direction * 0.5f, Color.red, 1f);

            }

        }
    }

    public void ResetIndex()
    {
        move = false;
        walkedToMidPoint = false;
        //Debug.Log("RestIndex");
        RoundPosition();
    }

    public void Move()
    {
        Vector3 flippedStep = FlipMovementSteps(originalNodeWalker.moveStep);
        targetNode = FindNodeAtPosition(currentCube.GetComponent<Node>().GetWalkPoint() + flippedStep);
        //.Log("!!!!!" + flippedStep);
        //Debug.Log("!!!!!" + targetNode + "   " + targetNode.GetWalkPoint() + "  "  );
        
        move = true;
    }

    private void SetInitialPosition()
    {
        if (noStartFlip || playerTransform == null) return;

        Vector3 newLocalPosition = transform.localPosition;
        newLocalPosition.x = flipX ? -playerTransform.localPosition.x : playerTransform.localPosition.x;
        newLocalPosition.z = flipZ ? -playerTransform.localPosition.z : playerTransform.localPosition.z;
        transform.localPosition = newLocalPosition;
    }

    

    private void RoundPosition()
    {
        Vector3 currentPosition = transform.localPosition;
        //Debug.Log("CloneRoundPre" + currentPosition);
        transform.localPosition = new Vector3(
            Mathf.Round(currentPosition.x*4f)/4f,
            Mathf.Round(currentPosition.y * 2f) / 2f,
            Mathf.Round(currentPosition.z*4)/4f
        );
        //Debug.Log("CloneRoundPose" + transform.position);
    }

    private Vector3 FlipMovementSteps(Vector3 originalSteps)
    {
        if (flipX)
        {
            originalSteps.x = -originalSteps.x;
        }

        if (flipZ)
        {
            originalSteps.z = -originalSteps.z;
        }

        return originalSteps;
    }

    private Node FindNodeAtPosition(Vector3 targetPosition)
    {
        Node[] allNodes = GameObject.FindObjectsByType<Node>(FindObjectsSortMode.None);


        //Debug.Log(" 1 : " + targetPosition);
        Vector3 currentPosition = targetPosition;
        float roundedX = Mathf.Round(currentPosition.x *10)/10;
        float roundedY = Mathf.Round(currentPosition.y *10)/10;
        float roundedZ = Mathf.Round(currentPosition.z * 10) / 10;
        Vector3 roundedPosition = new Vector3(roundedX, roundedY, roundedZ);
        targetPosition = roundedPosition;
        //Debug.Log(" 2 : " + targetPosition);

        foreach (Node node in allNodes)
        {
            
            if (node.GetWalkPoint() == targetPosition && currentCube.GetComponent<Node>().connectedNodes.Contains(node))
            {
                return node;
            }
        }

        foreach (Node node in allNodes)
        {
            if (node.GetWalkPoint() == transform.position )
            {
                //Debug.Log("here :(");
                return node;
            }
        }

        return null;
    }


    private void MoveMirroredNode()
    {
        if (move)
        {
            if (targetNode != null && targetNode.isWalkable)
            {


                Vector3 targetPosition = targetNode.GetComponent<Node>().GetWalkPoint();

                // Movement direction
                Vector3 worldDirection = targetPosition - transform.position;

                if (currentCube.GetComponent<Node>().isLadder)
                {
                    speed = 1.3f;
                }
                else
                {
                    speed = 2f;
                }

                if (targetNode.isStair)
                {
                    worldDirection.y = 0;
                }
                else if (targetNode.isLadder)
                {
                    // Ladder movement uses local forward
                    worldDirection = targetNode.transform.TransformDirection(Vector3.forward);
                }
                else
                {
                    worldDirection.y = 0;
                }

                // Rotate toward movement direction
                if (worldDirection != Vector3.zero && !currentCube.GetComponent<Node>().isLadder)
                {
                    worldDirection.Normalize();
                    Quaternion look = Quaternion.LookRotation(worldDirection);
                    float rawTargetY = look.eulerAngles.y;

                    float currentY = transform.eulerAngles.y;

                    // Rotation direction for mirrored rooms
                    bool exactlyOneFlip = (flipX ^ flipZ); // xor
                    bool preferClockwise = !exactlyOneFlip;

                    float resolvedTargetY = Resolve180Ambiguity(currentY, rawTargetY, preferClockwise);

                    Quaternion targetRotation = Quaternion.Euler(0f, resolvedTargetY, 0f);

                    transform.DORotateQuaternion(targetRotation, 0.3f).SetEase(Ease.OutSine);
                }




                if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
                {
                    move = false;
                    RoundPosition();
                    walkedToMidPoint = false;

                }
                else
                {
                    if (walkedToMidPoint)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
                        //Debug.Log("move to taregt Node" +targetPosition + " " + transform.position);

                        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
                        {
                            // Position reached
                            RoundPosition();
                            walkedToMidPoint = false;
                        }
                    }
                    else
                    {
                        Vector3 closestMidWalkPoint = FindClosestMidWalkPoint(targetNode, transform.position);

                        if ((closestMidWalkPoint != Vector3.zero) && (targetNode != currentCube))
                        {
                            targetPosition = closestMidWalkPoint;

                            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

                            //Debug.Log("move to mid point");
                        }
                        else
                        {
                            //Debug.Log("closest Mid walk point not found");
                        }

                        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
                        {
                            walkedToMidPoint = true;

                        }
                    }

                    // ANIMATIONS

                    if (currentCube.GetComponent<Node>().isLadder)
                    {
                        if (targetNode.GetComponent<Node>().isLadder)
                        {
                            UpdateAnimationState(PlayerState.Climbing);
                        }
                        else
                        {
                            if (targetNode.GetWalkPoint().y > transform.position.y)
                            {

                                UpdateAnimationState(PlayerState.ClimbingTop);
                            }
                            else
                            {

                                if (targetNode.GetWalkPoint().y < transform.position.y)
                                {
                                    UpdateAnimationState(PlayerState.ClimbingBottom);
                                }

                            }
                        }
                    }
                    else
                    {
                        if (currentCube.GetComponent<Node>().isStair)
                        {
                            if (targetNode.GetWalkPoint().y > transform.position.y)
                            {

                                UpdateAnimationState(PlayerState.StairsUp);
                            }
                            else
                            {
                                if (targetNode.GetWalkPoint().y < transform.position.y)
                                {

                                    UpdateAnimationState(PlayerState.StairsDown);
                                }
                            }
                        }
                        else
                        {
                            if (targetNode.isLadder)
                            {
                                float distance = Vector3.Distance(transform.position, targetNode.GetWalkPoint());
                                Debug.Log(transform.position + "    " + targetNode + "    " + targetNode.GetWalkPoint());
                                Debug.Log(distance);

                                if (distance < 0.6f)
                                {
                                    Debug.Log("distance less than");

                                    if (targetNode.GetWalkPoint().y > transform.position.y)
                                    {
                                        UpdateAnimationState(PlayerState.ClimbBottomReversed);
                                    }
                                    else if (targetNode.GetWalkPoint().y < transform.position.y)
                                    {
                                        UpdateAnimationState(PlayerState.ClimbTopReveresed);
                                    }
                                }
                            }
                            else
                            {
                                UpdateAnimationState(PlayerState.Walking);
                            }

                        }
                    }

                    if (!currentCube.GetComponent<Node>().connectedNodes.Contains(targetNode) && currentCube != targetNode.transform)
                    {
                        Debug.Log("snap back");
                        transform.position = (currentCube.GetComponent<Node>().GetWalkPoint());
                        //transform.position = walkPoint;
                        move = false;
                        targetNode = null;

                    }
                }


            }
            else
            {
                if (currentCube.GetComponent<Node>().isLadder)
                {
                    UpdateAnimationState(PlayerState.ClimbingIdle);
                    Debug.Log("HEEEREERHAIUI");

                }
                else
                {
                    UpdateAnimationState(PlayerState.Idle);

                }
            }
        }
        else
        {
            if (currentCube.GetComponent<Node>().isLadder)
            {
                UpdateAnimationState(PlayerState.ClimbingIdle);

            }
            else
            {
                UpdateAnimationState(PlayerState.Idle);

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
        ClimbBottomReversed = 9

    }

    void UpdateAnimationState(PlayerState newState)
    {
        // Delay idle animation transition
        if (newState == PlayerState.Idle || newState == PlayerState.ClimbingIdle)
        {
            if (currentState == newState)
                return;

            if (pendingIdleState == newState)
            {
                idleDelayTimer += Time.deltaTime;

                if (idleDelayTimer >= idleDelayDuration)
                {
                    animator.SetInteger("State", (int)newState);
                    currentState = newState;
                    pendingIdleState = PlayerState.Idle;
                    idleDelayTimer = 0f;
                }
            }
            else
            {
                pendingIdleState = newState;
                idleDelayTimer = 0f;
            }
        }
        else
        {
            // Reset idle tracking
            idleDelayTimer = 0f;
            pendingIdleState = PlayerState.Idle;

            if (currentState != newState)
            {
                animator.SetInteger("State", (int)newState);
                currentState = newState;
            }
        }
    }

    private Vector3 FindClosestMidWalkPoint(Node node, Vector3 currentPosition)
    {
        Vector3 closestPoint = Vector3.zero;
        float closestDistance = Mathf.Infinity;

        foreach (Vector3 point in node.midWalkPoints)
        {
            float distance = Vector3.Distance(currentPosition, point);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    private float Resolve180Ambiguity(float currentY, float rawTargetY, bool preferClockwise)
    {
        // Shortest signed angle to target
        float diff = Mathf.DeltaAngle(currentY, rawTargetY);

        // Resolve 180-degree ambiguity
        if (Mathf.Abs(diff) >= 179f)
        {
            diff = preferClockwise ? 179f : -179f;
        }

        return currentY + diff;
    }

}