#nullable enable
using UnityEngine;

namespace Muhanok.Presentation.Views
{
    public sealed class CoinView : MonoBehaviour
    {
        private float spinDegPerSec;
        private Transform? disc;

        public static CoinView Build(PrimitiveFactory factory, PresentationProfile p, Transform? parent)
        {
            var root = PrimitiveFactory.Empty("Coin", parent);
            var view = root.AddComponent<CoinView>();
            view.spinDegPerSec = p.coinSpinDegPerSec;
            var disc = factory.Create(PrimitiveType.Cylinder, "Disc", p.coinColor, root.transform,
                new Vector3(0f, 1.0f, 0f), new Vector3(0.6f, 0.05f, 0.6f));
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            view.disc = disc.transform;
            return view;
        }

        private void Update()
        {
            if (disc == null) return;
            disc.Rotate(0f, spinDegPerSec * Time.deltaTime, 0f, Space.World);
        }
    }
}
