using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Drive
{
  public Drive()
  {
    files = new Dictionary<string, File>();
    samples = new Dictionary<string, Sample>();
    location = 0;
  }

  public Drive(ConfigNode node)
  {
    // parse science  files
    files = new Dictionary<string, File>();
    if (node.HasNode("files"))
    {
      foreach(var file_node in node.GetNode("files").GetNodes())
      {
        files.Add(DB.from_safe_key(file_node.name), new File(file_node));
      }
    }

    // parse science samples
    samples = new Dictionary<string, Sample>();
    if (node.HasNode("samples"))
    {
      foreach(var sample_node in node.GetNode("samples").GetNodes())
      {
        samples.Add(DB.from_safe_key(sample_node.name), new Sample(sample_node));
      }
    }

    // parse preferred location
    location = Lib.ConfigValue(node, "location", 0u);
  }

  public void save(ConfigNode node)
  {
    // save science files
    var files_node = node.AddNode("files");
    foreach(var p in files)
    {
      p.Value.save(files_node.AddNode(DB.to_safe_key(p.Key)));
    }

    // save science samples
    var samples_node = node.AddNode("samples");
    foreach(var p in samples)
    {
      p.Value.save(samples_node.AddNode(DB.to_safe_key(p.Key)));
    }

    // save preferred location
    node.AddValue("location", location);
  }

  // add science data, creating new file or incrementing existing one
  public void record_file(string subject_id, double amount, double max_amount)
  {
    // create new data or get existing one
    File file;
    if (!files.TryGetValue(subject_id, out file))
    {
      file = new File();
      files.Add(subject_id, file);
    }

    // increase amount of data stored in the file
    file.size += amount;
    
    // clamp to max data collectible
    file.size = Math.Min(file.size, max_amount);
  }

  // add science sample, creating new sample or incrementing existing one
  public void record_sample(string subject_id, double amount, double max_amount)
  {
    // create new data or get existing one
    Sample sample;
    if (!samples.TryGetValue(subject_id, out sample))
    {
      sample = new Sample();
      samples.Add(subject_id, sample);
    }

    // increase amount of data stored in the sample
    sample.size += amount;
    
    // clamp to max data collectible
    sample.size = Math.Min(sample.size, max_amount);
  }

  // remove science data, deleting the file when it is empty
  public void delete_file(string subject_id, double amount)
  {
    // get data
    File file;
    if (files.TryGetValue(subject_id, out file))
    {
      // decrease amount of data stored in the file
      file.size -= amount;

      // remove file if empty
      if (file.size <= double.Epsilon) files.Remove(subject_id);
    }
  }

  // remove science sample, deleting the sample when it is empty
  public void delete_sample(string subject_id, double amount)
  {
    // get data
    Sample sample;
    if (samples.TryGetValue(subject_id, out sample))
    {
      // decrease amount of data stored in the sample
      sample.size -= amount;

      // remove sample if empty
      if (sample.size <= double.Epsilon) samples.Remove(subject_id);
    }
  }

  // set send flag for a file
  public void send(string subject_id, bool b)
  {
    File file;
    if (files.TryGetValue(subject_id, out file))
    {
      file.send = b;
    }
  }

  // set analyze flag for a sample
  public void analyze(string subject_id, bool b)
  {
    Sample sample;
    if (samples.TryGetValue(subject_id, out sample))
    {
      sample.analyze = b;
    }
  }

  // move all data to another drive
  public void move(Drive destination)
  {
    // copy files
    foreach(var p in files)
    {
      destination.record_file(p.Key, p.Value.size, Science.experiment_size(p.Key));
    }

    // copy samples
    foreach(var p in samples)
    {
      destination.record_sample(p.Key, p.Value.size, Science.experiment_size(p.Key));
    }

    // clear source drive
    files.Clear();
    samples.Clear();
  }


  // return size of data stored in Mb (including samples)
  public double size()
  {
    double amount = 0.0;
    foreach(var p in files)
    {
      amount += p.Value.size;
    }
    foreach(var p in samples)
    {
      amount += p.Value.size;
    }
    return amount;
  }


  // transfer data between two vessels
  public static void transfer(Vessel src, Vessel dst)
  {
    // get drives
    Drive a = DB.Vessel(src).drive;
    Drive b = DB.Vessel(dst).drive;

    // get size of data being transfered
    double amount = a.size();

    // if there is data
    if (amount > double.Epsilon)
    {
      // transfer the data
      a.move(b);

      // inform the user
      Message.Post
      (
        Lib.BuildString(Lib.HumanReadableDataSize(amount), " of data transfered"),
        Lib.BuildString("from <b>", src.vesselName, "</b> to <b>", dst.vesselName, "</b>")
      );
    }
  }


  public Dictionary<string, File> files;      // science files
  public Dictionary<string, Sample> samples;  // science samples
  public uint location;                       // where the data is stored specifically, optional
}


} // KERBALISM

