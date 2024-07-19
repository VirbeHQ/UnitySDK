using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Virbe.Core;
using Virbe.Core.Actions;
using Virbe.UI.Components.ProductCard;
using Virbe.UI.Layouts;

namespace Virbe.UI.Components.Slide
{
    public class SlideManager : VirbeUIComponent
    {
        [SerializeField] private  SlideLoader slideLoader;

        private VirbePluginUIConnector _virbePluginUIConnector;

        private void Awake()
        {
            _virbePluginUIConnector = GetComponentInParent<VirbePluginUIConnector>();
            Assert.IsNotNull(_virbePluginUIConnector, "VirbePluginUIConnector component is required in the parent Layout component");
        }

        public override void BeingStateChanged(BeingState beingState)
        {
            // Ignore at the moment
        }

        public override void BeingActionPlayed(BeingAction beingAction)
        {
            ClearSlides();
            List<Core.Custom.Slide> extractSlides = beingAction.custom?.ExtractSlides();
            if (extractSlides != null && extractSlides.Count > 0)
            {
                LoadSlide(extractSlides[0]);
            }
        }

        private void LoadSlide(Core.Custom.Slide slide)
        {
            gameObject.SetActive(true);
            slideLoader.SetSlide(slide);
        }

        public void ClearSlides()
        {
            gameObject.SetActive(false);
        }

        public override void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}