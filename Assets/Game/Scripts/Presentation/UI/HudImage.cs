using UnityEngine;
using UnityEngine.UI;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Makes the plain UI image every code-built HUD element starts as.
    ///
    /// <para>
    /// Never a raycast target: the HUD is looked at, not clicked, and an image that caught the pointer
    /// would swallow a click meant for whatever is under it.
    /// </para>
    /// </summary>
    public static class HudImage
    {
        public static Image Create(Transform parent, string name)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.raycastTarget = false;
            return image;
        }

        public static Image Create(Transform parent, string name, Color color)
        {
            Image image = Create(parent, name);
            image.color = color;
            return image;
        }
    }
}
