﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
public class InfantryBehaviour : UnitBehaviour {
    InfantryBehaviourState behaviour = InfantryBehaviourState.Nothing;

    TransporterBehaviour transporter;
    int menCount = 8;
    bool gettingIntoFormation = false;
    bool ordersDone = false;
    bool init;
    int unloadIndex;
    float interval=.2f;
    float loadCooldown = .2f;
	// Use this for initialization
    List<Man> men = new List<Man>();
	void Start () {
        initialize();
        
	}
    void initialize()
    {
        if (!init)
        {
            data = UnitData.Infantry();
			base.setHealth(data.maxHealth); //health initialized here instead of UnitBehaviour because there's no "base.Start()" unlike for the other vehicles
            buildMen();
            init = true;
        }
    }
    public  void buildMen()
    {
        
        for (int i = 0; i < menCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(.1f,.2f,.1f);
            //go.transform.parent = transform;
            men.Add(new Man(go,data));
        }
    }
	// Update is called once per frame
	void Update () {
        base.Update();
        switch (behaviour){
            case InfantryBehaviourState.Boarding:
                boarding();
                break;
            case InfantryBehaviourState.WalkToTransport:
                updatePosition();
                destination = transporter.getRallyPoint();
                if (reachedDestination())
                {
                    behaviour = InfantryBehaviourState.Boarding;
                }
                else
                {
                    men.ForEach(x => x.setDestination(destination));
                }   
                break;
            case InfantryBehaviourState.Unloading:
                unloading();
                getIntoFormation();
                break;
            case InfantryBehaviourState.GettingIntoFormation:
                updatePosition();
                getIntoFormation();
                break;
            case InfantryBehaviourState.MoveToWaypoint:
                updatePosition();
                if (reachedDestination())
                {
                    setFormation(Formation.Ring);
                    ordersDone = true;
                }
                break;
            case InfantryBehaviourState.Nothing:
                updatePosition();
                break;
        }
        //WRONG
        
        
	}

    private void updatePosition()
    {
        var pos = men.ConvertAll(x => x.gameObject).getCenterOfMass();
        transform.position = pos;
    }

    private bool reachedDestination()
    {
        var reachedDestination = men.All(x => x.reachedDestination);
        return reachedDestination;
    }

    private void getIntoFormation()
    {
        if (reachedDestination())
        {
            men.ForEach(x => x.fixFormationOffset(transform.position));
            if (gotDestination)
            {
                moveToDestination();
            }
            else
            {
                ordersDone = true;
                behaviour = InfantryBehaviourState.Nothing;
            }
        }
    }
    private void moveToDestination(){
        men.ForEach(x => x.setDestination(destination));
        behaviour = InfantryBehaviourState.MoveToWaypoint;
        gotDestination = false;
    }
    private void unloading()
    {
        loadCooldown -= Time.deltaTime;
        if (loadCooldown < 0)
        {
            loadCooldown = interval;
            men[unloadIndex++].setActive(true);
            if (unloadIndex == menCount)
            {
                unloadIndex = 0;
            }
        }
    }

