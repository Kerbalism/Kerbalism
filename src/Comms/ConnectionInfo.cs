using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	// link state
	public enum LinkStatus
	{
		direct_link,
		indirect_link,
		no_link,
		blackout
	};


	public sealed class ConnectionInfo
	{
		public ConnectionInfo(LinkStatus status = LinkStatus.no_link, double rate = 0.0, double strength = 0.0, double cost = 0.0, string target_name = "")
		{
			this.linked = status == LinkStatus.direct_link || status == LinkStatus.indirect_link;
			this.status = status;
			this.rate = rate;
			this.strength = strength;
			this.cost = cost;
			this.target_name = target_name;
		}

		public ConnectionInfo(ConnectionInfo other)
		{
			this.linked = other.linked;
			this.status = other.status;
			this.rate = other.rate;
			this.strength = other.strength;
			this.cost = other.cost;
			this.target_name = other.target_name;
		}

		public bool linked;         // true if there is a connection back to DSN
		public LinkStatus status;   // the link status
		public double rate;         // data rate in Mb/s
		public double strength;     // signal strength
		public double cost;         // EC/s consumed for transmission
		public string target_name;
	}


} // KERBALISM

