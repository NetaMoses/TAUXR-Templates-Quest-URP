using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum HandType { Left, Right, None, Any }
public enum FingerType { Thumb, Index, Middle, Ring, Pinky }

public class TAUXRPlayer : TAUXRSingleton<TAUXRPlayer>
{
    public bool bCastFromMiddle;

    [SerializeField] private Transform ovrRig;
    [SerializeField] private Transform playerHead;
    [SerializeField] private Transform rightHandAnchor;
    [SerializeField] private Transform leftHandAnchor;

    public bool IsEyeTrackingEnabled;
    public bool IsFaceTrackingEnabled;

    [Header("Eye Tracking")]
    [SerializeField] private Transform rightEye;
    [SerializeField] private Transform leftEye;
    private float EYERAYMAXLENGTH = 100000;
    [SerializeField] private float EYETRACKINGCONFIDENCETHRESHOLD = .5f;
    private Vector3 NOTTRACKINGVECTORVALUE = new Vector3(-1f, -1f, -1f);
    private OVREyeGaze ovrEyeR;
    private Transform focusedObject;
    private Vector3 eyeGazeHitPosition;
    int eyeTrackingIgnoreLayer = 7;
    LayerMask eyeTrackingLayerMask = ~(1 << 7);

    [Header("Face Tracking")]
    [SerializeField] private OVRFaceExpressions ovrFace;



    public TAUXRHand HandLeft;
    public TAUXRHand HandRight;


    private OVRHand ovrHandR, ovrHandL;
    private OVRSkeleton skeletonR, skeletonL;
    private OVRManager ovrManager;
    private Pincher pinchPoincL, pinchPointR;
    private List<HandCollider> handCollidersL, handCollidersR;


    // Color overlay
    [SerializeField] private MeshRenderer colorOverlayMR;

    // input handling
    private bool isLeftTriggerHolded = false;
    private bool isRightTriggerHolded = false;

    public Transform PlayerHead => playerHead;
    public Transform RightHand => rightHandAnchor;
    public Transform LeftHand => leftHandAnchor;

    public Transform RightEye => rightEye;
    public Transform LeftEye => leftEye;
    public Transform FocusedObject => focusedObject;
    public Vector3 EyeGazeHitPosition => eyeGazeHitPosition;

    public OVRFaceExpressions OVRFace => ovrFace;

    protected override void DoInAwake()
    {
        ovrManager = GetComponentInChildren<OVRManager>();

        InitHands();
        InitEyeTracking();
        if (IsFaceTrackingEnabled)
        {
            ovrRig.AddComponent<OVRFaceExpressions>();
        }
    }

    private void InitHands()
    {
        HandRight.Init();
        HandLeft.Init();
        /*  ovrHandL = leftHandAnchor.GetComponentInChildren<OVRHand>();
          ovrHandR = rightHandAnchor.GetComponentInChildren<OVRHand>();

          skeletonL = ovrHandL.GetComponent<OVRSkeleton>();
          skeletonR = ovrHandR.GetComponent<OVRSkeleton>();
          InitHandColliders();
        */
    }

    private void InitEyeTracking()
    {
        if (rightEye.TryGetComponent(out OVREyeGaze er))
        {
            ovrEyeR = er;
        }

        focusedObject = null;
        eyeGazeHitPosition = NOTTRACKINGVECTORVALUE;
    }

    // TODO: apply to all fingers
    public Transform GetHandFingerCollider(HandType handType)
    {
        switch (handType)
        {
            case HandType.Left:
                return handCollidersL[0].transform;
            case HandType.Right:
                return handCollidersR[0].transform;
            case HandType.Any:
                return handCollidersL[0].transform;
            case HandType.None:
                return null;
            default: return null;
        }
    }

    public void SetPassthrough(bool state)
    {
        ovrManager.isInsightPassthroughEnabled = state;
    }

    private void InitPinchPoints()
    {
        foreach (Pincher pp in GetComponentsInChildren<Pincher>())
        {
            if (pp.HandT == HandType.Right)
            {
                pinchPointR = pp;
                pinchPointR.Init(skeletonR);
            }
            else
            {
                pinchPoincL = pp;
                pinchPoincL.Init(skeletonL);
            }
        }
    }

