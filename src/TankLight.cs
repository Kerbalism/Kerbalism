// ===================================================================================================================
// play an animation when a resource in a tank get below a threshold, and play it in reverse when it get back above it
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class TankLight : PartModule
{
  [KSPField] public string animation_name;     // name of animation to play
  [KSPField] public string resource_name;      // name of resource to check
  [KSPField] public double threshold = 0.15;   // amount of resource considered low, proportional to capacity

  public bool green_status;

  public override void OnStart(StartState state)
  {
    Animation[] anim = this.part.FindModelAnimators(animation_name);
    if (anim.Length > 0)
    {
      double capacity = Lib.Capacity(part, resource_name);
      double level = capacity > 0.0 ? Lib.Amount(part, resource_name) / capacity : 0.0;
      if (level <= threshold)
      {
        anim[0][animation_name].normalizedTime = 1.0f;
        anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = false;
      }
      else
      {
        anim[0][animation_name].normalizedTime = 0.0f;
        anim[0][animation_name].speed = -Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = true;
      }
    }
  }

  public void Update()
  {
    Animation[] anim = this.part.FindModelAnimators(animation_name);
    if (anim.Length > 0)
    {
      double capacity = Lib.Capacity(part, resource_name);
      double level = capacity > 0.0 ? Lib.Amount(part, resource_name) / capacity : 0.0;
      if (level <= threshold && green_status)
      {
        anim[0][animation_name].normalizedTime = 0.0f;
        anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = false;
      }
      if (level > threshold && !green_status)
      {
        anim[0][animation_name].normalizedTime = 1.0f;
        anim[0][animation_name].speed = -Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = true;
      }
    }
  }
}


} // KERBALISM
