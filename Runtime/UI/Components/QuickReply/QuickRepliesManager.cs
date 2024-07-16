using System;
using System.Collections.Generic;
using UnityEngine;
using Virbe.Core;
using Virbe.Core.Actions;
using Virbe.Core.Custom;
using Virbe.UI.Layouts;

namespace Virbe.UI.Components.QuickReply
{
    public interface IQuickRepliesManager : IQuickReplyListener, IVirbeBeingEvetConsumer
    {
        void SetVisible(bool visible);
        void ClearButtons();
    }

    public class QuickRepliesManager : VirbeUIComponent, IQuickRepliesManager
    {
        [SerializeField] private GameObject quickReplyPrefab;
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private QuickReplyEvent onQuickReplyEvent = new QuickReplyEvent();
        
        private readonly List<QuickReplyLoader> _quickReplyLoaders = new List<QuickReplyLoader>();
        
        private List<Button> _currentButtons;

        public override void BeingStateChanged(BeingState beingState)
        {
            // Ignore at the moment
        }

        public override void BeingActionPlayed(BeingAction beingAction)
        {
            ClearQuickReplies();

            List<Button> extractButtons = beingAction.buttons;
            if (extractButtons != null)
            {
                ResizeQuickReplyLoaders(extractButtons);
                LoadQuickReplies(extractButtons);
                
                SetVisible(_quickReplyLoaders.Count > 0);
            }
        }

        void IQuickRepliesManager.SetVisible(bool visible) => SetVisible(visible);
        void IQuickRepliesManager.ClearButtons() => ClearQuickReplies();

        void IQuickReplyListener.OnQuickReplyClicked(Button button) => onQuickReplyEvent?.Invoke(button);
        public void ResizeQuickReplyLoaders(List<Button> buttons)
        {
            while (_quickReplyLoaders.Count < buttons.Count)
            {
                var newItem = Instantiate(quickReplyPrefab, contentRoot.transform);
                var quickReplyLoader = newItem.GetComponent<QuickReplyLoader>();
                _quickReplyLoaders.Add(quickReplyLoader);
            }
        }

        public void ClearQuickReplies()
        {
            _currentButtons?.Clear();
            
            foreach (var cardLoader in _quickReplyLoaders)
            {
                cardLoader.gameObject.SetActive(false);
            }
        }

        public override void SetVisible(bool visible)
        {
            gameObject.SetActive(visible && _currentButtons?.Count > 0);
        }

        private void LoadQuickReplies(List<Button> buttons)
        {
            _currentButtons = new List<Button>();

            for (var index = 0; index < buttons.Count; index++)
            {
                _currentButtons.Add(buttons[index]);
                _quickReplyLoaders[index].SetQuickReply(buttons[index], this);
                _quickReplyLoaders[index].gameObject.SetActive(true);
            }
        }

    }
}