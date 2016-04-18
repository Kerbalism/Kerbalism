// ====================================================================================================================
// quality-of-life mechanics
// ====================================================================================================================


using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {
  
  
[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class QualityOfLife : MonoBehaviour
{  
  // keep it alive
  QualityOfLife() { DontDestroyOnLoad(this); }
  
  		
	// implement quality-of-life mechanics
	public void FixedUpdate()
	{ 
	  // avoid case when DB isn't ready for whatever reason
	  if (!DB.Ready()) return;
	  
	  // do nothing in the editors and the menus    
    if (!Lib.SceneIsGame()) return;
	  
	  // do nothing if paused
	  if (Lib.IsPaused()) return;
	  
	  // get time elapsed from last update
  	double elapsed_s = TimeWarp.fixedDeltaTime;
  	
	  // for each vessel
	  foreach(Vessel v in FlightGlobals.Vessels)
	  {	
      // skip invalid vessels
  	  if (!Lib.IsVessel(v)) continue;  
	  
  	  // skip dead eva kerbals
  	  if (EVA.IsDead(v)) continue;
  	  
  	  // get crew
  	  List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();
  	  
      // calculate quality-of-life bonus
      double qol = Bonus(v);
      
  	  // for each crew
  	  foreach(ProtoCrewMember c in crew)
	    {   	    
  	    // get kerbal data
  	    kerbal_data kd = DB.KerbalData(c.name);
  	    
  	    // skip resque kerbals
  	    if (kd.resque == 1) continue;
	    
  	    // skip disabled kerbals
  	    if (kd.disabled == 1) continue;
  	    
	      // accumulate stress
	      kd.stressed += Settings.StressedDegradationRate * elapsed_s / (qol * Variance(c));
  	    
  	    // in case of breakdown
  	    if (kd.stressed >= Settings.StressedEventThreshold)
  	    {
  	      // trigger breakdown event
  	      Breakdown(v, c);
  	      
  	      // reset stress halfway between danger and event threshold
  	      kd.stressed = (Settings.StressedDangerThreshold + Settings.StressedEventThreshold) * 0.5;
  	    }
  	    // show warning messages
  	    else if (kd.stressed >= Settings.StressedDangerThreshold && kd.msg_stressed < 2)
  	    {
  	      Message.Post(Severity.danger, KerbalEvent.stress, v, c);
  	      kd.msg_stressed = 2;
  	    }
  	    else if (kd.stressed >= Settings.StressedWarningThreshold && kd.msg_stressed < 1)
  	    {
  	      Message.Post(Severity.warning, KerbalEvent.stress, v, c);
  	      kd.msg_stressed = 1;
  	    }
  	    // note: no recovery from stress 
  	  }
	  }
	}
	
	
	void Breakdown(Vessel v, ProtoCrewMember c)
	{
	  // constants
	  const double food_penality = 0.2;        // proportion of food lost on 'depressed'
	  const double oxygen_penality = 0.2;      // proportion of oxygen lost on 'wrong_valve'
	  
	  // get info	  
	  double food_amount = Lib.GetResourceAmount(v, "Food");
    double oxygen_amount = Lib.GetResourceAmount(v, "Oxygen");
    
    // compile list of events with condition satisfied
    List<KerbalBreakdown> events = new List<KerbalBreakdown>();
    events.Add(KerbalBreakdown.mumbling); //< do nothing, here so there is always something that can happen
    if (Lib.CrewCount(v) > 1) events.Add(KerbalBreakdown.argument); //< do nothing, add some variation to messages
    if (Lib.HasData(v)) events.Add(KerbalBreakdown.fat_finger);
    if (Malfunction.CanMalfunction(v)) events.Add(KerbalBreakdown.rage);
    if (food_amount > double.Epsilon) events.Add(KerbalBreakdown.depressed);
    if (oxygen_amount > double.Epsilon) events.Add(KerbalBreakdown.wrong_valve);
	  
    // choose a breakdown event
    KerbalBreakdown breakdown = events[Lib.RandomInt(events.Count)];
  	      
  	// post message first so this one is shown before malfunction message
  	Message.Post(Severity.breakdown, KerbalEvent.stress, v, c, breakdown);
  	      
  	// trigger the event
  	switch(breakdown)
  	{
  	  case KerbalBreakdown.mumbling: break; // do nothing
  	  case KerbalBreakdown.argument: break; // do nothing
  	  case KerbalBreakdown.fat_finger: Lib.RemoveData(v); break;
  	  case KerbalBreakdown.rage: Malfunction.CauseMalfunction(v); break;
  	  case KerbalBreakdown.depressed: Lib.RequestResource(v, "Food", food_amount * food_penality); break;
  	  case KerbalBreakdown.wrong_valve: Lib.RequestResource(v, "Oxygen", oxygen_amount * oxygen_penality); break;    
  	}
	}
	
	
	// return quality-of-life bonus
  public static double Bonus(Vessel v)
  {
    // deduce crew count and capacity
    int crew_count = Lib.CrewCount(v);
    int crew_capacity = Lib.CrewCapacity(v);
    
    // deduce entertainment bonus, multiplying all entertainment factors
    double entertainment = 1.0;
    if (v.loaded)
    {
      foreach(Entertainment m in v.FindPartModulesImplementing<Entertainment>())
      {
        entertainment *= m.rate;
      }
    }
    else
    {
      foreach(ProtoPartSnapshot part in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in part.modules)
        {
          if (m.moduleName == "Entertainment") entertainment *= Lib.GetProtoValue<double>(m, "rate");
        }
      }
    }
    
    // calculate quality of life bonus
    return Bonus((uint)crew_count, (uint)crew_capacity, entertainment, Lib.Landed(v), Signal.Link(v).linked);
  }
  
  
  // return quality-of-life bonus
  public static double Bonus(uint crew_count, uint crew_capacity, double entertainment, bool landed, bool linked)
  {    
    // deduce living space bonus in [1..n] range
    double living_space = LivingSpace(crew_count, crew_capacity);
    
    // deduce firm ground bonus in [1..1+bonus] range
    double firm_ground = (landed ? Settings.QoL_FirmGroundBonus : 0.0) + 1.0;
    
    // deduce phone home bonus in [1..1+bonus] range
    double phone_home = (linked ? Settings.QoL_PhoneHomeBonus : 0.0) + 1.0;
    
    // deduce not alone bonus in [bonus..bonus*n] range
    double not_alone = (crew_count > 1 ? Settings.QoL_NotAloneBonus : 0.0) + 1.0;
    
    // finally, return quality of life bonus
    return entertainment * living_space * firm_ground * phone_home * not_alone;
  }
  
  
  // return per-kerbal quality-of-life variance
  public static double Variance(ProtoCrewMember c)
  {
    // get a value in [0..1] range associated with a kerbal
    double k = (double)Lib.Hash32(c.name.Replace(" Kerman", "")) / (double)UInt32.MaxValue;
    
    // move in [-1..+1] range
    k = k * 2.0 - 1.0;
    
    // return kerbal-specific variance in range [1-n .. 1+n] 
    return 1.0 + Settings.QoL_KerbalVariance * k;
  }
  
  
  // return living space
  public static double LivingSpace(uint crew_count, uint crew_capacity)
  {
    return crew_count == 0 ? 1.0 : ((double)crew_capacity / (double)crew_count) * Settings.QoL_LivingSpaceBonus;
  }	
  
  
  // traduce living space value to string
  public static string LivingSpaceToString(double living_space)
  {
    if (living_space >= 2.5) return "modest";
    else if (living_space >= 1.5) return "poor";
    else if (living_space > double.Epsilon) return "cramped";
    else return "none";
  }
  
  // traduce entertainment value to string
  public static string EntertainmentToString(double entertainment)
  {
    if (entertainment >= 2.5) return "tolerable";
    else if (entertainment >= 1.5) return "boring";
    else return "none";
  } 
}


} // KERBALISM