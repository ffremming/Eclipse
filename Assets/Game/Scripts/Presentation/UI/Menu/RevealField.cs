using UnityEngine;

namespace SpaceGame.Presentation.Menu
{
    /// <summary>
    /// How lit a piece of the menu is, given how far it sits from the light.
    /// <para>
    /// <paramref name="floorAlpha"/> is the whole point of the type. The title may vanish into the
    /// dark because it is decoration, but the one action on the screen may not: a player who has to
    /// sweep a torch around to find the only button has been failed by the interface, however good
    /// it looks. The floor is what guarantees the difference is a setting rather than an accident.
    /// </para>
    /// </summary>
    public static class RevealField
    {
        /// <summary>
        /// Returns 1 within <paramref name="innerRadius"/> of the light, <paramref name="floorAlpha"/>
        /// beyond <paramref name="outerRadius"/>, and a smooth falloff between. Never returns less
        /// than <paramref name="floorAlpha"/>. Positions are in screen pixels.
        /// </summary>
        public static float Alpha(Vector2 cursor, Vector2 element, float innerRadius, float outerRadius,
            float floorAlpha)
        {
            floorAlpha = Mathf.Clamp01(floorAlpha);
            float distance = Vector2.Distance(cursor, element);
            float reveal;

            if (outerRadius <= innerRadius)
            {
                // Degenerate but legal: a light with no falloff at all. Dividing by the gap here
                // would be a NaN, and a NaN alpha renders as an invisible button.
                reveal = distance <= innerRadius ? 1f : 0f;
            }
            else
            {
                float t = Mathf.Clamp01((distance - innerRadius) / (outerRadius - innerRadius));
                reveal = 1f - t * t * (3f - 2f * t);
            }

            return Mathf.Lerp(floorAlpha, 1f, reveal);
        }
    }
}
