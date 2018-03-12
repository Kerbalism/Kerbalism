using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public enum AntennaType
{
  low_gain,
  high_gain
}


public sealed class Antenna : PartModule, ISpecifics, IAnimatedModule, IScienceDataTransmitter, IContractObjectiveModule
{
  // config
  [KSPField] public AntennaType type;      // type of antenna
  [KSPField] public double cost;           // cost of transmission in EC/s
  [KSPField] public double rate;           // transmission rate at zero distance in Mb/s
  [KSPField] public double dist;           // max transmission distance in meters

  // persistence
  [KSPField(isPersistant=true)] public bool extended;   // true if the low-gain antenna can receive data from other vessels
  [KSPField(isPersistant=true)] public bool relay;      // true if the low-gain antenna can receive data from other vessels

  // utility to transmit data over time when science system is disabled
  DataStream stream;
  ScreenMessage progress_msg;


  public override void OnStart(StartState state)
  {
    // don't break tutorial scenarios
    if (Lib.DisableScenario(this)) return;

    // assume extended if there is no animator
    extended |= part.FindModuleImplementing<ModuleAnimationGroup>() == null;

    // create data stream, used if science system is disabled
    stream = new DataStream();

    // in flight
    if (Lib.IsFlight())
    {
      // get animator module, if any
      var anim = part.FindModuleImplementing<ModuleAnimationGroup>();
      if (anim != null)
      {
        // resync extended state from animator
        // - rationale: extending in editor doesn't set extended to true,
        //   leading to spurious signal loss for 1 tick on prelaunch
        extended = anim.isDeployed;

        // allow extending/retracting even when vessel is not controllable
        anim.Events["DeployModule"].guiActiveUncommand = true;
        anim.Events["RetractModule"].guiActiveUncommand = true;
      }
    }
  }


  public void Update()
  {
    // in flight
    if (Lib.IsFlight())
    {
      // update ui
      Events["ToggleRelay"].active = type == AntennaType.low_gain && (extended || !Settings.ExtendedAntenna) && !vessel.isEVA;
      Events["ToggleRelay"].guiName = Lib.StatusToggle("Relay", relay ? "yes" : "no");

      // show transmission messages
      if (stream.transmitting())
      {
        string text = Lib.BuildString("Transmitting ", stream.current_file(), ": ", Lib.HumanReadablePerc(stream.current_progress()));
        if (progress_msg != null) ScreenMessages.RemoveMessage(progress_msg);
        progress_msg = ScreenMessages.PostScreenMessage(text, 1.0f, ScreenMessageStyle.UPPER_LEFT);
      }
    }
  }


  public void FixedUpdate()
  {
    // in flight
    if (Lib.IsFlight())
    {
      // if we are transmitting using the stock system
      if (stream.transmitting())
      {
        // get ec resource handler
        resource_info ec = ResourceCache.Info(vessel, "ElectricCharge");

        // if we are still linked, and there is ec left
        if (CanTransmit() && ec.amount > double.Epsilon)
        {
          // compression factor
          // - used to avoid making the user wait too much for transmissions that
          //   don't happen in background, while keeping transmission rates realistic
          const double compression = 16.0;

          // transmit using the data stream
          stream.update(DataRate * Kerbalism.elapsed_s * compression, vessel);

          // consume ec
          ec.Consume(DataResourceCost * Kerbalism.elapsed_s);
        }
        else
        {
          // abort transmission, return data to the vessel
          stream.abort(vessel);

          // inform the user
          ScreenMessages.PostScreenMessage("Transmission aborted", 5.0f, ScreenMessageStyle.UPPER_LEFT);
        }
      }
    }
  }


  void Transmit(List<ScienceData> queue)
  {
    // this function is here to support two things:
    // - data transmission when Science is disabled
    // - 'triggered' science data (irregardless if Science is enabled or not)

    foreach(ScienceData data in queue)
    {
      // if Science is enabled, it should hijack everything except triggered data
      // if that is not the case, we want to know
      if (Features.Science && !data.triggered)
      {
        throw new Exception("Unable to hijack non-triggered data '" + data.title + "'");
      }

      // add data to the stream
      stream.append(data);
    }
  }


