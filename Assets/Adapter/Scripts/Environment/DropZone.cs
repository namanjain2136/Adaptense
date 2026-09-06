// ============================================================
// ADAPTER Project — Phase 6
// DropZone.cs
//
// PURPOSE:
//   Marks a target position on the destination table where a
//   specific InteractableObject should be placed.
//   Visual states: pulsing cyan = empty, solid green = occupied.
// ============================================================

using UnityEngine;

namespace Adapter.Environment
{
    public class DropZone : MonoBehaviour
    {
        [Tooltip("The object that belongs in this drop zone.")]
        public InteractableObject assignedObject;

        [Tooltip("Label shown above the zone to tell the user what goes here.")]
        public string zoneName = "Drop Zone";

        public bool IsOccupied { get; private set; } = false;

        private MeshRenderer _renderer;
        private Material _emptyMat;
        private Material _occupiedMat;
        private float _pulseTimer = 0f;

        private void Start()
        {
            _renderer = GetComponent<MeshRenderer>();

            _emptyMat = new Material(Shader.Find("Standard"));
            _emptyMat.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            _emptyMat.SetFloat("_Mode", 3f);
            _emptyMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _emptyMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _emptyMat.SetInt("_ZWrite", 0);
            _emptyMat.DisableKeyword("_ALPHATEST_ON");
            _emptyMat.EnableKeyword("_ALPHABLEND_ON");
            _emptyMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _emptyMat.renderQueue = 3000;

            _occupiedMat = new Material(Shader.Find("Standard"));
            _occupiedMat.color = new Color(0.15f, 0.95f, 0.35f, 0.75f);
            _occupiedMat.SetFloat("_Mode", 3f);
            _occupiedMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _occupiedMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _occupiedMat.SetInt("_ZWrite", 0);
            _occupiedMat.DisableKeyword("_ALPHATEST_ON");
            _occupiedMat.EnableKeyword("_ALPHABLEND_ON");
            _occupiedMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _occupiedMat.renderQueue = 3000;

            if (_renderer != null)
                _renderer.material = _emptyMat;
        }

        private void Update()
        {
            if (IsOccupied || _renderer == null) return;
            _pulseTimer += Time.deltaTime * 2.5f;
            float alpha = Mathf.Lerp(0.15f, 0.5f, (Mathf.Sin(_pulseTimer) + 1f) * 0.5f);
            Color c = _emptyMat.color;
            c.a = alpha;
            _emptyMat.color = c;
            _renderer.material = _emptyMat;
        }

        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            if (_renderer != null)
                _renderer.material = occupied ? _occupiedMat : _emptyMat;
        }
    }
}
