.. _reliability:

Reliability
===========

MTBF
----

The `Mean Time Between Failures <https://en.wikipedia.org/wiki/Mean_time_between_failures>`_ is specified per-component and indicates how often it will experience a failure on average.

Failures
--------

Failures comes in two variants: *malfunctions* and *critical failures*. The former can be repaired, the latter can't but are less frequent. Both types of failure disable the associated module: that is, the module will stop working.

Every time a component fails on an unmanned vessel, there is a chance that it will be fixed remotely by mission control engineers.

Quality
-------

Manufacturing quality can be specified per-component in the VAB. A high quality will increase the MTBF, but also requires more money and mass. Thus there is a trade off between high reliability and cost/mass of components. Extra cost and mass are expressed in proportion to part cost and mass.

Inspection and Repair
---------------------

All Kerbals can inspect components to reveal some vague information about the time left until the next failure.

Kerbals can also repair malfunctioned components, provided that they have the necessary specialization and experience level required.

Redundancy
----------

The only way to plan around component failures is redundancy. To incentive this behavior, each component is assigned to a *redundancy group* and the planner will analyze redundancies on the vessel using this information. Optionally, when a component fails all others in the same *redundancy group* will be less likely to fail.

Supported modules
-----------------

The system can trigger failures on arbitrary modules in a part, using the Reliability module. This module is added automatically for most stock components.

+-----------------------------+-----------+----------+------------------+------------+------------+
| COMPONENT                   | MTBF (y)  | REPAIR   | REDUNDANCY       | EXTRA COST | EXTRA MASS |
+=============================+===========+==========+==================+============+============+
| Solar Panel                 | 4         | Anyone   | Power Generation | 2.5        | 1.0        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Reaction Wheel (standalone) | 4         | Anyone   | Attitude Control | 2.5        | 1.0        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Reaction Wheel (pod)        | 4         | Anyone   | Attitude Control | 0.25       | 0.15       |
+-----------------------------+-----------+----------+------------------+------------+------------+
| RCS                         | 8         | Engineer |                  | 2.0        | 1.0        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Light                       | 4         | Anyone   |                  | 5.0        | 1.0        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Parachute                   | 8         | Anyone   | Landing          | 2.5        | 0.5        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Engine                      | 8         | Engineer | Propulsion       | 1.0        | 0.1        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Radiator                    | 8         | Engineer |                  | 2.0        | 0.5        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Resource Converter          | 8         | Engineer |                  | 1.0        | 0.2        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Resource Harvester          | 8         | Engineer |                  | 1.0        | 0.2        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Antenna                     | 8         | Anyone   | Comms            | 2.0        | 1.0        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Treadmill (in Hitchiker)    | 4         | Engineer |                  | 0.25       | 0.05       |
+-----------------------------+-----------+----------+------------------+------------+------------+
| ECLSS                       | 8         | Anyone   | Life Support     | 2.5        | 0.1        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Fuel Cell                   | 8         | Engineer | Power Generation | 1.0        | 0.5        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Chemical Plant              | 8         | Engineer |                  | 1.0        | 0.2        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Crustal Harvester           | 8         | Engineer |                  | 1.0        | 0.2        |
+-----------------------------+-----------+----------+------------------+------------+------------+
| Atmospheric Harvester       | 8         | Engineer |                  | 1.0        | 0.5        |
+-----------------------------+-----------+----------+------------------+------------+------------+

