using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Units.Scripts.Weapons
{
    class Projectile : MonoBehaviour
    {
        public int velocity;
        public Vector3 direction;
        public float timeToLive = 4.0f;
        public float timeToLiveCounter = 0.0f;

        public GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        public void setProjectileGraphic(GameObject graphic) {
            projectileObject = graphic;
        }

        public void launch(Vector3 direction) {
            
        }

        public void Update() {
            if (timeToLiveCounter > timeToLive)
                Destroy(this);

            projectileObject.transform.position += direction * velocity;
            timeToLiveCounter += Time.deltaTime;
        }

    }
}
