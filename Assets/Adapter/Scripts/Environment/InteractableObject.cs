// ============================================================
// ADAPTER Project — Phase 6
// InteractableObject.cs
//
// PURPOSE:
//   Base class for 3D interactable objects in the Lab scene.
//   Supports a clear 4-state lifecycle:
//     Idle -> Selected -> PickedUp -> Placed
//   with smooth animations, material feedback, ghost preview,
//   and a floating name label above the object.
// ============================================================

using UnityEngine;

namespace Adapter.Environment
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(Collider))]
    public class InteractableObject : MonoBehaviour
    {
        [Tooltip("Display name shown in UI and floating label.")]
        public string objectName = "Object";

        [Tooltip("The drop zone Transform on the destination table where this belongs.")]
        public Transform placementTarget;

        [Header("State")]
        public bool IsSelected  { get; private set; }
        public bool IsPickedUp  { get; private set; }
        public bool IsPlaced    { get; private set; }

        // ---- internal references ----
        private MeshRenderer _meshRenderer;
        private Material     _defaultMat;
        private Material     _selectedMat;   // cyan highlight
        private Material     _pickedUpMat;   // orange/yellow
        private Material     _placedMat;     // green glow

        private Vector3    _originPos;
        private Quaternion _originRot;
        private Vector3    _originScale;

        private GameObject _ghostPreview;   // transparent target silhouette
        private GameObject _nameLabel;      // floating TextMesh label
        private TextMesh   _labelText;

        // ---- animation targets ----
        private Vector3    _animTargetPos;
        private bool       _animatingToTarget = false;

        // ---- constants ----
        private const float LiftHeight      = 0.45f;  // how high above origin when picked up
        private const float AnimSpeed       = 9f;
        private const float HoverHeight     = 0.09f;
        private const float HoverScaleMult  = 1.08f;
        private const float PickedScaleMult = 1.18f;

        // =====================================================
        // Unity lifecycle
        // =====================================================

        private void Start()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _originPos    = transform.position;
            _originRot    = transform.rotation;
            _originScale  = transform.localScale;

            // Capture the material Unity assigned (from LabSceneBuilder)
            if (_meshRenderer != null)
                _defaultMat = _meshRenderer.material;

            BuildMaterials();
            BuildGhostPreview();
            BuildNameLabel();
        }

        private void Update()
        {
            AnimateObject();
            UpdateLabel();
        }

        // =====================================================
        // Public API — called by GestureActionMapper
        // =====================================================

        public void OnHoverEnter()
        {
            if (IsPlaced || IsPickedUp) return;
            IsSelected = true;
            ApplyMaterial(_selectedMat);
            if (_nameLabel != null) _nameLabel.SetActive(true);
        }

        public void OnHoverExit()
        {
            if (IsPlaced || IsPickedUp) return;
            IsSelected = false;
            ApplyMaterial(_defaultMat);
            if (_nameLabel != null) _nameLabel.SetActive(false);
        }

        public void OnPickUp()
        {
            if (IsPlaced) return;
            IsPickedUp = true;
            IsSelected = false;
            ApplyMaterial(_pickedUpMat);
            if (_nameLabel != null) _nameLabel.SetActive(true);

            // Show ghost at placement target
            if (_ghostPreview != null && placementTarget != null)
                _ghostPreview.SetActive(true);

            _animatingToTarget = false; // free-float mode
        }

        public void OnPlace()
        {
            if (!IsPickedUp || placementTarget == null) return;
            IsPickedUp = false;
            IsPlaced   = true;
            ApplyMaterial(_placedMat);

            // Hide ghost
            if (_ghostPreview != null) _ghostPreview.SetActive(false);

            // Animate to placement target
            _animTargetPos   = placementTarget.position;
            _animatingToTarget = true;

            // Notify drop zone
            DropZone zone = placementTarget.GetComponent<DropZone>();
            if (zone != null) zone.SetOccupied(true);

            Debug.Log($"[InteractableObject] {objectName} placed at {placementTarget.name}");
        }

        public void OnReturn()
        {
            // Cancel pick-up — return to origin
            IsPickedUp = false;
            IsSelected = false;
            ApplyMaterial(_defaultMat);
            if (_ghostPreview != null) _ghostPreview.SetActive(false);
            if (_nameLabel   != null) _nameLabel.SetActive(false);
            _animTargetPos    = _originPos;
            _animatingToTarget = true;
            transform.rotation = _originRot;
        }

        public void OnReset()
        {
            // Hard reset — unplace and return to origin
            if (IsPlaced && placementTarget != null)
            {
                DropZone zone = placementTarget.GetComponent<DropZone>();
                if (zone != null) zone.SetOccupied(false);
            }
            IsSelected = IsPickedUp = IsPlaced = false;
            ApplyMaterial(_defaultMat);
            if (_ghostPreview != null) _ghostPreview.SetActive(false);
            if (_nameLabel   != null) _nameLabel.SetActive(false);
            transform.position   = _originPos;
            transform.rotation   = _originRot;
            transform.localScale = _originScale;
            _animatingToTarget   = false;
        }

        // =====================================================
        // Internal helpers
        // =====================================================

        private void AnimateObject()
        {
            if (_animatingToTarget)
            {
                // Smooth move to placement target or origin
                transform.position = Vector3.Lerp(transform.position, _animTargetPos, Time.deltaTime * AnimSpeed);
                if (IsPlaced)
                {
                    transform.localScale = Vector3.Lerp(transform.localScale, _originScale, Time.deltaTime * AnimSpeed);
                    transform.rotation   = Quaternion.Slerp(transform.rotation, _originRot, Time.deltaTime * AnimSpeed);
                }
                if (Vector3.Distance(transform.position, _animTargetPos) < 0.005f)
                {
                    transform.position   = _animTargetPos;
                    _animatingToTarget   = false;
                }
                return;
            }

            if (IsPickedUp)
            {
                // Float up + gentle bob + slow spin
                Vector3 liftTarget = _originPos + Vector3.up * (LiftHeight + Mathf.Sin(Time.time * 2.5f) * 0.035f);
                transform.position   = Vector3.Lerp(transform.position, liftTarget, Time.deltaTime * AnimSpeed);
                transform.Rotate(Vector3.up, 35f * Time.deltaTime, Space.World);
                transform.localScale = Vector3.Lerp(transform.localScale, _originScale * PickedScaleMult, Time.deltaTime * AnimSpeed);
            }
            else if (IsSelected)
            {
                // Gentle hover
                Vector3 hoverTarget = _originPos + Vector3.up * HoverHeight;
                transform.position   = Vector3.Lerp(transform.position, hoverTarget, Time.deltaTime * AnimSpeed);
                transform.localScale = Vector3.Lerp(transform.localScale, _originScale * HoverScaleMult, Time.deltaTime * AnimSpeed);
            }
            else if (!IsPlaced)
            {
                // Return to rest
                transform.position   = Vector3.Lerp(transform.position, _originPos, Time.deltaTime * AnimSpeed);
                transform.localScale = Vector3.Lerp(transform.localScale, _originScale, Time.deltaTime * AnimSpeed);
                transform.rotation   = Quaternion.Slerp(transform.rotation, _originRot, Time.deltaTime * AnimSpeed);
            }
        }

        private void UpdateLabel()
        {
            if (_nameLabel == null) return;
            // Label always floats above the object
            _nameLabel.transform.position = transform.position + Vector3.up * (transform.localScale.y * 0.85f + 0.18f);
            // Face the camera
            if (Camera.main != null)
                _nameLabel.transform.rotation = Quaternion.LookRotation(_nameLabel.transform.position - Camera.main.transform.position);
        }

        private void ApplyMaterial(Material mat)
        {
            if (_meshRenderer != null && mat != null)
                _meshRenderer.material = mat;
        }

        // ---- builders ----

        private void BuildMaterials()
        {
            Color baseColor = _defaultMat != null ? _defaultMat.color : Color.gray;

            _selectedMat = new Material(Shader.Find("Standard"));
            _selectedMat.color = new Color(0.25f, 0.85f, 1f);

            _pickedUpMat = new Material(Shader.Find("Standard"));
            _pickedUpMat.color = new Color(1f, 0.82f, 0.15f);

            _placedMat = new Material(Shader.Find("Standard"));
            _placedMat.color = new Color(0.2f, 0.95f, 0.4f);
        }

        private void BuildGhostPreview()
        {
            if (placementTarget == null) return;

            _ghostPreview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ghostPreview.name = $"{objectName}_Ghost";
            _ghostPreview.transform.position   = placementTarget.position;
            _ghostPreview.transform.localScale  = transform.localScale * 1.05f;

            // Remove collider so it doesn't interfere
            Destroy(_ghostPreview.GetComponent<Collider>());

            Material ghostMat = new Material(Shader.Find("Standard"));
            ghostMat.color = new Color(1f, 0.82f, 0.15f, 0.28f);
            ghostMat.SetFloat("_Mode", 3f);
            ghostMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ghostMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghostMat.SetInt("_ZWrite", 0);
            ghostMat.DisableKeyword("_ALPHATEST_ON");
            ghostMat.EnableKeyword("_ALPHABLEND_ON");
            ghostMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            ghostMat.renderQueue = 3000;
            _ghostPreview.GetComponent<MeshRenderer>().material = ghostMat;

            _ghostPreview.SetActive(false);
        }

        private void BuildNameLabel()
        {
            _nameLabel = new GameObject($"{objectName}_Label");
            _nameLabel.transform.SetParent(null); // world space

            _labelText = _nameLabel.AddComponent<TextMesh>();
            _labelText.text          = objectName;
            _labelText.fontSize      = 32;
            _labelText.characterSize = 0.055f;
            _labelText.anchor        = TextAnchor.MiddleCenter;
            _labelText.alignment     = TextAlignment.Center;
            _labelText.color         = Color.white;

            _nameLabel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_ghostPreview != null) Destroy(_ghostPreview);
            if (_nameLabel    != null) Destroy(_nameLabel);
        }
    }
}
