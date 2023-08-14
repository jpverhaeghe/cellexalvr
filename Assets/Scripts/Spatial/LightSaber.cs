
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace CellexalVR.Spatial
{

    /// <summary>
    /// This class handles the positioning and animation of slicing light saber.
    /// For collision and slice logic <see cref="LightSaberSliceCollision"/>
    /// </summary>
    public class LightSaber : MonoBehaviour
    {
        public static LightSaber instance;
        public GameObject rayCastSource;
        public LightSaberSliceCollision laser;

        private Vector3 positionInHand = new Vector3(0.01f, -0.02f, -0.02f);
        private Quaternion rotationInHand = Quaternion.Euler(15f, 90f, 50f);
        private bool inHand;
        private new Rigidbody rigidbody;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (inHand) return;
            Ray ray = new Ray(rayCastSource.transform.position, rayCastSource.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, 10)) return;
            if (hit.collider.gameObject != gameObject)
            {
                if (rigidbody.isKinematic)
                {
                    rigidbody.isKinematic = false;
                }

                return;
            }

            LaserHover();
            if (true)
            {
                StartCoroutine(MoveToHand());
            }
        }

        /// <summary>
        /// Move the saber around a bit when hand is facing it.
        /// </summary>
        private void LaserHover()
        {
            if (!rigidbody.isKinematic)
            {
                rigidbody.isKinematic = true;
            }

            float dT = Time.deltaTime;
            Vector3 pos = transform.position;
            pos.y = 0.1f + (1f + math.sin(Time.time)) * 0.1f;
            if (pos.y < 0.1f) return;
            transform.Rotate(20 * dT, 0, 0);
            transform.position = pos;
        }

        /// <summary>
        /// Animate saber to attach it to your hand.
        /// </summary>
        /// <returns></returns>
        private IEnumerator MoveToHand()
        {
            inHand = true;
            laser.GetComponentInChildren<VisualEffect>(true).Play();
            transform.parent = gameObject.transform;
            GetComponent<Rigidbody>().isKinematic = true;
            float dT = Time.deltaTime;
            while (Vector3.Distance(transform.localPosition, positionInHand) > 0.01f)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, positionInHand, dT * 5);
                transform.Rotate(90f * dT, 80 * dT, 150f * dT);
                yield return null;
            }
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(ActivateLaser());
        }

        /// <summary>
        /// Activate the light part of the light saber.
        /// </summary>
        /// <returns></returns>
        private IEnumerator ActivateLaser()
        {
            laser.gameObject.SetActive(true);
            Vector3 targetScale = new Vector3(0.01f, 0.5f, 0.01f);
            Vector3 targetPosition = new Vector3(laser.transform.localPosition.x, 0.58f, laser.transform.localPosition.z);
            float dT = Time.deltaTime;
            while (math.abs(laser.transform.localScale.y - 0.5f) > 0.01f)
            {
                laser.transform.localScale = Vector3.MoveTowards(laser.transform.localScale, targetScale, 3 * dT);
                laser.transform.localPosition = Vector3.MoveTowards(laser.transform.localPosition, targetPosition, 3 * dT);
                yield return null;
            }

            laser.transform.localScale = targetScale;
            laser.transform.localPosition = targetPosition;
        }
    }
}