using System.Collections.Generic;
using UnityEngine;
using Virbe.Core;
using Virbe.Core.Actions;
using Virbe.UI.Layouts;

namespace Virbe.UI.Components.ProductCard
{
    public class ProductCardManager : VirbeUIComponent, IProductCardManager
    {
        [SerializeField] private GameObject productCardPrefab;
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private ProductLearnMoreEvent onProductCardClicked = new ProductLearnMoreEvent();
        
        private readonly List<ProductCardLoader> _productCardLoaders = new List<ProductCardLoader>();

        public override void BeingStateChanged(BeingState beingState)
        {
            // Ignore at the moment
        }

        public override void BeingActionPlayed(BeingAction beingAction)
        {
            ClearCards();
            if (beingAction.cards != null)
            {
                ResizeProductCardLoaders(beingAction.cards);
                LoadProductsData(beingAction.cards);
            }
        }

        public override void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        void IProductCardManager.ClearCards() => ClearCards();

        void IProductCardManager.SetVisible(bool visible) => SetVisible(visible);

        void IProductCardListener.OnLearnMoreClicked(Core.Custom.Card card) => onProductCardClicked.Invoke(card);

        private void ClearCards()
        {
            foreach (var cardLoader in _productCardLoaders)
            {
                cardLoader.gameObject.SetActive(false);
            }
        }

        private void ResizeProductCardLoaders(List<Core.Custom.Card> cards)
        {
            while (_productCardLoaders.Count < cards.Count)
            {
                var newItem = Instantiate(productCardPrefab, contentRoot.transform);
                _productCardLoaders.Add(newItem.GetComponent<ProductCardLoader>());
            }
        }

        private void LoadProductsData(List<Core.Custom.Card> beingActionCards)
        {
            for (var index = 0; index < beingActionCards.Count; index++)
            {
                var productCard = beingActionCards[index];
                _productCardLoaders[index].SetProductCard(productCard, this);
                _productCardLoaders[index].gameObject.SetActive(true);
            }
        }
    }
}