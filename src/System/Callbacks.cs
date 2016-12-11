using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using KSP.UI.Screens.SpaceCenter.MissionSummaryDialog;


namespace KERBALISM {


public sealed class Callbacks
{
  public Callbacks()
  {
    // set callbacks
    GameEvents.onCrewOnEva.Add(this.toEVA);
    GameEvents.onCrewBoardVessel.Add(this.fromEVA);
    GameEvents.onVesselRecoveryProcessing.Add(this.vesselRecoveryProcessing);
    GameEvents.onVesselRecovered.Add(this.vesselRecovered);
    GameEvents.onVesselTerminated.Add(this.vesselTerminated);
    GameEvents.onVesselWillDestroy.Add(this.vesselDestroyed);
    GameEvents.onVesselWasModified.Add(this.vesselModified);
    GameEvents.onPartCouple.Add(this.vesselDock);
    GameEvents.onPartDie.Add(this.partDestroyed);
    GameEvents.OnTechnologyResearched.Add(this.techResearched);
    GameEvents.onGUIEditorToolbarReady.Add(this.addEditorCategory);
  }


  void toEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // get total crew in the origin vessel
    double tot_crew = (double)Lib.CrewCount(data.from.vessel) + 1.0;

    // get vessel resources handler
    vessel_resources resources = ResourceCache.Get(data.from.vessel);

    // setup supply resources capacity in the eva kerbal
    Profile.SetupEva(data.to);

    // for each resource in the kerbal
    for(int i=0; i < data.to.Resources.Count; ++i)
    {
      // get the resource
      PartResource res = data.to.Resources[i];

      // determine quantity to take
      double quantity = Math.Min(resources.Info(data.from.vessel, res.resourceName).amount / tot_crew, res.maxAmount);

      // remove resource from vessel
      quantity = data.from.RequestResource(res.resourceName, quantity);

      // add resource to eva kerbal
      data.to.RequestResource(res.resourceName, -quantity);
    }

    // show warning if there isn't monoprop in the eva suit
    string prop_name = Lib.EvaPropellantName();
    if (Lib.Amount(data.to, prop_name) <= double.Epsilon && !Lib.Landed(data.from.vessel))
    {
      Message.Post(Severity.danger, Lib.BuildString("There isn't any <b>", prop_name, "</b> in the EVA suit", "Don't let the ladder go!"));
    }

    // turn off headlamp light, to avoid stock bug that show them for a split second when going on eva
    KerbalEVA kerbal = data.to.FindModuleImplementing<KerbalEVA>();
    EVA.HeadLamps(kerbal, false);

    // if vessel info is open, switch to the eva kerbal
    if (Info.IsOpen()) Info.Open(data.to.vessel);

    // execute script
    DB.Vessel(data.from.vessel).computer.execute(data.from.vessel, ScriptType.eva_out);
  }


  void fromEVA(GameEvents.FromToAction<Part, Part> data)
  {
    // for each resource in the eva kerbal
    for(int i=0; i < data.from.Resources.Count; ++i)
    {
      // get the resource
      PartResource res = data.from.Resources[i];

      // add leftovers to the vessel
      data.to.RequestResource(res.resourceName, -res.amount);
    }

    // merge drives data
    Drive.transfer(data.from.vessel, data.to.vessel);

    // forget vessel data
    DB.vessels.Remove(Lib.RootID(data.from.vessel));

    // if vessel info is open, switch to the vessel
    if (Info.IsOpen()) Info.Open(data.to.vessel);

    // execute script
    DB.Vessel(data.to.vessel).computer.execute(data.to.vessel, ScriptType.eva_in);
  }


  void vesselRecoveryProcessing(ProtoVessel v, MissionRecoveryDialog dialog, float score)
  {
    // note:
    // this function accumulate science stored in drives on recovery,
    // and visualize the data in the recovery dialog window

    // zero fucks given if science system is disabled, or in sandbox mode
    if (!Features.Science || HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX) return;

    // get the drive data from DB
    uint root_id = v.protoPartSnapshots[v.rootIndex].flightID;
    if (!DB.vessels.ContainsKey(root_id)) return;
    Drive drive = DB.vessels[root_id].drive;

    // for each file in the drive
    foreach(var p in drive.files)
    {
      // shortcuts
      string filename = p.Key;
      File file = p.Value;

      // de-buffer partially transmitted data
      file.size += file.buff;
      file.buff = 0.0;

      // get subject
      ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(filename);

      // credit science
      double credits = Science.credit(filename, file.size, false, v);

      // create science widged
      ScienceSubjectWidget widged = ScienceSubjectWidget.Create
      (
        subject,            // subject
        (float)file.size,   // data gathered
        (float)credits,     // science points
        dialog              // recovery dialog
      );

      // add widget to dialog
      dialog.AddDataWidget(widged);

      // add science credits to total
      dialog.scienceEarned += (float)credits;
    }

    // for each sample in the drive
    // for each file in the drive
    foreach(var p in drive.samples)
    {
      // shortcuts
      string filename = p.Key;
      Sample sample = p.Value;

      // get subject
      ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(filename);

      // credit science
      double credits = Science.credit(filename, sample.size, false, v);

      // create science widged
      ScienceSubjectWidget widged = ScienceSubjectWidget.Create
      (
        subject,            // subject
        (float)sample.size, // data gathered
        (float)credits,     // science points
        dialog              // recovery dialog
      );

      // add widget to dialog
      dialog.AddDataWidget(widged);

      // add science credits to total
      dialog.scienceEarned += (float)credits;
    }
  }


