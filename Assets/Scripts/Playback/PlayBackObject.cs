using System.Collections;
using UnityEngine;

namespace TiltBrush
{
    public class PlayBackObject : MonoBehaviour
    {

        static public PlayBackObject m_Instance;

        private Vector3 m_Velocity;
        private Quaternion m_Rotation;

        private bool isActive;
        public bool IsActive { get { return isActive; } }

        private Indicator m_StrokeIndicator;
        protected class Indicator
        {
            public StrokeIndicator m_Indicator;
        }

        private Controller m_VrController;
        protected class Controller
        {
            public VrControllers m_Controller;            
        }

        private Avatar m_OculusAvatar;
        protected class Avatar
        {
            public OculusAvatar m_Avatar;
        }
        //public OvrAvatar myAvatar;

        [SerializeField] private GameObject m_VrControllerPrefab;
        [SerializeField] private GameObject m_OculusAvatarPrefab;
        [SerializeField] private GameObject m_IndicatorPrefab;

        public StrokeIndicator GetIndicator()
        {
            return m_StrokeIndicator.m_Indicator;
        }

        public VrControllers GetController()
        {
            return m_VrController.m_Controller;
        }

        public OculusAvatar GetAvatar()
        {
            return m_OculusAvatar.m_Avatar;
        }
            
        public void Initialized()
        {
            // create new object for OculusAvatar
            var avatar = new Avatar();
            GameObject a_obj = (GameObject)Instantiate(m_OculusAvatarPrefab);
            a_obj.transform.parent = transform;
            avatar.m_Avatar = a_obj.GetComponent<OculusAvatar>();
            m_OculusAvatar = avatar;

            // create new object for VrController
            var controller = new Controller();
            GameObject c_obj = (GameObject)Instantiate(m_VrControllerPrefab);
            c_obj.transform.parent = transform;
            controller.m_Controller = c_obj.GetComponent<VrControllers>();
            m_VrController = controller;

            // create new object for StrokIndicator
            var indicator = new Indicator();
            GameObject i_obj = (GameObject)Instantiate(m_IndicatorPrefab);
            i_obj.transform.parent = transform;
            indicator.m_Indicator = i_obj.GetComponent<StrokeIndicator>();
            m_StrokeIndicator = indicator;

            isActive = true;
        }

        void Awake()
            {
                m_Instance = this;
                isActive = false; 
            }

            
        // Use this for initialization
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void ClearPlayBack()
        {
            isActive = false;
            //Destroy(m_StrokeIndicator.m_Indicator.gameObject);
            //Destroy(m_VrController.m_Controller.gameObject);
            //Destroy(m_OculusAvatar.m_Avatar.gameObject);
            Debug.Log("Cleaning indicator");
            m_StrokeIndicator.m_Indicator.ClearPlayback();
            Debug.Log("Cleaning avatar");
            m_OculusAvatar.m_Avatar.ClearPlayback();
            Debug.Log("Cleaning Controller");
            m_VrController.m_Controller.ClearPlayback();
        }

        public void SetParent(CanvasScript canvas)
        {
            if (isActive) {
                Debug.Log("Setting Parent for PlayBack Object");
                m_OculusAvatar.m_Avatar.gameObject.transform.SetParent(canvas.transform, false);
                m_VrController.m_Controller.gameObject.transform.SetParent(canvas.transform, false);
                m_StrokeIndicator.m_Indicator.gameObject.transform.SetParent(canvas.transform, false);
                Debug.Log("Finish Setting Parent for PlayBack Object");
            }
        }

        public void SyncHeadPosition()
        {
            Debug.Log("Sync Head position");
            if (isActive)
            {
                var canvas = App.Scene.ActiveCanvas;
                TrTransform xf_HeadCS = canvas.AsCanvas[ViewpointScript.Head];
                TrTransform xf_HeadAvat = canvas.AsCanvas[m_Instance.GetAvatar().transform];
                // m_Velocity = xf_HeadAvat.translation - xf_HeadCS.translation;
                m_Velocity = m_Instance.GetAvatar().transform.position - ViewpointScript.Head.position;
                // relative rotation for camera when 
                // m_Rotation = Quaternion.Inverse(ViewpointScript.Head.rotation) * m_Instance.GetAvatar().transform.rotation;
                // newScene.rotation = camera.rotation * (old scene rotation relateive to instructor)
                // m_Rotation = m_Instance.GetAvatar().transform.rotation;
                ApplyVelocity(m_Velocity, m_Rotation);
            }
        }

        void ApplyVelocity(Vector3 velocity, Quaternion rotation)
        {
            Debug.Log("ApplyVelocity");
            TrTransform newScene = App.Scene.Pose;
            newScene.translation -= velocity;
            // newScene.rotation = ViewpointScript.Head.rotation * Quaternion.Inverse(rotation);
            // newScene might have gotten just a little bit invalid.
            // Enforce the invariant that fly always sends you
            // to a scene which is MakeValidPose(scene)
            newScene = SketchControlsScript.MakeValidScenePose(newScene, BoundsRadius);
            App.Scene.Pose = newScene;
        }

        float BoundsRadius
        {
            get
            {
                return SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            }
        }
    }
}
