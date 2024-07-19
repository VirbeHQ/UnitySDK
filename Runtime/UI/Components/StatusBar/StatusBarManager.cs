using System;
using UnityEngine;
using Virbe.Core;
using Virbe.Core.Actions;
using Virbe.UI.Components;
using Behaviour = Virbe.Core.Behaviour;

public class StatusBarManager : VirbeUIComponent
{
    [SerializeField] private bool showListeningIndicator = true;
    [SerializeField] private bool showSpeakingIndicator = true;
    [SerializeField] private bool showProcessingIndicator = true;
    [SerializeField] private bool showMuteIndicator = true;
    [SerializeField] private bool showConnectionIndicator = true;
    
    [SerializeField] private GameObject listeningIndicator;
    [SerializeField] private GameObject speakingIndicator;
    [SerializeField] private GameObject processingIndicator;
    [SerializeField] private GameObject muteIndicator;
    [SerializeField] private GameObject connectionIndicator;
    
    private void Start()
    {
        SetVisible(false);
    }

    public override void BeingStateChanged(BeingState beingState)
    {
        SetVisible(false);
        
        switch (beingState.Behaviour)
        {
            case Behaviour.Idle:
                break;
            case Behaviour.Focused:
                break;
            case Behaviour.InConversation:
                break;
            case Behaviour.Listening:
                if (listeningIndicator != null) listeningIndicator.SetActive(showListeningIndicator);
                break;
            case Behaviour.RequestProcessing:
                if (processingIndicator) processingIndicator.SetActive(showProcessingIndicator);
                
                break;
            case Behaviour.RequestError:
                break;
            case Behaviour.RequestReceived:
                break;
            case Behaviour.PlayingBeingAction:
                if (speakingIndicator != null) speakingIndicator.SetActive(showSpeakingIndicator);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void BeingActionPlayed(BeingAction beingAction)
    {
        // Ignore at the moment
    }

    public override void SetVisible(bool visible)
    {
        if (listeningIndicator != null) listeningIndicator.SetActive(false);
        if (speakingIndicator != null) speakingIndicator.SetActive(false);
        if (processingIndicator != null) processingIndicator.SetActive(false);
        if (muteIndicator != null) muteIndicator.SetActive(false);
        if (connectionIndicator != null) connectionIndicator.SetActive(false);
    }
}