    private void InitHandColliders()
    {
        handCollidersL = new List<HandCollider>();
        handCollidersR = new List<HandCollider>();

        foreach (HandCollider hc in GetComponentsInChildren<HandCollider>())
        {
            if (hc.HandT == HandType.Right)
            {
                hc.Init(skeletonR);
                handCollidersR.Add(hc);
            }
            else
            {
                hc.Init(skeletonL);
                handCollidersL.Add(hc);
            }
        }
    }

    void Start()
    {

    }

    void Update()
    {
        HandRight.UpdateHand();
        HandLeft.UpdateHand();

        if (IsEyeTrackingEnabled)
        {
            CalculateEyeParameters();
        }

        isLeftTriggerHolded = OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > .7f;
        isRightTriggerHolded = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) > .7f;
    }

    private void CalculateEyeParameters()
    {
        if (ovrEyeR == null) return;

        if (ovrEyeR.Confidence < EYETRACKINGCONFIDENCETHRESHOLD)
        {
            Debug.LogWarning("EyeTracking confidence value is low. Eyes are not tracked");
            focusedObject = null;
            eyeGazeHitPosition = NOTTRACKINGVECTORVALUE;

            return;
        }

        // cast from middle eye
        Vector3 eyePosition = bCastFromMiddle ? ((rightEye.position + leftEye.position) / 2) : rightEye.position;
        Vector3 eyeForward = rightEye.forward;

        RaycastHit hit;
        if (Physics.Raycast(eyePosition, eyeForward, out hit, EYERAYMAXLENGTH, eyeTrackingLayerMask))
        {
            focusedObject = hit.transform;
            eyeGazeHitPosition = hit.point;
        }
        else
        {
            focusedObject = null;
            eyeGazeHitPosition = NOTTRACKINGVECTORVALUE;
            Debug.Log("Eyeray hit nothig");
        }

        Debug.DrawRay(eyePosition, eyeForward);
    }


    // covers player's view with color. 
    async public UniTask FadeToColor(Color targetColor, float duration)
    {
        if (duration == 0)
        {
            colorOverlayMR.material.color = targetColor;
            return;
        }

        Color currentColor = colorOverlayMR.material.color;
        if (currentColor == targetColor) return;

        float lerpTime = 0;
        while (lerpTime < duration)
        {
            lerpTime += Time.deltaTime;
            float t = lerpTime / duration;
            colorOverlayMR.material.color = Color.Lerp(currentColor, targetColor, t);

            await UniTask.Yield();
        }
    }

    public void RecenterView()
    {
        // IMPLEMENT
    }
    public void CalibrateFloorHeight()
    {
        // IMPLEMENT
    }
    public bool IsTriggerPressedThisFrame(HandType handType)
    {
        switch (handType)
        {
            case HandType.Left:
                return isLeftTriggerHolded;
            case HandType.Right:
                return isRightTriggerHolded;
            case HandType.Any:
                return isLeftTriggerHolded || isRightTriggerHolded;
            case HandType.None:
                return false;
            default: return false;
        }
    }

    // task progresses only when trigger is hold.
    async public UniTask WaitForTriggerHold(HandType handType, float duration)
    {
        float holdingDuration = 0;
        while (holdingDuration < duration)
        {
            if (IsTriggerPressedThisFrame(handType))
            {
                holdingDuration += Time.deltaTime;
            }
            else
            {
                holdingDuration = 0;
            }

            await UniTask.Yield();
        }

    }

    public bool IsPlayerPinchingThisFrame(HandType handType)
    {
        switch (handType)
        {
            case HandType.Left:
                return ovrHandL.GetFingerIsPinching(OVRHand.HandFinger.Index);
            case HandType.Right:
                return ovrHandR.GetFingerIsPinching(OVRHand.HandFinger.Index);
            case HandType.Any:
                return ovrHandL.GetFingerIsPinching(OVRHand.HandFinger.Index) || ovrHandR.GetFingerIsPinching(OVRHand.HandFinger.Index); ;
            case HandType.None:
                return false;
            default: return false;
        }
    }

    async public UniTask WaitForPinchHold(HandType handType, float duration)
    {
        float holdingDuration = 0;
        while (holdingDuration < duration)
        {
            if (IsPlayerPinchingThisFrame(handType))
            {
                holdingDuration += Time.deltaTime;
            }
            else
            {
                holdingDuration = 0;
            }

            await UniTask.Yield();
        }

    }


    public void RepositionPlayer(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
    }
}