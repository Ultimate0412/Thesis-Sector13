using System.Collections.Generic;
using UnityEngine;

namespace Sector13.Decals
{
    /// <summary>
    /// Marks a volume where floor decals should be scattered. Has no runtime cost -
    /// the DecalScatterArea editor bakes projectors into the scene.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DecalScatterArea : MonoBehaviour
    {
        [Tooltip("Decal ids from the DecalLibrary, e.g. WaterStain, OilStain, CardboardScuff")]
        public List<string> decalIds = new List<string> { "WaterStain" };

        [Tooltip("How many decals this area should hold")]
        [Range(0, 200)] public int count = 10;

        [Tooltip("World size range in metres")]
        public Vector2 sizeRange = new Vector2(1.2f, 2.6f);

        [Tooltip("Minimum distance between two decals")]
        public float minSpacing = 1.2f;

        [Tooltip("Keep decals this far from the volume edge")]
        public float edgeMargin = 0.3f;

        public int seed = 1337;

        [Tooltip("Parent for the baked decals. Defaults to a sibling 'Decals' object.")]
        public Transform outputParent;

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
