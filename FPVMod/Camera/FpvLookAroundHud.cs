using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.FpvView
{
    /// <summary>Tiny center cross while RMB free-look is held.</summary>
    internal static class FpvLookAroundHud
    {
        private const float ArmPx = 7f;
        private const float ThickPx = 1.8f;

        private static GameObject? _root;
        private static RectTransform? _parent;
        private static bool _visible;

        internal static void BindParent(RectTransform? parent) => _parent = parent;

        internal static void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
            {
                if (_root != null)
                    _root.SetActive(false);
                return;
            }

            EnsureUi();
            if (_root != null)
                _root.SetActive(true);
        }

        internal static void DestroyUi()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            _visible = false;
        }

        private static void EnsureUi()
        {
            if (_parent == null)
                return;

            if (_root != null)
            {
                if (_root.transform.parent != _parent)
                {
                    _root.transform.SetParent(_parent, false);
                    _root.transform.SetAsLastSibling();
                }
                return;
            }

            _root = new GameObject("FPV.LookCenterMark");
            _root.hideFlags = HideFlags.HideAndDontSave;
            _root.transform.SetParent(_parent, false);

            var rt = _root.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(ArmPx * 2f, ArmPx * 2f);

            AddArm(_root.transform, "H", new Vector2(ArmPx, ThickPx));
            AddArm(_root.transform, "V", new Vector2(ThickPx, ArmPx));
            _root.SetActive(_visible);
            _root.transform.SetAsLastSibling();
        }

        private static void AddArm(Transform parent, string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.78f);
            img.raycastTarget = false;
        }
    }
}
