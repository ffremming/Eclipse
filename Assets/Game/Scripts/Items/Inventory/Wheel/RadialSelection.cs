using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// The arithmetic of a radial menu: where a pointer is, and which wedge that is.
    ///
    /// <para>
    /// Angles run clockwise from straight up, so slot 0 sits at twelve o'clock and the rest follow
    /// round the dial in slot order. Each wedge is centred on its slot's angle rather than starting
    /// at it, which puts the first slot dead ahead of the pointer's rest position instead of
    /// leaving it half on each side of the seam.
    /// </para>
    /// <para>
    /// Kept out of the MonoBehaviour that drives the wheel because every bug a radial menu has is in
    /// here — a seam that picks the wrong neighbour, a deadzone that swallows a wedge — and none of
    /// them can be seen without the Editor unless the maths stands on its own.
    /// </para>
    /// </summary>
    public static class RadialSelection
    {
        /// <summary>Returned when the pointer is not over any wedge.</summary>
        public const int NoSelection = -1;

        private const float FullTurn = 360f;

        /// <summary>
        /// The wedge under <paramref name="offset"/>, or <see cref="NoSelection"/> while the pointer
        /// is still inside the deadzone around the hub.
        /// </summary>
        /// <param name="offset">Pointer position relative to the wheel's centre, y up.</param>
        /// <param name="count">How many wedges the wheel has.</param>
        /// <param name="deadzoneRadius">Distance from the centre inside which nothing is selected.</param>
        public static int IndexAt(Vector2 offset, int count, float deadzoneRadius)
        {
            if (count <= 0 || offset.magnitude <= deadzoneRadius) return NoSelection;

            float wedge = FullTurn / count;
            float clockwiseFromUp = Mathf.Repeat(Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg, FullTurn);

            // Half a wedge of shift turns "which wedge does this angle fall in" into a floor, because
            // wedge 0 straddles angle 0. The modulo folds the last wedge's far half back onto it.
            return Mathf.FloorToInt((clockwiseFromUp + wedge * 0.5f) / wedge) % count;
        }

        /// <summary>Where the middle of a wedge is, in degrees clockwise from straight up.</summary>
        public static float CentreAngle(int index, int count)
        {
            return count <= 0 ? 0f : index * (FullTurn / count);
        }

        /// <summary>
        /// Moves the virtual pointer by a look delta and keeps it on the dial.
        ///
        /// <para>
        /// The pointer is virtual because the mouse is still locked to the game window while the
        /// wheel is up — the camera stops reading it, but nothing releases it. A stick's "delta" is
        /// its deflection, so holding it over pushes the pointer out to the rim and it stays there,
        /// which is exactly what selecting with a stick should feel like.
        /// </para>
        /// </summary>
        public static Vector2 MovePointer(Vector2 pointer, Vector2 delta, float sensitivity, float maxRadius)
        {
            return Vector2.ClampMagnitude(pointer + delta * sensitivity, Mathf.Max(0f, maxRadius));
        }
    }
}
