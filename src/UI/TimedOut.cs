using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public static class TimedOut
{
  public static bool timeout(this Panel p, vessel_info vi)
  {
    if (!vi.connection.linked && vi.crew_count == 0)
    {
      p.header(string.Empty);
      p.header(msg[((int)Time.realtimeSinceStartup) % msg.Length]);
      return true;
    }
    return false;
  }

  static string[] msg =
  {
    "<i>Connection in progress</i>",
    "<i>Connection in progress.</i>",
    "<i>Connection in progress..</i>",
    "<i>Connection in progress...</i>",
    "<b><color=#ff0000>[ERROR]</color></b><i> Connection timed-out</i>",
    "<b><color=#ff0000>[ERROR]</color></b><i> Connection timed-out</i>",
    "<b><color=#ff0000>[ERROR]</color></b><i> Connection timed-out</i>",
    "",
    "<i>New tentative in 3s</i>",
    "<i>New tentative in 2s</i>",
    "<i>New tentative in 1s</i>",
    ""
  };
}


} // KERBALISM