  void vesselRecovered(ProtoVessel pv, bool b)
  {
    // note: this is called multiple times when a vessel is recovered

    // for each crew member
    foreach(ProtoCrewMember c in pv.GetVesselCrew())
    {
      // avoid creating kerbal data in db again,
      // as this function may be called multiple times
      if (!DB.kerbals.ContainsKey(c.name)) continue;

      // set roster status of eva dead kerbals
      if (DB.Kerbal(c.name).eva_dead)
      {
        c.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
      }

      // forget kerbal data of recovered kerbals
      DB.kerbals.Remove(c.name);
    }

    // for each part
    foreach(ProtoPartSnapshot p in pv.protoPartSnapshots)
    {
      // forget all potential vessel data
      DB.vessels.Remove(p.flightID);
    }

    // purge the caches
    Cache.purge(pv);
    ResourceCache.purge(pv);
  }


  void vesselTerminated(ProtoVessel pv)
  {
    // forget all kerbals data
    foreach(ProtoCrewMember c in pv.GetVesselCrew()) DB.kerbals.Remove(c.name);

    // for each part
    foreach(ProtoPartSnapshot p in pv.protoPartSnapshots)
    {
      // forget all potential vessel data
      DB.vessels.Remove(p.flightID);
    }

    // purge the caches
    Cache.purge(pv);
    ResourceCache.purge(pv);
  }


  void vesselDestroyed(Vessel v)
  {
    // for each part
    foreach(Part p in v.parts)
    {
      // forget all potential vessel data
      DB.vessels.Remove(p.flightID);
    }

    // rescan the damn kerbals
    // - vessel crew is empty at destruction time
    // - we can't even use the flightglobal roster, because sometimes it isn't updated yet at this point
    HashSet<string> kerbals_alive = new HashSet<string>();
    HashSet<string> kerbals_dead = new HashSet<string>();
    foreach(Vessel ov in FlightGlobals.Vessels)
    {
      foreach(ProtoCrewMember c in Lib.CrewList(ov)) kerbals_alive.Add(c.name);
    }
    foreach(var p in DB.kerbals)
    {
      if (!kerbals_alive.Contains(p.Key)) kerbals_dead.Add(p.Key);
    }
    foreach(string n in kerbals_dead) DB.kerbals.Remove(n);

    // purge the caches
    Cache.purge(v);
    ResourceCache.purge(v);
  }


  void vesselDock(GameEvents.FromToAction<Part, Part> e)
  {
    // note:
    //  we do not forget vessel data here, it just became inactive
    //  and ready to be implicitly activated again on undocking
    //  we do however tweak the data of the vessel being docked a bit,
    //  to avoid states getting out of sync, leading to unintuitive behaviours
    VesselData vd = DB.Vessel(e.from.vessel);
    vd.msg_belt = false;
    vd.msg_signal = false;
    vd.storm_age = 0.0;
    vd.storm_time = 0.0;
    vd.storm_state = 0;
    vd.supplies.Clear();
    vd.scansat_id.Clear();

    // merge drives data
    Drive.transfer(e.from.vessel, e.to.vessel);
  }


  void vesselModified(Vessel v)
  {
    // do nothing in the editor
    if (Lib.IsEditor()) return;

    // first, we scan all known vessels and get the set of drives indexed by preferred location
    Dictionary<uint, uint> drives = new Dictionary<uint, uint>();
    foreach(var p in DB.vessels)
    {
      uint root_id = p.Key;
      VesselData vd = p.Value;

      if (vd.drive.location != 0)
      {
        drives.Add(vd.drive.location, root_id);
      }
    }

    // then we scan all parts of the vessel and see if one of them is the preferred location of a drive
    foreach(Part p in v.parts)
    {
      // if that's the case
      if (drives.ContainsKey(p.flightID))
      {
        // use the associated drive for this vessel
        // - this does the right thing even if the two vessels are the same
        uint root_id = drives[p.flightID];
        Lib.Swap(ref DB.Vessel(v).drive, ref DB.vessels[root_id].drive);
        return;
      }
    }
  }


  void partDestroyed(Part p)
  {
    // forget all potential vessel data
    DB.vessels.Remove(p.flightID);
  }


  void addEditorCategory()
  {
    if (PartLoader.LoadedPartsList.Find(k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0) != null)
    {
      var icon = new RUI.Icons.Selectable.Icon("Kerbalism", Lib.GetTexture("category_normal"), Lib.GetTexture("category_selected"));
      PartCategorizer.Category category = PartCategorizer.Instance.filters.Find(k => k.button.categoryName == "Filter by Function");
      PartCategorizer.AddCustomSubcategoryFilter(category, "", icon, k => k.tags.IndexOf("_kerbalism", StringComparison.Ordinal) >= 0);
    }
  }


  void techResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> data)
  {
    if (data.target != RDTech.OperationResult.Successful) return;

    // collect unique configure-related unlocks
    HashSet<string> labels = new HashSet<string>();
    foreach(AvailablePart p in PartLoader.LoadedPartsList)
    {
      foreach(Configure cfg in p.partPrefab.FindModulesImplementing<Configure>())
      {
        foreach(ConfigureSetup setup in cfg.Setups())
        {
          if (setup.tech == data.host.techID)
          {

            labels.Add(Lib.BuildString(setup.name, " in ", cfg.title));
          }
        }
      }

      // add unique configure-related unlocks
      foreach(string label in labels)
      {
        Message.Post
        (
          "<color=#00ffff><b>PROGRESS</b></color>\nOur scientists just made a breakthrough",
          Lib.BuildString("We now have access to <b>", label, "</b>")
        );
      }
    }
  }
}


} // KERBALISM