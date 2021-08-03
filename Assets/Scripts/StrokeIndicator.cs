using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiltBrush
{
    public class StrokeIndicator : MonoBehaviour
    {
        // [SerializeField] static GameObject m_object;
        // static public StrokeIndicator m_instance;
        //public GameObject BeginIndicatorFromMemory()
        //{

        //    return null;
        //}

        //private PointerScript m_script;

        //public StrokeIndicator(PointerScript script)
        //{
        //    m_script = script;
        //}




        //public static StrokeIndicator Create(Transform parent, GameObject indicatorPrefab) // 
        //{
        //    // GameObject go = Resources.Load<GameObject>("StrokIndicator");
        //    GameObject currenObject = Instantiate(indicatorPrefab);
        //    currenObject.transform.SetParent(parent);

        //    StrokeIndicator strokeIndicator = currenObject.GetComponent<StrokeIndicator>();

        //    return strokeIndicator;
        //}

        //void Start()
        //{
            
        //}

        //void Update()
        //{
            
        //}
        public void ClearPlayback()
        {
            Destroy(gameObject);
        }
    }

}
