// ============================================================
// ADAPTER Project — Environment
// InteractableObject.cs
//
// PURPOSE:
//   Base class for 3D interactable objects in the Lab scene.
//   Provides smooth visual 3D movement (elevation, rotation, scaling)
//   and material feedback when selected or grabbed.
// ============================================================

using UnityEngine;

namespace Adapter.Environment
{
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(Collider))]
    public class InteractableObject : MonoBehaviour
    {
        [Tooltip("Name of the object, e.g. 'Beaker', 'Book', 'Switch'")]
        public string objectName = "Interactable";

        [Header("Feedback Materials")]
        public Material defaultMaterial;
        public Material highlightMaterial;
        public Material grabbedMaterial;

        private MeshRenderer _meshRenderer;
        private Vector3 _initialPosition;
        private Vector3 _initialScale;
        private Quaternion _initialRotation;

        private bool _isHovered = false;
        private bool _isGrabbed = false;

        private void Start()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _initialPosition = transform.position;
            _initialScale = transform.localScale;
            _initialRotation = transform.rotation;

            if (_meshRenderer != null && defaultMaterial == null)
            {
                defaultMaterial = _meshRenderer.material;
            }

            // Create default highlight / grab materials if not assigned
            if (highlightMaterial == null)
            {
                highlightMaterial = new Material(Shader.Find("Standard"));
                highlightMaterial.color = new Color(0.3f, 0.85f, 1f);
            }
            if (grabbedMaterial == null)
            {
                grabbedMaterial = new Material(Shader.Find("Standard"));
                grabbedMaterial.color = new Color(1f, 0.85f, 0.2f);
            }
        }

        private void Update()
        {
            // Smoothly animate 3D movement based on state
            if (_isGrabbed)
            {
                // Float up + gently rotate & bob
                Vector3 targetPos = _initialPosition + new Vector3(0, 0.25f + Mathf.Sin(Time.time * 3f) * 0.04f, 0);
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 8f);
                transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
                transform.localScale = Vector3.Lerp(transform.localScale, _initialScale * 1.15f, Time.deltaTime * 8f);
            }
            else if (_isHovered)
            {
                // Slight elevation on hover
                Vector3 targetPos = _initialPosition + new Vector3(0, 0.08f, 0);
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 8f);
                transform.localScale = Vector3.Lerp(transform.localScale, _initialScale * 1.08f, Time.deltaTime * 8f);
            }
            else
            {
                // Return to rest position & orientation
                transform.position = Vector3.Lerp(transform.position, _initialPosition, Time.deltaTime * 8f);
                transform.localScale = Vector3.Lerp(transform.localScale, _initialScale, Time.deltaTime * 8f);
                transform.rotation = Quaternion.Slerp(transform.rotation, _initialRotation, Time.deltaTime * 8f);
            }
        }

        public void OnHoverEnter()
        {
            _isHovered = true;
            if (_meshRenderer != null && highlightMaterial != null && !_isGrabbed)
                _meshRenderer.material = highlightMaterial;
        }

        public void OnHoverExit()
        {
            _isHovered = false;
            if (_meshRenderer != null && defaultMaterial != null && !_isGrabbed)
                _meshRenderer.material = defaultMaterial;
        }

        public void OnGrab()
        {
            _isGrabbed = true;
            if (_meshRenderer != null && grabbedMaterial != null)
                _meshRenderer.material = grabbedMaterial;
        }

        public void OnRelease()
        {
            _isGrabbed = false;
            if (_isHovered && _meshRenderer != null && highlightMaterial != null)
                _meshRenderer.material = highlightMaterial;
            else if (_meshRenderer != null && defaultMaterial != null)
                _meshRenderer.material = defaultMaterial;
        }
    }
}
