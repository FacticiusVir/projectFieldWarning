using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Units.Scripts
{
    abstract class Weapon
    {
        //TODO: Have a List<Unitbehavior> of targets for multi-targeting weapons

        enum WeaponState {AIMING, FIRING }

        public Boolean isReloading = false;
        public Boolean targetIsAcquired = false;
        public Boolean weaponIsCoolingDown = false;
        //public int reloadProgress = 100;
        public float reloadProgress;   // Time between switching magazines
        public float fireRateCooldown = 0.0f; // Limits time between shots (in a magazine)

        public int reloadTime;
        public int rateOfFire;
        public int capacity;
        public int accuracy;
        public int maxRange;
        public int minRange;
        public int turnTime;    //Speed of weapon rotation
        public GameObject projectile;

        public int aimTime;

        public abstract void fireAt(UnitBehaviour target);
        protected abstract void fire();
        public abstract void aim();

    }
}
