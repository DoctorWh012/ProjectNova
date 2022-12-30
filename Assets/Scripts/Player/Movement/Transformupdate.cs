using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transformupdate 
{
    public ushort Tick { get; private set; }
    public Vector3 Position { get; private set; }

    public Transformupdate(ushort tick, Vector3 position)
    {
        Tick = tick;
        Position = position;
    }

}
