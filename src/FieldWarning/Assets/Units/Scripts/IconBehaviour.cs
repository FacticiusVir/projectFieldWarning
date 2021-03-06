﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class IconBehaviour : SelectableBehavior
{
    int layer=-1;
    public PlatoonBehaviour unit;
    GameObject camera;

    SymbolBehaviour symbol
    {
        get
        {
            if (_symbol == null)
            {
                _symbol = transform.GetChild(1).GetComponent<SymbolBehaviour>();
            }
            return _symbol;
        }
    }
    SymbolBehaviour _symbol;

    Transform billboard{
        get{
            if(_billboard==null){
                _billboard=transform.GetChild(0);
            }
            return _billboard;
        }
    }
    Transform _billboard;

    float height = 1f;
    float size = 0.1f;
    public bool isInitiated = false;
    private bool init = false;
    Color baseColor = Color.blue;
    bool visible = true;


	// Use this for initialization
	void Start () {
        camera = GameObject.Find("Main Camera");
        //billboard.transform.localPosition = billboard.GetComponent<Renderer>().bounds.extents.y * Vector3.up;
        billboard.GetComponent<Renderer>().material.color = baseColor;
        if (layer!=-1)
            setLayer(layer);
        setSelected(false);
	}
	
	// Update is called once per frame
	void Update () {
        if (!init)
        {
            setVisible(visible);
            init = true;
        }
        transform.localPosition =  height * camera.transform.up;
        transform.rotation=Quaternion.LookRotation(camera.transform.forward);
        var distance=(camera.transform.position-transform.position).magnitude;
        transform.localScale = size*distance*Vector3.one;
        
	}
    public void setUnit(PlatoonBehaviour u)
    {
        unit = u;
        symbol.setIcon(u.type);
    }
    /*public override void setDestination(Vector3 v)
    {
        foreach(var pb in unit.GetComponents<PlatoonBehaviour>())pb.setDestination(v);
    }
    public override void setFinalHeading(Vector3 v)
    {
        unit.GetComponent<PlatoonBehaviour>().setFinalHeading(v);
    }
    public override void getDestinationFromGhost()
    {
        unit.GetComponent<PlatoonBehaviour>().getDestinationFromGhost();
    }
    public override PlatoonBehaviour getPlatoon()
    {
        return unit.GetComponent<PlatoonBehaviour>();
    }*/
    public void setLayer(int l)
    {
        layer = l;
        if(billboard!=null)billboard.gameObject.layer = l;
        gameObject.layer = l;
    }
    public void setSource(List<UnitBehaviour> list)
    {
        isInitiated = true;
        billboard.GetComponentInChildren<CompoundHealthbarBehaviour>().setSource(list);
    }
    public void setVisible(bool vis)
    {
        gameObject.SetActive(vis);
        if (_billboard != null)
        {
            
            billboard.GetComponent<Renderer>().enabled = vis;
            symbol.GetComponent<Renderer>().enabled = vis;
        }
        else
        {
            
            visible = vis;
        }
        if (vis)
        {
            setLayer(LayerMask.NameToLayer("Selectable"));

        }
        else
        {
            setLayer(LayerMask.NameToLayer("Ignore Raycast"));
        }
    }
    public void setSelected(bool selected)
    {
        Color color;
        if (selected)
        {
            color = (baseColor + Color.white) / 2;
            
        }
        else
        {
            color = baseColor;
        }
        billboard.GetComponent<Renderer>().material.color = color;
        symbol.GetComponent<Renderer>().material.color = color;// (color + Color.white) / 2;
    }
    public void setGhost()
    {
        billboard.GetComponent<Renderer>().material.SetColor("_Emission", (2*baseColor + Color.white) / 3);
        symbol.GetComponent<Renderer>().material.SetColor("_Emission", (2 * baseColor + Color.white) / 3);
        setVisible(true);
    }
    public void setTeam(Team t)
    {
        if (t == Team.Blue)
        {
            baseColor = Color.Lerp(Color.blue,Color.white,.1f);
        }
        else
        {
            baseColor = Color.red;
        }
        
    }
}
