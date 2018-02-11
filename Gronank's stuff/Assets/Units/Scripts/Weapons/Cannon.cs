using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Units.Scripts.Weapons
{

    class Cannon : Weapon
    {
        UnitBehaviour owner;

        Cannon(UnitBehaviour owner)
        {
            this.owner = owner; //Owner of this weapon
            aimTime = 1;
            reloadTime = 5;
            capacity = 30;
            rateOfFire = 2;
            accuracy = 50;
            maxRange = 2000;
            minRange = 0;
            projectile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        public override void fireAt(UnitBehaviour target) {
            setTargetAcquired(true); //For testing.
                        
            if (targetIsAcquired)
            {
                fireAtGround(target.transform.position);
            }
            else
                aim();
        }

        public void fireAtGround(Vector3 target) {
            if (fireRateCooldown <= 0  && !isReloading) {
                fire();
                Projectile shot = new Projectile();
                shot.transform.position = owner.transform.position;
                var distance = target.magnitude; // ?? copied from tutorial
                var heading = target - owner.transform.position;
                var direction = heading / distance; //Normalized direction vector
                shot.velocity = 10;
                shot.direction = direction;
            }
        }

        protected override void fire() {
            capacity -= 1;
            fireRateCooldown = rateOfFire;
            if (capacity <= 0)
            {
                isReloading = true;
            }
        }

        public void Update() {
            if (isReloading)
            {
                reloadProgress += Time.deltaTime;
                if (reloadProgress >= reloadTime)
                    isReloading = false;
            }
            if (fireRateCooldown > 0)
                fireRateCooldown -= Time.deltaTime;
        }

        public override void aim() {
            //TODO aiming, get weapon to target things
        }

        //Function mostly intended for debugging.
        public void setTargetAcquired(Boolean boolean) {
            targetIsAcquired = boolean;
        }

    }
}
