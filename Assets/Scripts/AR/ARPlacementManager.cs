using System.Collections.Generic;
using ARLearning.Input;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARLearning.AR
{
[RequireComponent(typeof(ARRaycastManager))]
[RequireComponent(typeof(ARPlaneManager))]
[RequireComponent(typeof(ARLearningStateController))]
[RequireComponent(typeof(TouchInputRouter))]
    public sealed class ARPlacementManager : MonoBehaviour
    {
        [SerializeField] GameObject m_ReticlePrefab;
        [SerializeField] GameObject m_LearningCubePrefab;
        [SerializeField] LearningObjectCatalog m_ObjectCatalog;
        static readonly List<ARRaycastHit> s_Hits = new();
        ARRaycastManager _raycasts;
        ARPlaneManager _planes;
        ARLearningStateController _state;
        TouchInputRouter _input;
        GameObject _reticle;
        GameObject _placedObject;
        Pose _pose;
        bool _hasPose;

        public GameObject PlacedObject => _placedObject;
        public event System.Action<GameObject> ObjectPlaced;
        public event System.Action ObjectReset;

        void Awake()
        {
            _raycasts = GetComponent<ARRaycastManager>(); _planes = GetComponent<ARPlaneManager>();
            _state = GetComponent<ARLearningStateController>(); _input = GetComponent<TouchInputRouter>();
            _reticle = Instantiate(m_ReticlePrefab); _reticle.SetActive(false);
            // The editable Learning Cube is the default. The catalog only replaces
            // it after the learner explicitly chooses a reference model.
        }
        void OnEnable()
        {
            _input.Tap += TryPlace;
            if (m_ObjectCatalog != null) m_ObjectCatalog.Changed += ReplacePlacedObject;
        }
        void OnDisable()
        {
            _input.Tap -= TryPlace;
            if (m_ObjectCatalog != null) m_ObjectCatalog.Changed -= ReplacePlacedObject;
        }
        void Update()
        {
            if (_state.Current == ARLearningState.Placed) return;
            var point = _input.PointerPosition == Vector2.zero ? new Vector2(Screen.width * .5f, Screen.height * .5f) : _input.PointerPosition;
            _hasPose = _raycasts.Raycast(point, s_Hits, TrackableType.PlaneWithinPolygon);
            _reticle.SetActive(_hasPose);
            if (!_hasPose) { _state.Set(ARLearningState.Scanning); return; }
            _pose = s_Hits[0].pose;
            _reticle.transform.SetPositionAndRotation(_pose.position, _pose.rotation);
            _state.Set(ARLearningState.PlacementReady);
        }
        void TryPlace(Vector2 _) 
        {
            if (!_hasPose || _placedObject != null || _state.Current == ARLearningState.Placed) return;
            _placedObject = Instantiate(m_LearningCubePrefab, _pose.position, _pose.rotation);
            if (_placedObject.GetComponent<TopFaceCue>() == null) _placedObject.AddComponent<TopFaceCue>();
            ObjectPlaced?.Invoke(_placedObject);
            _planes.enabled = false; _reticle.SetActive(false); _state.Set(ARLearningState.Placed);
        }
        /// <summary>Places the current learning prefab in front of the non-AR camera preview.</summary>
        public void PlacePreviewObject()
        {
            if (_placedObject != null || m_LearningCubePrefab == null) return;
            var camera = Camera.main;
            if (camera == null) return;
            var position = camera.transform.position + camera.transform.forward * 0.75f;
            _placedObject = Instantiate(m_LearningCubePrefab, position, Quaternion.identity);
            if (_placedObject.GetComponent<TopFaceCue>() == null) _placedObject.AddComponent<TopFaceCue>();
            ObjectPlaced?.Invoke(_placedObject);
            _planes.enabled = false;
            _reticle.SetActive(false);
            _state.Set(ARLearningState.Placed);
        }
        void ReplacePlacedObject(GameObject prefab)
        {
            if (prefab == null) return;
            m_LearningCubePrefab = prefab;
            if (_placedObject == null) return;
            var position = _placedObject.transform.position;
            var rotation = _placedObject.transform.rotation;
            var scale = _placedObject.transform.localScale;
            Destroy(_placedObject);
            _placedObject = Instantiate(m_LearningCubePrefab, position, rotation);
            if (_placedObject.GetComponent<TopFaceCue>() == null) _placedObject.AddComponent<TopFaceCue>();
            _placedObject.transform.localScale = scale;
            ObjectReset?.Invoke();
            ObjectPlaced?.Invoke(_placedObject);
        }
        public void ResetPlacement()
        {
            if (_placedObject != null) Destroy(_placedObject);
            _placedObject = null; _planes.enabled = true; _hasPose = false; _reticle.SetActive(false);
            ObjectReset?.Invoke();
            _state.Set(ARLearningState.Scanning);
        }
        // Reserved for a future whole-object AR reposition mode; mesh tools remain independent.
        public void BeginObjectRepositioning() { if (_placedObject != null) ResetPlacement(); }
    }
}
