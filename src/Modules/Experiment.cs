using System;
using System.Collections.Generic;
using Experience;
using UnityEngine;


namespace KERBALISM {


// EXPERIMENTAL
public sealed class Experiment : PartModule, ISpecifics
{
  // config
  [KSPField] public string experiment;          // id of associated experiment definition
  [KSPField] public string situations;          // comma-separed list of situations
  [KSPField] public double data_rate;           // sampling rate in Mb/s
  [KSPField] public double ec_rate;             // EC consumption rate per-second
  [KSPField] public bool   transmissible=true;  // true if data can be transmitted
  [KSPField] public string deploy=string.Empty; // deploy animation
  [KSPField] public string crew=string.Empty;   // operator crew

  // persistence
  [KSPField(isPersistant=true)] public bool recording;
  [KSPField(isPersistant=true)] public string issue = string.Empty;

  // animations
  Animator deploy_anim;

  // crew specs
  CrewSpecs operator_cs;

  // experiment title
  string exp_name;


  public override void OnStart(StartState state)
  {
    // create animator
    deploy_anim = new Animator(part, deploy);

    // set initial animation state
    deploy_anim.still(recording ? 1.0 : 0.0);

    // parse crew specs
    operator_cs = new CrewSpecs(crew);

    // get experiment title
    exp_name = ResearchAndDevelopment.GetExperiment(experiment).experimentTitle;
  }


  public void Update()
  {
    // in flight
    if (Lib.IsFlight())
    {
      // get info from cache
      vessel_info vi = Cache.VesselInfo(vessel);

      // do nothing if vessel is invalid
      if (!vi.is_valid) return;

      // update ui
      bool has_operator = operator_cs.check(vessel);
      Events["Toggle"].guiName = Lib.StatusToggle(exp_name, !recording ? "stopped" : issue.Length == 0 ? "recording" : Lib.BuildString("<color=#ffff00>", issue, "</color>"));
    }
    // in the editor
    else if (Lib.IsEditor())
    {
      // update ui
      Events["Toggle"].guiName = Lib.StatusToggle(exp_name, recording ? "recording" : "stopped");
    }
  }

  public void FixedUpdate()
  {
    // do nothing in the editor
    if (Lib.IsEditor()) return;

    // do nothing if vessel is invalid
    if (!Cache.VesselInfo(vessel).is_valid) return;

    // get ec handler
    resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

    // if experiment is active
    if (recording)
    {
      // detect conditions
      // - comparing against amount in previous step
      bool has_ec = ec.amount > double.Epsilon;
      bool has_operator = operator_cs.check(vessel);
      string sit = Science.situation(vessel, situations);

      // deduce issues
      issue = string.Empty;
      if (sit.Length == 0) issue = "invalid situation";
      else if (!has_operator) issue = "no operator";
      else if (!has_ec) issue = "missing <b>EC</b>";

      // if there are no issues
      if (issue.Length == 0)
      {
        // generate subject id
        string subject_id = Science.generate_subject(experiment, vessel.mainBody, sit, Science.biome(vessel, sit), Science.multiplier(vessel, sit));

        // record in drive
        if (transmissible)
        {
          DB.Vessel(vessel).drive.record_file(subject_id, data_rate * Kerbalism.elapsed_s);
        }
        else
        {
          DB.Vessel(vessel).drive.record_sample(subject_id, data_rate * Kerbalism.elapsed_s);
        }

        // consume ec
        ec.Consume(ec_rate * Kerbalism.elapsed_s);
      }
    }
  }


  public static void BackgroundUpdate(Vessel v, ProtoPartModuleSnapshot m, Experiment exp, resource_info ec, double elapsed_s)
  {
    // if experiment is active
    if (Lib.Proto.GetBool(m, "recording"))
    {
      // detect conditions
      // - comparing against amount in previous step
      bool has_ec = ec.amount > double.Epsilon;
      bool has_operator = new CrewSpecs(exp.crew).check(v);
      string sit = Science.situation(v, exp.situations);

      // deduce issues
      string issue = string.Empty;
      if (sit.Length == 0) issue = "invalid situation";
      else if (!has_operator) issue = "no operator";
      else if (!has_ec) issue = "missing <b>EC</b>";
      Lib.Proto.Set(m, "issue", issue);

      // if there are no issues
      if (issue.Length == 0)
      {
        // generate subject id
        string subject_id = Science.generate_subject(exp.experiment, v.mainBody, sit, Science.biome(v, sit), Science.multiplier(v, sit));

        // record in drive
        if (exp.transmissible)
        {
          DB.Vessel(v).drive.record_file(subject_id, exp.data_rate * elapsed_s);
        }
        else
        {
          DB.Vessel(v).drive.record_sample(subject_id, exp.data_rate * elapsed_s);
        }

        // consume ec
        ec.Consume(exp.ec_rate * elapsed_s);
      }
    }
  }


  [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "_", active = true)]
  public void Toggle()
  {
    // toggle recording
    recording = !recording;

    // play deploy animation if exist
    deploy_anim.play(!recording, false);
  }


  // action groups
  [KSPAction("Start/Stop Experiment")] public void Action(KSPActionParam param) { Toggle(); }


  // part tooltip
  public override string GetInfo()
  {
    return Specs().info();
  }


  // specifics support
  public Specifics Specs()
  {
    var specs = new Specifics();
    specs.add("Name", ResearchAndDevelopment.GetExperiment(experiment).experimentTitle);
    specs.add("Data rate", Lib.HumanReadableDataRate(data_rate));
    specs.add("EC required", Lib.HumanReadableRate(ec_rate));
    if (crew.Length > 0) specs.add("Operator", new CrewSpecs(crew).info());
    specs.add(string.Empty);
    specs.add("<color=#00ffff>Situations:</color>", string.Empty);
    var tokens = Lib.Tokenize(situations, ',');
    foreach(string s in tokens) specs.add(Lib.BuildString("• <b>", s, "</b>"));
    return specs;
  }
}


} // KERBALISM

