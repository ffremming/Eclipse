// Flashes a body red when it is hurt, so a hit reads on the thing that took it.
//
// The tint is a per-material property block on every renderer below this object, blended from the
// material's own base colour toward the flash colour. Nothing is instanced or swapped, and once the
// flash has faded the block is cleared, so the renderer goes back to being exactly its shared
// material rather than a copy that merely looks like it.
using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Gameplay
{
    [DisallowMultipleComponent]
    public class DamageFlash : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private HealthComponent health;

        [Tooltip("What the body is pulled toward at the moment of a hit. Brighter than the material " +
                 "it tints, so the flash reads as the body lighting up rather than only darkening.")]
        [SerializeField] private Color flashColor = new Color(1f, 0.15f, 0.15f);

        [Tooltip("Seconds for the flash to fade out. Long enough to register in the middle of a " +
                 "fight, short enough that a run of hits reads as a run of hits.")]
        [SerializeField] private float duration = 0.3f;

        private struct Slot
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color BaseColor;
        }

        private Slot[] slots;
        private MaterialPropertyBlock block;
        private FlashTimer timer;

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();

            block = new MaterialPropertyBlock();
            timer = new FlashTimer(duration);
            slots = FindTintableSlots();
        }

        private void OnEnable()
        {
            if (health != null) health.OnDamage += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDamage -= OnDamaged;
            timer = new FlashTimer(duration);
            Apply(0f);
        }

        private void Update()
        {
            if (!timer.Active) return;

            timer.Advance(Time.deltaTime);
            Apply(timer.Strength);
        }

        private void OnDamaged(int amount)
        {
            timer.Trigger();
            Apply(timer.Strength);
        }

        private Slot[] FindTintableSlots()
        {
            var found = new List<Slot>();

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].HasProperty(BaseColor)) continue;

                    found.Add(new Slot
                    {
                        Renderer = renderer,
                        MaterialIndex = i,
                        BaseColor = materials[i].GetColor(BaseColor)
                    });
                }
            }

            return found.ToArray();
        }

        private void Apply(float strength)
        {
            foreach (Slot slot in slots)
            {
                // Gone by the time a flash ends or this is disabled — a corpse removed, a scene torn down.
                if (slot.Renderer == null) continue;

                if (strength <= 0f)
                {
                    slot.Renderer.SetPropertyBlock(null, slot.MaterialIndex);
                    continue;
                }

                block.SetColor(BaseColor, Color.Lerp(slot.BaseColor, flashColor, strength));
                slot.Renderer.SetPropertyBlock(block, slot.MaterialIndex);
            }
        }
    }
}
