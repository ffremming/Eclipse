using SpaceGame.Vegetation;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Bake and Clear on the field itself, so tuning a layer and seeing the result is one click
    /// rather than a trip to the menu bar.
    /// </summary>
    [CustomEditor(typeof(VegetationField))]
    public sealed class VegetationFieldEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            VegetationField field = (VegetationField)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake")) VegetationBaker.Bake(field);
                if (GUILayout.Button("Clear")) VegetationBaker.Clear(field);
            }
        }
    }
}
