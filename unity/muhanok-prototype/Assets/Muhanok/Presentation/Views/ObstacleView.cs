#nullable enable
using Muhanok.Domain;
using UnityEngine;

namespace Muhanok.Presentation.Views
{
    /// 장애물 하나의 모양. 프리미티브 조합. 풀에서 재사용된다.
    public sealed class ObstacleView : MonoBehaviour
    {
        public ObstacleKind Kind { get; private set; }

        public static ObstacleView Build(ObstacleKind kind, PrimitiveFactory factory, PresentationProfile p, Transform? parent)
        {
            var root = PrimitiveFactory.Empty("Obstacle " + kind, parent);
            var view = root.AddComponent<ObstacleView>();
            view.Kind = kind;
            var w = p.laneWidth * 0.8f;

            switch (kind)
            {
                case ObstacleKind.Stairs:
                    // 낮은 계단 3단. 무릎을 들어(또는 점프) 넘는다.
                    for (var i = 0; i < 3; i++)
                    {
                        var h = 0.25f * (i + 1);
                        factory.Create(PrimitiveType.Cube, "Step" + i, p.stairsColor, root.transform,
                            new Vector3(0f, h * 0.5f, i * 0.4f), new Vector3(w, h, 0.4f));
                    }
                    break;
                case ObstacleKind.Barricade:
                    factory.Create(PrimitiveType.Cube, "Bar", p.barricadeColor, root.transform,
                        new Vector3(0f, 0.5f, 0f), new Vector3(w, 1.0f, 0.3f));
                    factory.Create(PrimitiveType.Cube, "LegL", p.barricadeColor, root.transform,
                        new Vector3(-w * 0.4f, 0.5f, 0f), new Vector3(0.15f, 1.0f, 0.6f));
                    factory.Create(PrimitiveType.Cube, "LegR", p.barricadeColor, root.transform,
                        new Vector3(w * 0.4f, 0.5f, 0f), new Vector3(0.15f, 1.0f, 0.6f));
                    break;
                case ObstacleKind.Cage:
                    // 머리 높이의 철창. 아래로 엎드려 지나간다.
                    var top = p.cageBarHeight;
                    factory.Create(PrimitiveType.Cube, "Beam", p.cageColor, root.transform,
                        new Vector3(0f, top + 0.15f, 0f), new Vector3(w, 0.3f, 0.3f));
                    for (var i = 0; i < 4; i++)
                    {
                        var x = -w * 0.5f + w * i / 3f;
                        factory.Create(PrimitiveType.Cylinder, "Bar" + i, p.cageColor, root.transform,
                            new Vector3(x, top + 0.15f + 0.6f, 0f), new Vector3(0.08f, 0.6f, 0.08f));
                    }
                    factory.Create(PrimitiveType.Cube, "PostL", p.cageColor, root.transform,
                        new Vector3(-w * 0.5f, top * 0.5f, 0f), new Vector3(0.12f, top, 0.12f));
                    factory.Create(PrimitiveType.Cube, "PostR", p.cageColor, root.transform,
                        new Vector3(w * 0.5f, top * 0.5f, 0f), new Vector3(0.12f, top, 0.12f));
                    break;
                case ObstacleKind.PoliceCar:
                    // 레인을 꽉 채운다. 옆 레인으로만 피할 수 있다.
                    factory.Create(PrimitiveType.Cube, "Body", p.policeCarColor, root.transform,
                        new Vector3(0f, 0.6f, 0f), new Vector3(p.laneWidth * 0.9f, 1.2f, 3.0f));
                    factory.Create(PrimitiveType.Cube, "Cabin", p.policeCarColor, root.transform,
                        new Vector3(0f, 1.5f, -0.3f), new Vector3(p.laneWidth * 0.8f, 0.6f, 1.5f));
                    factory.Create(PrimitiveType.Cube, "LightR", Color.red, root.transform,
                        new Vector3(-0.3f, 1.9f, -0.3f), new Vector3(0.4f, 0.2f, 0.3f));
                    factory.Create(PrimitiveType.Cube, "LightB", Color.blue, root.transform,
                        new Vector3(0.3f, 1.9f, -0.3f), new Vector3(0.4f, 0.2f, 0.3f));
                    break;
            }
            return view;
        }
    }
}
