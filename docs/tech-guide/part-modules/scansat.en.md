The SCANsat part module will be added to all SCANsat scanners, there is nothing that can, or should, be customized.

## How SCANsat works under the hood

In a stock KSP game, SCANsat completely bypasses all data transmission while a probe is scanning a body and gernerating a map. The scanner part has a button that lets you generate data, it is that data you have to transmit back to Kerbin to get science points. But that is totally independent from the map generation itself. You can scan a body with a probe, never transmit anything back from that probe, and still see the map in the SCANsat windows.

I looked into the SCANsat code to search for ways to change that, can't be done. So we have to work around that.

Kerbalism always had special integration for SCANsat, before 3.0 it was used to turn SCANsat scanners off if there is no EC remaining on an unloaded vessel, and then back on again once EC is restored. The new science system does something similar.

There is a part module, KerbalismScansat, that acts as a bridge between Kerbalism and the SCANsat mod. What that part module does is: look at how much of the body has been scanned (in percent), and if the percentage went up, generate some data and store it. _How much_ data depends on _the difference in scan completion percentage_. Then, if the drive is full, it turn the scanner off until the drive is empty again.

This simulates the need to transmit data back before you can see a map, although technically it doesn't exactly work like that. If you're clever, you could just keep deleting the file on the hard drive of the probe, and the scanner would happily continue scanning without transmitting anything. But then you would loose the science points for the part that you deleted - forever, there is no way you can re-generate that data.

SCANsat updates it's scanners periodically at intervals. This means that depending on your current time warp speed, a probe could be scanning a big chunk of a body between two updates, which then would generate an enormeous amount of data in the drive, much much more than it can store. Kerbalism would turn the scanner off, wait until the data has been transmitted, and then turn it on again. At that point, whatever part of the body that happens to be in the view range of the probe at that point will be considered as scanned by SCANsat, which will likely lead to the next enormeous chunk of data being generated. There is no way to scan less, or to slow the scanner down any other way than turn it off and on again.