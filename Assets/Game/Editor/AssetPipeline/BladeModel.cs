using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// A blade model and how a hand holds it: everything about a weapon that belongs to the MESH
    /// rather than to what is done with it.
    /// <para>
    /// Split out when the enemies started carrying the same blades as the player. The hold rotation
    /// in particular is tuned by eye against a hand, and it is a property of how the model was
    /// authored — a khopesh whose edge is on a different axis from the sword's needs a different
    /// number to point the same way. Left inline in each builder, the player's sword and the
    /// enemy's would have been two copies of the same tuned value, and the first re-export that
    /// changed one would leave the other holding its blade backwards with nothing to say why.
    /// </para>
    /// </summary>
    public readonly struct BladeModel
    {
        /// <summary>The imported model.</summary>
        public readonly string Path;

        /// <summary>Longest-axis size once held, in metres.</summary>
        public readonly float Length;

        /// <summary>Which end of the model, as authored, the handle is on.</summary>
        public readonly HandleEnd Handle;

        /// <summary>How far up from the handle end the hand closes, as a fraction of the length.</summary>
        public readonly float GripAlong;

        /// <summary>
        /// Rotation in hand space that puts the blade forward and up from the fist with its edge
        /// leading, as the holder idles. A new model is tuned against that pose, not against these
        /// numbers.
        /// </summary>
        public readonly Vector3 HoldRotation;

        public BladeModel(string path, float length, HandleEnd handle, float gripAlong,
                          Vector3 holdRotation)
        {
            Path = path;
            Length = length;
            Handle = handle;
            GripAlong = gripAlong;
            HoldRotation = holdRotation;
        }
    }

    /// <summary>The blades in the project, as models. What each one is FOR is the builders' business.</summary>
    public static class BladeModels
    {
        public static readonly BladeModel Sword = new BladeModel(
            "Assets/Game/Art/Models/Weapons/Sword/sword.fbx",
            length: 0.95f, HandleEnd.LowEnd, gripAlong: 0.16f,
            holdRotation: new Vector3(19.71f, 168.65f, 5.98f));

        public static readonly BladeModel Khopesh = new BladeModel(
            "Assets/Game/Art/Models/Weapons/Khopesh/khopesh.fbx",
            length: 0.72f, HandleEnd.HighEnd, gripAlong: 0.15f,
            holdRotation: new Vector3(340.29f, 348.65f, 354.02f));

        public static readonly BladeModel Axe = new BladeModel(
            "Assets/Game/Art/Models/Weapons/Axe/axe.obj",
            length: 0.88f, HandleEnd.HighEnd, gripAlong: 0.30f,
            holdRotation: new Vector3(5.63f, 76.63f, 340.19f));
    }
}
