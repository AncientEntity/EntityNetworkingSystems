using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EntityNetworkingSystems
{
    public class LinearInterpolation : MonoBehaviour
    {
        public bool currentlyInterpolating = false;
        public Vector3 targetPosition;
        public float stepSize = 3f;

        private float stoppingDistance = 0.2f;

        private void Start()
        {
            stoppingDistance = stepSize / 20f;
        }

        private void Update()
        {
            if (currentlyInterpolating)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, (stepSize+(Mathf.Abs(Vector2.Distance(transform.position,targetPosition))*0.3f)) * Time.deltaTime);
                if(Vector3.Distance(transform.position,targetPosition) <= stoppingDistance)
                {
                    //transform.position = targetPosition;
                    currentlyInterpolating = false;
                }
            }
        }

        public void UpdateIPosition(FieldArgs args)
        {
            Vector3 newPos = args.GetValue<SerializableVector>().ToVec3();
            if(Vector3.Distance(transform.position,newPos) >= 5f)
            {
                transform.position = newPos;
                return;
            }

            targetPosition = newPos;
            currentlyInterpolating = true;
            
        }

    }
}