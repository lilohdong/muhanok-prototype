#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Muhanok.Presentation
{
    /// 프리미티브 + 단색 머티리얼. 캐릭터 모델링은 프로토타입 범위 밖이다.
    /// 같은 색은 같은 머티리얼을 공유해서 드로우콜과 GC를 아낀다.
    public sealed class PrimitiveFactory
    {
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private readonly Shader shader;

        public PrimitiveFactory()
        {
            shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
        }

        public Material MaterialFor(Color color)
        {
            if (materials.TryGetValue(color, out var m)) return m;
            m = new Material(shader) { color = color, name = "Muhanok " + color };
            materials.Add(color, m);
            return m;
        }

        public GameObject Create(PrimitiveType type, string name, Color color, Transform? parent, Vector3 localPosition, Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // 충돌은 Domain의 ClearanceRule이 판정한다. 물리 콜라이더는 필요 없다.
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);

            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = localScale;
            return go;
        }

        public static GameObject Empty(string name, Transform? parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
