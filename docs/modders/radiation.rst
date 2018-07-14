.. _radiation:

Modding Kerbalism's Radiation Models
====================================

.. image:: ../../misc/img/rdr/radiation-fields-0.png

------

Radiation Model
---------------
A *RadiationModel* defines the signed distance function parameters that determine the shapes of the inner belt, outer belt and magnetopause. The model can be assigned to one or more celestial bodies using *RadiationBody*.

The inner belt is a torus. The *a* radius defines the distance from the section center to the origin. The *b* radius defines the radius of the section.

The outer belt is the boolean subtraction of a torus with another torus. The second torus is equal to the first, except for the fact that the *b* radius is reduced by a border factor. This in turn is not constant everywhere but fades from the *outer_border_start* at the origin to the *outer_border_end* at the domain boundary.

The magnetopause is simply a sphere, possibly deformed along the *body->star* vector to define a magnetotail.

All values are in body radii.

+--------------------+-------------------------------------------------------------------------------+---------+
| PROPERTY           | DESCRIPTION                                                                   | DEFAULT |
+====================+===============================================================================+=========+
| name               | Unique name for the radiation model                                           |         |
+--------------------+-------------------------------------------------------------------------------+---------+
| has_inner          | True if the model has an inner radiation belt                                 | false   |
+--------------------+-------------------------------------------------------------------------------+---------+
| inner_dist         | Inner belt torus *a* radius                                                   |         |
+--------------------+-------------------------------------------------------------------------------+---------+
| inner_radius       | Inner belt torus *b* radius                                                   |         |
+--------------------+-------------------------------------------------------------------------------+---------+
| inner_compression  | Deform space along the *body->star* vector, in direction of the star          | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| inner_extension    | Deform space along the *body->star* vector, in opposite direction of the star | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| inner_quality      | Quality of border for rendering purposes, only influence pre-computation time | 30.0    |
+--------------------+-------------------------------------------------------------------------------+---------+
| inner_deform       | Deform the surface using a sum of sine waves                                  | 0.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| has_outer          | True if the model has an outer radiation belt                                 | false   |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_dist         | Outer belt torus *a* radius                                                   |         |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_radius       | Outer belt torus *b* radius                                                   |         |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_compression  | Deform space along the *body->star* vector, in direction of the star          | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_extension    | Deform space along the *body->star* vector, in opposite direction of the star | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_border_start | Outer belt border extension at the origin                                     | 0.1     |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_border_end   | Outer belt border extension at the domain boundary                            | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_deform       | Deform the surface using a sum of sine waves                                  | 0.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| outer_quality      | Quality of border for rendering purposes, only influence pre-computation time | 40.0    |
+--------------------+-------------------------------------------------------------------------------+---------+
| has_pause          | True if the model has a magnetopause                                          | false   |
+--------------------+-------------------------------------------------------------------------------+---------+
| pause_radius       | Magnetopause radius                                                           |         |
+--------------------+-------------------------------------------------------------------------------+---------+
| pause_compression  | Deform space along the *body->star* vector, in direction of the star          | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| pause_extension    | Deform space along the *body->star* vector, in opposite direction of the star | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| pause_height_scale | Deform space along the magnetic axis vector                                   | 1.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| pause_deform       | Deform the surface using a sum of sine waves                                  | 0.0     |
+--------------------+-------------------------------------------------------------------------------+---------+
| pause_quality      | Quality of border for rendering purposes, only influence pre-computation time | 20.0    |
+--------------------+-------------------------------------------------------------------------------+---------+

------

Radiation Body
--------------
The *RadiationBody* associates a *RadiationModel* to a celestial body and defines the radiation contribution inside the zones delimited by the signed distance function. Radiation values in a zone can be negative, that is usually the case for a magnetopause's contribution.

+-----------------+------------------------------------------------------------------+---------+
| PROPERTY        | DESCRIPTION                                                      | DEFAULT |
+=================+==================================================================+=========+
| name            | Name of the celestial body                                       |         |
+-----------------+------------------------------------------------------------------+---------+
| model           | Name of the *RadiationModel* associated                          |         |
+-----------------+------------------------------------------------------------------+---------+
| radiation_inner | Radiation contribution inside the inner belt, in rad/h           |         |
+-----------------+------------------------------------------------------------------+---------+
| radiation_outer | Radiation contribution inside the outer belt, in rad/h           |         |
+-----------------+------------------------------------------------------------------+---------+
| radiation_pause | Radiation contribution inside the magnetopause, in rad/h         |         |
+-----------------+------------------------------------------------------------------+---------+
| reference       | Index of the body used to determine radiation fields orientation | 0       |
+-----------------+------------------------------------------------------------------+---------+

Radiation is *computed* at a point by walking the *body chain* and summing all contributions for that point from all the fields overlapping with that point. When the top of the chain is reached the radiation value parameter *ExternRadiation* from the `Settings <../settings.html>`_ file is added.

