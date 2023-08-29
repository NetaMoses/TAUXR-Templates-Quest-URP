using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class DebugMode : MonoBehaviour
{
    private PinchingInputManager pinchingInputManager;

    private bool _waitingForPinchingInputsInARow;
    [SerializeField] private bool _inDebugMode;
    [SerializeField] private int _numberOfPinchesToEnterDebugMode = 3;

    [Header("Eye tracking debugger")] [SerializeField]
    private bool _debugEyeData = true;

    [SerializeField] private GameObject _textPopUp;
    [SerializeField] private Material _focusedObjectMaterial;
    [SerializeField] private Transform _eyeHitPositionSphere;
    private GameObject _previousFocusedObject;
    private Material _previousFocusedObjectPreviousMaterial;
    private bool _wasFocusedOnObject = false;

    private void Start()
    {
        pinchingInputManager = TXRPlayer.Instance.PinchingInputManager;
    }

    // Update is called once per frame
    private void Update()
    {
        if (pinchingInputManager.IsInputPressedThisFrame(HandType.Any) && !_waitingForPinchingInputsInARow)
        {
            _waitingForPinchingInputsInARow = true;
            HandType nextHand = pinchingInputManager.IsLeftHeld() ? HandType.Right : HandType.Left;
            pinchingInputManager.WaitForInputsInARow(_numberOfPinchesToEnterDebugMode, 1, ToggleDebugModeState,
                () => _waitingForPinchingInputsInARow = false, true, nextHand).Forget();
        }

        if (!_inDebugMode)
        {
            return;
        }

        if (_debugEyeData)
        {
            DebugEyeData();
        }
    }

    private void ToggleDebugModeState()
    {
        _waitingForPinchingInputsInARow = false;
        _inDebugMode = !_inDebugMode;
        Debug.Log("In debug mode = " + _inDebugMode);
    }

    private void DebugEyeData()
    {
        // Debug.Log(TXRPlayer.Instance.EyeTracker.FocusedObject);
        Transform focusedObject = TXRPlayer.Instance.EyeTracker.FocusedObject;
        if (focusedObject != null && (focusedObject.gameObject.tag.Equals("PinchPoint") ||
                                      focusedObject.gameObject.tag.Equals("Toucher")))
        {
            return;
        }

        if (focusedObject != null)
        {
            if (!_wasFocusedOnObject)
            {
                UpdateTextPopUp(focusedObject);
                _previousFocusedObjectPreviousMaterial = focusedObject.GetComponent<MeshRenderer>().material;
                _previousFocusedObject = focusedObject.gameObject;
                focusedObject.GetComponent<MeshRenderer>().material = _focusedObjectMaterial;
                _eyeHitPositionSphere.gameObject.SetActive(true);
                _wasFocusedOnObject = true;
            }

            _eyeHitPositionSphere.position = TXRPlayer.Instance.EyeTracker.EyeGazeHitPosition;
        }
        else if (_wasFocusedOnObject && focusedObject == null)
        {
            _previousFocusedObject.GetComponent<MeshRenderer>().material = _previousFocusedObjectPreviousMaterial;
            _wasFocusedOnObject = false;
            _eyeHitPositionSphere.gameObject.SetActive(false);
            // _textPopUp.SetActive(false);
        }
    }

    private void UpdateTextPopUp(Transform focusedObject)
    {
        _textPopUp.SetActive(true);
        Collider focusedObjectCollider = focusedObject.GetComponent<Collider>();
        _textPopUp.transform.position = new Vector3(
            focusedObjectCollider.ClosestPoint(TXRPlayer.Instance.EyeTracker.EyePosition).x,
            focusedObjectCollider.bounds.max.y + 0.1f,
            focusedObjectCollider.ClosestPoint(TXRPlayer.Instance.EyeTracker.EyePosition).z);
        _textPopUp.transform.LookAt(TXRPlayer.Instance.EyeTracker.EyePosition);
        _textPopUp.transform.eulerAngles = new Vector3(0,
            _textPopUp.transform.eulerAngles.y + 90, 0);
        _textPopUp.GetComponent<TextPopUp>().SetTextAndScale(focusedObject.name);
    }
}