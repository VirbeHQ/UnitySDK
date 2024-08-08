using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;
using Virbe.Core;
using Virbe.Core.Actions;
using Virbe.Core.Exceptions;
using Virbe.UI.Components.BottomBar;
using Virbe.UI.Components.Input;
using Virbe.UI.Components.ProductCard;
using Virbe.UI.Components.QuickReply;
using Virbe.UI.Components.Slide;
using Virbe.UI.Components.Subtitle;
using Behaviour = Virbe.Core.Behaviour;

namespace Virbe.UI.Layouts
{
    public class VirbeLayoutEventConsumer : VirbeBeingEventConsumer
    {
        [SerializeField] protected SubtitleManager userSubtitleManager;

        [FormerlySerializedAs("subtitleManager")] [SerializeField]
        private SubtitleManager beingSubtitleManager;

        [SerializeField] private ProductCardManager productCardManager;
        [SerializeField] private QuickRepliesManager quickRepliesManager;
        [SerializeField] private SlideManager slideManager;
        [SerializeField] private BottomBarManager bottomBarManager;
        [SerializeField] protected InputManager inputManager;
        [SerializeField] protected StatusBarManager statusBarManager;

        private UserAction? _lastUserAction;
        private BeingAction? _lastBeingAction;
        private Behaviour _lastBeingBehaviour;
        [CanBeNull] private Exception _lastBeingException;
        private IProductCardManager _productCardManager;
        private IQuickRepliesManager _quickRepliesManager;

        private void Awake()
        {
            if(productCardManager != null)
            {
                _productCardManager = productCardManager;
            }
            if(quickRepliesManager != null)
            {
                _quickRepliesManager = quickRepliesManager;
            }

            userSubtitleManager?.SetVisible(false);
            beingSubtitleManager?.SetVisible(false);
            inputManager?.SetVisible(false);
            slideManager?.SetVisible(false);
            statusBarManager?.SetVisible(false);
        }

        private void Start()
        {
            _productCardManager?.SetVisible(false);
            _quickRepliesManager?.SetVisible(false);
        }

        public void RefreshUIState()
        {
            if (_lastBeingException?.GetType() == typeof(VirbeException.SpeechRecognitionError))
            {
                userSubtitleManager?.SetMessage("???");
            }
            else
            {
                userSubtitleManager?.SetMessage($"{_lastUserAction?.text}");
            }


            if (_lastBeingException?.GetType() == typeof(VirbeException.SpeechRecognitionError))
            {
                beingSubtitleManager?.SetMessage("Try again");
            }
            else
            {
                switch (_lastBeingBehaviour)
                {
                    default:
                        beingSubtitleManager.SetMessage($"{_lastBeingAction?.text}");
                        break;
                }
            }

            var userStatusBarVisibleStates = new[]
            {
                Behaviour.PlayingBeingAction, Behaviour.RequestProcessing, Behaviour.RequestError,
                Behaviour.InConversation
            };
            userSubtitleManager?.SetVisible(userStatusBarVisibleStates.Contains(_lastBeingBehaviour));

            var beingStatusBarVisibleStates = new[]
            {
                Behaviour.PlayingBeingAction, Behaviour.RequestError, Behaviour.RequestReceived,
                Behaviour.InConversation
            };
            beingSubtitleManager?.SetVisible(beingStatusBarVisibleStates.Contains(_lastBeingBehaviour));

            if(_quickRepliesManager != null)
            {
                _quickRepliesManager?.SetVisible(beingStatusBarVisibleStates.Contains(_lastBeingBehaviour));
                if (_lastBeingAction != null)
                {
                    _quickRepliesManager?.BeingActionPlayed((BeingAction)_lastBeingAction);
                }
                else
                {
                    _quickRepliesManager?.ClearButtons();
                }
            }

            if (_productCardManager != null)
            {
                _productCardManager.SetVisible(beingStatusBarVisibleStates.Contains(_lastBeingBehaviour));
                if (_lastBeingAction != null)
                {
                    _productCardManager.BeingActionPlayed((BeingAction)_lastBeingAction);
                }
                else
                {
                    _productCardManager.ClearCards();
                }
            }

            if (inputManager != null)
            {
                inputManager.SetVisible(beingStatusBarVisibleStates.Contains(_lastBeingBehaviour));

                if (_lastBeingAction != null)
                {
                    inputManager.BeingActionPlayed((BeingAction)_lastBeingAction);
                }
                else
                {
                    inputManager.ClearInput();
                }
            }
        }

        public override void BeingStateChanged(BeingState beingState)
        {
            _lastBeingBehaviour = beingState.Behaviour;

            ScheduleUICleanUpIfNeeded();

            RefreshUIState();

            if (statusBarManager != null)
            {
                statusBarManager.BeingStateChanged(beingState);
            }
        }

        public override void UserActionPlayed(UserAction userAction)
        {
            _lastUserAction = userAction;

            RefreshUIState();
        }

        public override void BeingActionPlayed(BeingAction beingAction)
        {
            _lastBeingAction = beingAction;

            RefreshUIState();
        }

        public override void ConversationErrorHappened(Exception exception)
        {
            _lastBeingException = exception;

            RefreshUIState();
        }

        public void ClearButtonsAndCards()
        {
            _lastBeingAction?.buttons?.Clear();
            _lastBeingAction?.cards?.Clear();
        }

        public void RegisterEventConsumer(IVirbeBeingEvetConsumer eventConsumer)
        {
            switch (eventConsumer)
            {
                case IProductCardManager pcm:
                    _productCardManager = pcm;
                    break;
                case IQuickRepliesManager qrm:
                    _quickRepliesManager = qrm;
                    break;
                default:
                    Debug.LogError($"{eventConsumer.GetType()} is not yet implemented in dynamic append mode");
                    break;
            }
        }
        private void ScheduleUICleanUpIfNeeded()
        {
            var cleanImmediatelyStates = new[]
            {
                Behaviour.Focused, Behaviour.Idle, Behaviour.Listening, Behaviour.RequestProcessing
            };
            if (cleanImmediatelyStates.Contains(_lastBeingBehaviour))
            {
                _lastUserAction = null;
                _lastBeingAction = null;
                _lastBeingException = null;
                beingSubtitleManager?.SetMessage("");
                userSubtitleManager?.SetMessage("");
            }
        }

    }
}