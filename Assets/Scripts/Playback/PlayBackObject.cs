﻿using System.Collections;
using UnityEngine;

namespace TiltBrush
{
        public class PlayBackObject : MonoBehaviour
        {

            static public PlayBackObject m_Instance;

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
            }

            void Awake()
                {
                    m_Instance = this;
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
            //Destroy(m_StrokeIndicator.m_Indicator.gameObject);
            //Destroy(m_VrController.m_Controller.gameObject);
            //Destroy(m_OculusAvatar.m_Avatar.gameObject);
            Debug.Log("Cleaning indicator");
            m_StrokeIndicator.m_Indicator.ClearPlayback();
            Debug.Log("Cleaning avatar");
            m_OculusAvatar.m_Avatar.ClearPlayback();
            Debug.Log("Cleaning avatar");
            m_VrController.m_Controller.ClearPlayback();
        }
        }
}