    private void boarding()
    {
        loadCooldown -= Time.deltaTime;
        if (loadCooldown < 0)
        {
            loadCooldown = interval;

            if(unloadIndex<menCount)men[unloadIndex++].setMatch(transporter.transform.position);
            
            foreach (var man in men.Where((x, i) => i < unloadIndex))
            {
                if (man.reachedDestination)
                {
                    man.setActive(false);
                }
            }
            if (men.All(x => !x.active))
            {
                behaviour = InfantryBehaviourState.Nothing;
            }
        }
    }
    public void setTransportTarget(TransporterBehaviour transport)
    {
        if (transport == null)
        {
            destination = transform.position;
            setRingFormation();
        }
        else
        {
            transporter = transport;
            destination = transporter.getRallyPoint();
            men.ForEach(x => x.fixFormationOffset(transform.position));
            men.ForEach(x => x.setDestination(destination));
            //gotDestination = false;
            behaviour = InfantryBehaviourState.WalkToTransport;
        }
    }
    /*public bool setRally(Vector3 pos,Vector3 vehicle)
    {
        
        if (men.All(x => x.reachedDestination))
        {
            unloadIndex = 0;
            boarding = true;
            destination = vehicle;
            return true;
        }
        else
        {
            men.ForEach(x => x.setDestination(pos));
            return false;
        }
        
    }*/
    protected override void doMovement()
    {
        men.ForEach(x => x.doMovement());
    }
    public override void setUnitFinalHeading(float heading)
    {
        base.setUnitFinalHeading(heading);
        ordersDone = false;
        setFormation(Formation.Line);
    }
    private void setFormation(Formation f)
    {
        initialize();
        if (f == Formation.Ring)
        {
            setRingFormation();
        }
        else
        {
            setLineFormation();
        }
        
    }
    private void setRingFormation()
    {
        var radius = .8f;
        List<Vector3> destinations = new List<Vector3>();
        for (int i = 0; i < menCount; i++)
        {
            var offset=Quaternion.AngleAxis(360 * i / (menCount), Vector3.up) * (radius*Vector3.forward)+destination;
            destinations.Add(offset);
        }
        men.ConvertAll(x => x as Matchable<Vector3>).Match(destinations);
        behaviour = InfantryBehaviourState.GettingIntoFormation;
    }
    private void setLineFormation()
    {
        var seperation = .4f;
        List<Vector3> destinations = new List<Vector3>();

        
        var left = Quaternion.AngleAxis(-Mathf.Rad2Deg*finalHeading, Vector3.up) * (seperation*Vector3.forward);
        for (int i = 0; i < menCount; i++)
        {
            var offset = transform.position+left*(i-(menCount-1)/2f);
            destinations.Add(offset);
        }
        men.ConvertAll(x => x as Matchable<Vector3>).Match(destinations);
        behaviour = InfantryBehaviourState.GettingIntoFormation;
    }
    public void unload(Vector3 pos, Vector3 rally)
    {

        enabled = true;
        initialize();
        transform.position = rally;
        destination=rally;
        foreach(var man in men){
            man.gameObject.transform.position = pos;
            man.gameObject.SetActive(false);
        }
        setRingFormation();
        unloadIndex = 0;
        behaviour = InfantryBehaviourState.Unloading;
    }
    
    protected override Renderer[] getRenderers()
    {
        return men.ConvertAll(x => x.gameObject.GetComponent<Renderer>()).ToArray();
    }
    public override void setOriginalOrientation(Vector3 pos, Quaternion rotation,bool wake=true)
    {
        if (wake) WakeUp();
        initialize();
        var p = pos;
        var y = Ground.terrainData.GetInterpolatedHeight(p.x, p.z);
        pos = new Vector3(p.x, y, p.z);
        transform.position = pos;
        destination = pos;
        setRingFormation();
        men.ForEach(x =>
        {
            x.teleport();
            x.setActive(true);
        });

    }
    public override bool ordersComplete()
    {
        return ordersDone;
    }
    public override void updateMapOrientation()
    {
        
    }
    public bool interactsWithTransport(bool directly)
    {
        if (directly)
        {
            return behaviour == InfantryBehaviourState.Unloading || behaviour == InfantryBehaviourState.Boarding;
        }
        else
        {
            return behaviour == InfantryBehaviourState.Boarding || behaviour == InfantryBehaviourState.Unloading || behaviour == InfantryBehaviourState.WalkToTransport;
        }
    }
    enum InfantryBehaviourState
    {
        WalkToTransport,
        Boarding,
        Unloading,
        GettingIntoFormation,
        Normal,
        MoveToWaypoint,
        Nothing
    }
    enum Formation
    {
        Ring,
        Line
    }
    class Man : Matchable<Vector3>
    {
        public bool reachedDestination;
        Vector3 destination;
        Vector3 formationOffset;
        UnitData data;
        public GameObject gameObject;
        public bool active{
            get;
            private set;
        }
        public Man(GameObject obj, UnitData d)
        {
            gameObject = obj;
            data = d;
        }
        public void setActive(bool a)
        {
            active = a;
            gameObject.SetActive(a);
        }
        public void teleport()
        {
            gameObject.transform.position = destination;
        }
        public void doMovement()
        {
            if (!active) return;
            var diff = destination - gameObject.transform.position;
            var distance = diff.magnitude;
            var step = Time.deltaTime * data.movementSpeed;
            if (step < distance)
            {
                gameObject.transform.position += step * diff / distance;

            }
            else
            {
                gameObject.transform.position = destination;
                reachedDestination = true;
            }
        }
        public void setMatch(Vector3 match)
        {
            destination = match;
            reachedDestination = false;
        }

        public void setDestination(Vector3 d)
        {
            destination = d + formationOffset;
            reachedDestination = false;

        }

        public float getScore(Vector3 matchee)
        {
            return (matchee - gameObject.transform.position).magnitude;
        }
        public void fixFormationOffset(Vector3 center)
        {
            formationOffset = (gameObject.transform.position - center);
        }

    }
}
