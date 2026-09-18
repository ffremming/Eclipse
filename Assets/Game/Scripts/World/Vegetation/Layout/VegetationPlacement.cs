namespace SpaceGame.Vegetation
{
    /// <summary>
    /// One plant a layer wants, after both the layout and the item's own ranges have been drawn.
    /// Metres and degrees, on the XZ plane; the height comes from the ground the field is baked on.
    /// </summary>
    public readonly struct VegetationPlacement
    {
        public VegetationPlacement(float x, float z, int itemIndex,
                                   float yawDegrees, float tiltDegrees, float tiltYawDegrees,
                                   float scale, float sink)
        {
            X = x;
            Z = z;
            ItemIndex = itemIndex;
            YawDegrees = yawDegrees;
            TiltDegrees = tiltDegrees;
            TiltYawDegrees = tiltYawDegrees;
            Scale = scale;
            Sink = sink;
        }

        public float X { get; }
        public float Z { get; }

        /// <summary>Which of the layer's items to plant.</summary>
        public int ItemIndex { get; }

        /// <summary>Spin around the plant's own up axis, so neighbours do not face the same way.</summary>
        public float YawDegrees { get; }

        /// <summary>Lean away from vertical.</summary>
        public float TiltDegrees { get; }

        /// <summary>Compass direction the lean points in.</summary>
        public float TiltYawDegrees { get; }

        /// <summary>Uniform size multiplier on the prefab, which is already at its life size.</summary>
        public float Scale { get; }

        /// <summary>Metres pushed down into the ground, so the plant is planted rather than perched.</summary>
        public float Sink { get; }
    }
}
