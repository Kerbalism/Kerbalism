// ===================================================================================================================
// a gravity ring habitat that improve quality of life
// ===================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class GravityRing : PartModule
{
  // .cfg
  [KSPField] public string description;
  [KSPField] public double entertainment_rate;
  [KSPField] public double ec_rate;
  [KSPField] public string open_animation;
  [KSPField] public string rotate_animation;

  // ring speed tweakable
  [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Speed"),
   UI_FloatRange(minValue=0.0f, maxValue=1.0f, stepIncrement=0.01f)]
  public float speed = 0.0f;

  // the current state
  [KSPField(isPersistant = true)] public bool opened = false;   // if opened
  [KSPField(isPersistant = true)] public double rate = 1.0;     // the current entertainment provided

  // rotate animation
  Animation rotate;

  // editor/r&d info
  public override string GetInfo()
  {
    return Lib.BuildString
    (
      description, "\n",
      "\n<color=#999999>Comfort (max): <b>", entertainment_rate.ToString("F1"), "</b></color>",
      "\n<color=#999999>EC consumption: <b>", Lib.HumanReadableRate(ec_rate), "</b></color>"
    );
  }

  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Open", active = false)]
  public void Open()
  {
    opened = true;

    Events["Open"].active = false;
    Events["Close"].active = true;

    if (rotate != null) { rotate.Stop(); speed = 0.5f; }

    Animation[] anim = this.part.FindModelAnimators(open_animation);
    if (anim.Length > 0)
    {
      anim[0][open_animation].normalizedTime = 0.0f;
      anim[0][open_animation].speed = Math.Abs(anim[0][open_animation].speed);
      anim[0].Play(open_animation);
    }
  }

  // rmb close door
  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Close", active = false)]
  public void Close()
  {
    opened = false;

    Events["Open"].active = true;
    Events["Close"].active = false;

    if (rotate != null) { rotate.Stop(); speed = 0.0f; }

    Animation[] anim = this.part.FindModelAnimators(open_animation);
    if (anim.Length > 0)
    {
      anim[0][open_animation].normalizedTime = 1.0f;
      anim[0][open_animation].speed = -Math.Abs(anim[0][open_animation].speed);
      anim[0].Play(open_animation);
    }
  }


  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    if (open_animation.Length > 0)
    {
      // enable/disable rmb ui events based on initial door state as per .cfg files
      Events["Open"].active = !opened;
      Events["Close"].active = opened;

      // set open animation to beginning or end
      Animation[] anim = this.part.FindModelAnimators(open_animation);
      if (anim.Length > 0)
      {
        if (opened)
        {
          anim[0][open_animation].normalizedTime = 1.0f;
          anim[0][open_animation].speed = Math.Abs(anim[0][open_animation].speed);
          anim[0].Play(open_animation);
        }
        else
        {
          anim[0][open_animation].normalizedTime = 0.0f;
          anim[0][open_animation].speed = -Math.Abs(anim[0][open_animation].speed);
          anim[0].Play(open_animation);
        }
      }
    }
    if (rotate_animation.Length > 0)
    {
      Animation[] anim = this.part.FindModelAnimators(rotate_animation);
      if (anim.Length > 0)
      {
        rotate = anim[0];
        rotate[rotate_animation].normalizedTime = 1.0f;
        rotate[rotate_animation].speed = 0.0f;
        rotate[rotate_animation].wrapMode = WrapMode.Loop;
      }
      else
      {
        rotate = null;
      }
    }
  }


  // implement gravity ring mechanics
  public void FixedUpdate()
  {
    // reset speed when not open
    if (!opened) speed = 0.0f;

    // hide the tweakable if not open
    this.Fields["speed"].guiActive = opened;
    this.Fields["speed"].guiActiveEditor = opened;

    // manage animation
    if (rotate != null)
    {
      // set rotating animation speed
      rotate[rotate_animation].speed = speed;

      // if its open but no animations are playing, start rotating
      if (opened)
      {
        bool playing = false;
        foreach(var anim in this.part.FindModelAnimators())
        {
          playing |= anim.isPlaying;
        }
        if (!playing) rotate.Play(rotate_animation);
      }
    }

    // do nothing else in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    // get resource cache
    vessel_resources resources = ResourceCache.Get(vessel);

    // get resource handler
    resource_info ec = resources.Info(vessel, "ElectricCharge");

    // consume ec
    ec.Consume(ec_rate * speed * Kerbalism.elapsed_s);

    // reset speed if there isn't enough ec
    // note: comparing against amount in previous simulation step
    if (ec.amount <= double.Epsilon) speed = 0.0f;

    // set entertainment
    rate = 1.0 + (entertainment_rate - 1.0) * speed;
  }


  // implement gravity ring mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, GravityRing ring, vessel_resources resources, double elapsed_s)
  {
    // get protomodule data
    float speed = Lib.Proto.GetFloat(m, "speed");

    // get resource handler
    resource_info ec = resources.Info(vessel, "ElectricCharge");

    // consume ec
    ec.Consume(ring.ec_rate * speed * elapsed_s);

    // reset speed if there isn't enough ec
    // note: comparing against amount in previous simulation step
    if (ec.amount <= double.Epsilon)
    {
      speed = 0.0f;
      Lib.Proto.Set(m, "speed", speed);
    }

    // set entertainment
    double rate = 1.0 + (ring.entertainment_rate - 1.0) * speed;
    Lib.Proto.Set(m, "rate", rate);
  }
}


} // KERBALISM