  [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "_", active = true)]
  public void ToggleRelay()
  {
    relay = !relay;
  }


  public override string GetInfo()
  {
    string desc = type == AntennaType.low_gain
      ? "A low-gain antenna for short range comminications with <b>DSN and other vessels</b>, that can also <b>relay data</b>"
      : "An high-gain antenna for long range communications with <b>DSN</b>";
    return Specs().info(desc);
  }


  // specifics support
  public Specifics Specs()
  {
    double[] ranges = new double[12];
    for(int i=0; i < 12; ++i)
    {
      ranges[i] = dist / 13.0 * (double)(i + 1);
    }

    Specifics specs = new Specifics();
    specs.add("Type", type == AntennaType.low_gain ? "low-gain" : "high-gain");
    specs.add("Transmission cost", Lib.BuildString(cost.ToString("F2"), " EC/s"));
    specs.add("Nominal rate", Lib.HumanReadableDataRate(rate));
    specs.add("Nominal distance", Lib.HumanReadableRange(dist));
    specs.add(string.Empty);
    specs.add("<color=#00ffff><b>Transmission rates</b></color>");
    foreach(double range in ranges)
    {
      specs.add(Lib.BuildString(Lib.HumanReadableRange(range),  "\t<b>", Lib.HumanReadableDataRate(calculate_rate(range * 0.99,  dist, rate)), "</b>"));
    }
    return specs;
  }


  // data transmitter support
  public float DataRate { get { vessel_info vi = Cache.VesselInfo(vessel); return vi.is_valid ? (float)vi.connection.rate : 0.0f; } }
  public double DataResourceCost { get { return cost; } }
  public bool CanTransmit() { vessel_info vi = Cache.VesselInfo(vessel); return vi.is_valid && vi.connection.linked; }
  public bool IsBusy() { return false; }
  public void TransmitData(List<ScienceData> dataQueue) { Transmit(dataQueue); }

  // animation group support
  public void EnableModule()      { extended = true; }
  public void DisableModule()     { extended = false; }
  public bool ModuleIsActive()    { return false; }
  public bool IsSituationValid()  { return true; }

  // contract objective support
  public bool CheckContractObjectiveValidity()  { return true; }
  public string GetContractObjectiveType()      { return "Antenna"; }


  // return data rate in kbps
  public static double calculate_rate(double d, double dist, double rate)
  {
    double k = Math.Max(1.0 - d / dist, 0.0);
    return k * k * rate;
  }
}


public sealed class DataStream
{
  public DataStream()
  {
    queue = new List<ScienceData>();
    transmitted = new List<double>();
  }

  public void append(ScienceData data)
  {
    queue.Add(data);
    transmitted.Add(0.0);
  }

  public void update(double size, Vessel v)
  {
    if (queue.Count > 0)
    {
      // get first data in queue
      ScienceData data = queue[0];

      // transmit some
      transmitted[0] += size;

      // if transmission is completed
      if (transmitted[0] >= data.dataAmount)
      {
        // if triggered, fire the triggered data callback
        if (data.triggered)
        {
          // we can't just integrate triggered data with Science data transmission,
          // because virtually all the listener callbacks assume the vessel is loaded
          GameEvents.OnTriggeredDataTransmission.Fire(data, v, false);
        }
        // if not triggered, we just submit the data
        else
        {
          ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(data.subjectID);
          if (subject != null)
          {
            ResearchAndDevelopment.Instance.SubmitScienceData(data.dataAmount, subject, 1.0f, v.protoVessel);
          }
        }

        // remove data from queue
        queue.RemoveAt(0);
        transmitted.RemoveAt(0);
      }
    }
  }

  public void abort(Vessel v)
  {
    foreach(ScienceData data in queue)
    {
      foreach(ModuleScienceContainer container in Lib.FindModules<ModuleScienceContainer>(v))
      {
        // add the data to the container
        container.ReturnData(data);

        // if, for some reasons, it wasn't possible to add the data, try the next container
        // note: this also deal with multiple versions of same data in the entire vessel
        if (!container.HasData(data)) continue;

        // data was added, process the next data
        break;
      }
    }

    queue.Clear();
    transmitted.Clear();
  }

  public string current_file()
  {
    return queue.Count > 0 ? queue[0].title : string.Empty;
  }

  public double current_progress()
  {
    return queue.Count > 0 ? transmitted[0] / queue[0].dataAmount : 0.0;
  }

  public bool transmitting()
  {
    return queue.Count > 0;
  }

  List<ScienceData> queue;
  List<double> transmitted;
}


} // KERBALISM


