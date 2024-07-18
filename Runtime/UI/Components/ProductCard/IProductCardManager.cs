using Virbe.Core;

namespace Virbe.UI.Components.ProductCard
{
    public interface IProductCardManager : IProductCardListener, IVirbeBeingEvetConsumer
    {
        void ClearCards();
        void SetVisible(bool visible);
    }
}