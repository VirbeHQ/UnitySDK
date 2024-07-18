using UI.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Virbe.UI.Components.ProductCard
{
    public interface IProductCardListener
    {
        void OnLearnMoreClicked(Core.Custom.Card card);
    }
    public class ProductCardLoader : MonoBehaviour
    {
        [SerializeField] private RawImage BackgroundImage;
        [SerializeField] private Text Title;
        [SerializeField] private Button LearnMore;
        private Core.Custom.Card _card;

        public void SetProductCard(Core.Custom.Card card, IProductCardListener onProductCardListener)
        {
            _card = card;
            Title.text = card.Title;
            LearnMore.onClick.RemoveAllListeners();
            LearnMore.onClick.AddListener(() => { onProductCardListener.OnLearnMoreClicked(_card); });
            StartCoroutine(ImageLoader.GetRemoteTexture(_card.ImageUrl, (tex) => {
                if (tex != null)
                {
                    BackgroundImage.texture = tex;
                }
            }));
        }

    }
}