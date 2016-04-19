// ===================================================================================================================
// TankLight module
// play an animation when LS in a tank get low, and play it in reverse when LS in the tank get nominal
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public class TankLight : PartModule
{
  [KSPField] public string animation_name;    // name of animation to play
  [KSPField] public string resource_name;     // name of resource to check
  [KSPField] public double threshold = 0.15;   // amount of resource considered low, proportional to capacity

  public bool green_status;

  public override void OnStart(StartState state)
  {
    Animation[] anim = this.part.FindModelAnimators(animation_name);
    if (anim.Length > 0)
    {
      double ls = Lib.GetResourceAmount(this.part, resource_name);
      double capacity = Lib.GetResourceCapacity(this.part, resource_name);
      double red_capacity = capacity * threshold;
      if (ls <= red_capacity)
      {
        anim[0][animation_name].normalizedTime = 1.0f;
        anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = false;
      }
      if (ls > red_capacity)
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
      double ls = Lib.GetResourceAmount(this.part, resource_name);
      double capacity = Lib.GetResourceCapacity(this.part, resource_name);
      double red_capacity = capacity * threshold;
      if (ls <= red_capacity && green_status)
      {
        anim[0][animation_name].normalizedTime = 0.0f;
        anim[0][animation_name].speed = Math.Abs(anim[0][animation_name].speed);
        anim[0].Play(animation_name);
        green_status = false;
      }
      if (ls > red_capacity && !green_status)
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
