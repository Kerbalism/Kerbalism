.. _environment:

Environment
===========

Temperature
-----------
Temperatures in space range from ridiculously low to extremely high. The temperature model in Kerbalism considers

- `solar radiation <https://en.wikipedia.org/wiki/Solar_irradiance>`_ *(the energy flux coming from a star, if not occluded)*
- `albedo radiation <https://en.wikipedia.org/wiki/Albedo>`_ *(the energy flux reflected from a celestial body towards a vessel)*
- `body radiation <https://en.wikipedia.org/wiki/Radiative_cooling>`_ *(the radiative cooling flux from a nearby celestial body)*
- `cosmic background radiation <https://en.wikipedia.org/wiki/Cosmic_microwave_background>`_

The temperature is then obtained according to the `Stefan-Boltzmann law <https://en.wikipedia.org/wiki/Stefan%E2%80%93Boltzmann_law>`_ assuming the vessel is a perfect `black body <https://en.wikipedia.org/wiki/Black_body>`_. Inside an atmosphere, the stock atmospheric temperature model is used instead.

----------

Radiation
---------
Celestial bodies interact in complex ways with radiation. Some have a magnetopause that shields radiation. Others have regions populated by extremely charged particles. The magnetopause is simply a sphere, possibly deformed along the body->star vector to define a magnetotail.

This is modeled with *radiation fields*, regions of space around a celestial body that have an associated radiation level. The overall radiation level for a vessel is determined by evaluating all the fields overlapping at the vessel position.

These fields are rendered in map view or the tracking station. They can be toggled by pressing *Keypad 0/1/2/3*, or by using the *Body Info* window.

.. image:: ../misc/img/rdr/radiation-fields-0.png

Radiation Models can be modified, see the `Modding Kerbalism's Radiation Models <modders/radiation.html>`_ section for more details.

----------

Space weather
-------------
`Coronal Mass Ejection <https://en.wikipedia.org/wiki/Coronal_mass_ejection>`_ events are generated in a stars corona, and move toward either a planetary system or a star-orbiting vessel. A warning will be issued as soon as the CME is ejected towards a body of interest. When the CME hits a planetary system or a star-orbiting vessel, all vessels outside of a magnetopause and in direct line of sight of a Star will receive extra radiation. Vessels inside of a magnetopause will suffer a communications *blackout*. The effects last for some time until the situation returns to normality.