using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace AeroSim.AircraftModules
{
    public class CountermeasurePool : MonoBehaviour
    {
        private static CountermeasurePool instance;
        public static CountermeasurePool Instance => instance;

        public ObjectPool<FlareCm> flarePool;
        public ObjectPool<GameObject> chaffPool;
        public GameObject flarePrefab;
        public GameObject chaffPrefab;
        public Transform flareParent;

        void Awake()
        {
            instance = this;
        }

        public void Start()
        {
            flarePool = new ObjectPool<FlareCm>(CreateFlare, GetFlare, ReleaseFlare, DestroyFlare, true, 24, 10000);
        }

        public FlareCm CreateFlare()
        {
            GameObject obj = GameObject.Instantiate(flarePrefab, flareParent);
            return obj.GetComponent<FlareCm>();
        }

        public void GetFlare(FlareCm flare)
        {
            flare.Reset();
            flare.gameObject.SetActive(true);
        }

        public void ReleaseFlare(FlareCm flare)
        {
            flare.gameObject.SetActive(false);
        }

        public void DestroyFlare(FlareCm flare)
        {
            GameObject.Destroy(flare.gameObject);
        }
    }
}