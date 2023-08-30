using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

//TODO: create interface (to see the methods)
public abstract class AInputManager
{
    public CancellationTokenSource WaitForHoldCancellationTokenSource;

    public bool IsInputPressedThisFrame(HandType handType)
    {
        bool isLeftHeld = IsLeftHeld();
        bool isRightHeld = IsRightHeld();

        switch (handType)
        {
            case HandType.Left:
                return isLeftHeld;
            case HandType.Right:
                return isRightHeld;
            case HandType.Any:
                return isLeftHeld || isRightHeld;
            case HandType.None:
                return false;
            default: return false;
        }
    }

    public abstract bool IsLeftHeld();
    public abstract bool IsRightHeld();

    public async UniTask WaitForPress(HandType handType, Action callback = default)
    {
        await WaitForExitIfHolding(handType);
        UniTask.WaitUntil(() => IsInputPressedThisFrame(handType));
        callback?.Invoke();
        DoOnPress();
    }

    protected virtual void DoOnPress()
    {
    }

    private async UniTask WaitForExitIfHolding(HandType handType)
    {
        if (IsInputPressedThisFrame(handType))
        {
            await UniTask.WaitUntil(() => !IsInputPressedThisFrame(handType));
        }
    }

    public async UniTask WaitForHoldAndRelease(HandType handType, float duration, Action callback = default)
    {
        await WaitForHold(handType, duration);
        await UniTask.WaitUntil(() => !IsInputPressedThisFrame(handType));
        callback?.Invoke();
    }

    public async UniTask WaitForHold(HandType handType, float duration,
        Action callback = default)
    {
        using (WaitForHoldCancellationTokenSource = new CancellationTokenSource())
        {
            float holdingTime = 0;

            //If the player is already holding when the method starts, we wait for release and a new hold
            await WaitForExitIfHolding(handType);

            bool cancellationRequested = WaitForHoldCancellationTokenSource.IsCancellationRequested;
            while (holdingTime < duration && !cancellationRequested)
            {
                holdingTime = IsInputPressedThisFrame(handType)
                    ? holdingTime + Time.deltaTime
                    : 0;

                DoWhileHolding(handType, holdingTime, duration);

                await UniTask.Yield();
            }

            if (!cancellationRequested)
            {
                callback?.Invoke();
            }
        }

        DoOnHoldFinished(handType);
    }

    protected virtual void DoWhileHolding(HandType handType, float holdingDuration, float duration)
    {
    }

    protected virtual void DoOnHoldFinished(HandType handType)
    {
    }

    public async UniTask WaitForInputsInARow(int numberOfInputsInARow, float maxTimeBetweenInputs,
        Action successCallback, Action timeOutCallback = default,
        bool alternateHands = false, HandType startingHand = HandType.Right)
    {
        int currentNumberOfInputs = 1;
        float currentTime = 0;
        HandType nextHandType = startingHand;
        while (currentNumberOfInputs < numberOfInputsInARow)
        {
            bool inputReceived = alternateHands
                ? IsInputPressedThisFrame(nextHandType)
                : IsInputPressedThisFrame(HandType.Any);
            if (inputReceived)
            {
                currentNumberOfInputs++;
                currentTime = 0;
                nextHandType = nextHandType == HandType.Left ? HandType.Right : HandType.Left;
            }

            await UniTask.Yield();
            currentTime += Time.deltaTime;

            if (currentTime > maxTimeBetweenInputs)
            {
                timeOutCallback?.Invoke();
                return;
            }
        }

        successCallback();
    }
}