#nullable enable
using System.Collections.Generic;
using Muhanok.Application;
using UnityEngine;

namespace Muhanok.Presentation.Views
{
    /// 구간 하나의 뷰. 바닥 + 풀에서 빌린 장애물/코인. 구간이 회수되면 전부 풀로 돌려준다.
    public sealed class SegmentView : MonoBehaviour
    {
        private PresentationProfile profile = null!;
        private Transform floor = null!;
        private Renderer floorRenderer = null!;
        private PrimitiveFactory factory = null!;

        private readonly List<ObstacleView> obstacles = new List<ObstacleView>();
        private readonly List<CoinView?> coins = new List<CoinView?>();

        public int SegmentIndex { get; private set; } = -1;

        public static SegmentView Build(PrimitiveFactory factory, PresentationProfile p, Transform worldRoot)
        {
            var root = PrimitiveFactory.Empty("Segment", worldRoot);
            var view = root.AddComponent<SegmentView>();
            view.profile = p;
            view.factory = factory;
            var floorGo = factory.Create(PrimitiveType.Cube, "Floor", p.floorColorA, root.transform,
                Vector3.zero, Vector3.one);
            view.floor = floorGo.transform;
            view.floorRenderer = floorGo.GetComponent<Renderer>();
            return view;
        }

        public void Show(TrackSegment segment, ViewPools pools, Transform worldRoot)
        {
            SegmentIndex = segment.Index;
            transform.SetParent(worldRoot, false);
            transform.localPosition = new Vector3(0f, 0f, segment.StartDistance);

            var length = segment.Layout.Length;
            floor.localPosition = new Vector3(0f, -profile.floorThickness * 0.5f, length * 0.5f);
            floor.localScale = new Vector3(profile.laneWidth * 3f, profile.floorThickness, length);
            floorRenderer.sharedMaterial = factory.MaterialFor(segment.Index % 2 == 0 ? profile.floorColorA : profile.floorColorB);

            var layout = segment.Layout;
            for (var i = 0; i < layout.Obstacles.Count; i++)
            {
                var o = layout.Obstacles[i];
                var v = pools.GetObstacle(o.Kind);
                v.transform.SetParent(transform, false);
                v.transform.localPosition = new Vector3((int)o.Lane * profile.laneWidth, 0f, o.DistanceFromSegmentStart);
                obstacles.Add(v);
            }
            for (var i = 0; i < layout.Coins.Count; i++)
            {
                var c = layout.Coins[i];
                if (segment.IsCoinTaken(i))
                {
                    coins.Add(null);
                    continue;
                }
                var v = pools.GetCoin();
                v.transform.SetParent(transform, false);
                v.transform.localPosition = new Vector3((int)c.Lane * profile.laneWidth, 0f, c.DistanceFromSegmentStart);
                coins.Add(v);
            }
        }

        public void HideCoin(int coinIndex, ViewPools pools)
        {
            if (coinIndex < 0 || coinIndex >= coins.Count) return;
            var v = coins[coinIndex];
            if (v == null) return;
            coins[coinIndex] = null;
            pools.Release(v);
        }

        public void Clear(ViewPools pools)
        {
            foreach (var o in obstacles) pools.Release(o);
            obstacles.Clear();
            foreach (var c in coins) if (c != null) pools.Release(c);
            coins.Clear();
            SegmentIndex = -1;
        }
    }
}
