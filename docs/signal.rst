.. _signal:

Signal
======

Connections
-----------
To transmit data, vessels need a valid communications link with the `Deep Space Network <https://en.wikipedia.org/wiki/NASA_Deep_Space_Network>`_ (DSN) on the surface of your home planet. For unmanned vessels, this communication link is also required for remote control. Celestial bodies occlude the signal, and other vessels can act as relays. Science data transmission speed is attenuated by signal strength and distance.

----------

Antennas
--------
Antennas comes in two types: *Internal* and *External*.

- Internal antennas as the name implies are fitted internally to probes and manned pods and can be used for short-range telemetry communications with the DSN and other vessels, they allow for operation of vessels via a control signal, these antennas also require constant power to operate.
- External antennas are the externally fitted antennas, these allow for longer distance communications and boost the telemetry and control signals, they are also used for transmitting science data or relaying data in the case of relay antennas. These antennas are also required for the Comfort bonus *call home*.

----------

Range and Rate
--------------

When transmitting science, the data rate of that transmission depends on the type of antenna and on the distance. Data rates will be very low for very long distances, and reasonably high over short distances. Keep this in mind when planning missions into deep space.

Combining antennae will increase the communications range, but only to a very limited extend will it increase the data rate. When using multiple antennae at the same time, their combined data range will be the geometric mean of their individual rates.

What this means in practice is this: if you combine 2 or more antennae of the same type, you will increase the total communication range of the vessel and at the same time decrease the loss of data rate with distance. When you compare a vessel with one antenna with another vessel that has two antennae, they will both be able to transmit (almost) equally fast over short distances. But at long range, the vessel with 2 antennae will have an advantage.

This also means that you will want to combine antennae of same or similar capabilities only. If you combine one very fast antenna with one that is very weak, you will sacrifice almost all of the speed benefits you get from the fast antenna for the added range benefit of the weak antenna.

----------

Transmission cost
-----------------
Transmitting data consumes *ElectricCharge*. The cost is fixed and doesn't change with distance or signal strength.

Transmitters will use more EC during transmission, since they have to power their signal amplifier for sending. While passive (not sending), EC consumption will be a fraction of the transmission cost.

----------

Extending antennas
------------------
Deployable Antennas need to be extended to work. This can be used by the player to configure what antennas are used for transmission, at any time. Extending and retracting antennas is possible even when the vessel is not controllable.

----------

Control loss
------------
Kerbalism will use the stock CommNet system if it is enabled allowing for 3 different models for control loss in vessels without a connection, the **none** model, causes complete loss of control on unlinked vessels. The **limited** model instead permits partial control of the vessel, the **full** model causes no loss of control, so that signal loss only affects the science data transmission. If CommNet is not enabled then connections will always be available unless you run out of Electric Charge or antennas break.
