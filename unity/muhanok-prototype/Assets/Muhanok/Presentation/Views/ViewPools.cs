#nullable enable
using System.Collections.Generic;
using Muhanok.Domain;
using UnityEngine;
using UnityEngine.Pool;

namespace Muhanok.Presentation.Views
{
    /// 구간·장애물·코인 뷰의 풀. UnityEngine.Pool.ObjectPool<T> 그대로 쓴다. 직접 구현하지 않는다.
    public sealed class ViewPools
    {
        private readonly Transform poolRoot;
        private readonly Dictionary<ObstacleKind, ObjectPool<ObstacleView>> obstacles =
            new Dictionary<ObstacleKind, ObjectPool<ObstacleView>>();
        private readonly ObjectPool<CoinView> coins;
        private readonly ObjectPool<SegmentView> segments;

        public ViewPools(PrimitiveFactory factory, PresentationProfile profile, Transform worldRoot)
        {
            poolRoot = PrimitiveFactory.Empty("Pool", null).transform;
            poolRoot.gameObject.SetActive(false);

            foreach (ObstacleKind kind in System.Enum.GetValues(typeof(ObstacleKind)))
            {
                var k = kind;
                obstacles[k] = new ObjectPool<ObstacleView>(
                    createFunc: () => ObstacleView.Build(k, factory, profile, poolRoot),
                    actionOnGet: v => v.gameObject.SetActive(true),
                    actionOnRelease: v => { v.gameObject.SetActive(false); v.transform.SetParent(poolRoot, false); },
                    actionOnDestroy: v => Object.Destroy(v.gameObject),
                    collectionCheck: false, defaultCapacity: 8, maxSize: 64);
            }

            coins = new ObjectPool<CoinView>(
                createFunc: () => CoinView.Build(factory, profile, poolRoot),
                actionOnGet: v => v.gameObject.SetActive(true),
                actionOnRelease: v => { v.gameObject.SetActive(false); v.transform.SetParent(poolRoot, false); },
                actionOnDestroy: v => Object.Destroy(v.gameObject),
                collectionCheck: false, defaultCapacity: 32, maxSize: 256);

            segments = new ObjectPool<SegmentView>(
                createFunc: () => SegmentView.Build(factory, profile, worldRoot),
                actionOnGet: v => v.gameObject.SetActive(true),
                actionOnRelease: v => { v.gameObject.SetActive(false); v.transform.SetParent(poolRoot, false); },
                actionOnDestroy: v => Object.Destroy(v.gameObject),
                collectionCheck: false, defaultCapacity: 4, maxSize: 16);
        }

        public ObstacleView GetObstacle(ObstacleKind kind) => obstacles[kind].Get();
        public void Release(ObstacleView v) => obstacles[v.Kind].Release(v);
        public CoinView GetCoin() => coins.Get();
        public void Release(CoinView v) => coins.Release(v);
        public SegmentView GetSegment() => segments.Get();
        public void Release(SegmentView v) => segments.Release(v);
    }
}
