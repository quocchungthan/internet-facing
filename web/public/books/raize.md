# Toyota Raize — a field book

**Learning configuration:** 2019 Japanese-market Z, 1.0-liter 1KR-VET turbo,
2WD, D-CVT. This is deliberately not a blend of Japanese, Indonesian and
Philippine specifications.

**Content contract:** [raize.json](https://github.com/quocchungthan/internet-facing/blob/sub/30s/web/src/content/raize.json).
Each chapter anchor below equals a `parts[].id` in that file. Links open the
matching interactive part at the site's root; no nested application route is
required.

> This is an original educational companion, not Toyota's owner or repair
> manual. The 3D car is a simplified illustration. Never derive lift points,
> fastener torque, clearances, fluid grades or service intervals from it.

## Start with the right car

Toyota's [Japanese launch release][toyota] identifies a 1.0-liter 1KR-VET turbo
engine, D-CVT, front MacPherson struts and a rear torsion beam. It states a body
length of 3,995 mm and width of 1,695 mm, and a luggage capacity of 369 liters
with the deck board in the low position. Its 17-inch wheel description applies
to the specified equipment, not automatically to every Raize.

A brochure establishes design facts, not maintenance instructions. Find the
owner manual that matches the car's build date and chassis in
[Toyota's owner-manual library][manuals]. Export-market specifications and
equipment can differ. Confirm the registration, identification plate, actual
equipment and service records before applying any procedure.

The modeled wheelbase is a silhouette parameter, not an independently
measured dimension or a repair tolerance.

## Before a check

Park safely on level ground, apply the parking brake and switch the engine
off. Let hot parts cool. Remove loose jewelry, keep keys away from accidental
starting, and keep children and pets away. Fans can start unexpectedly.

Do not work under a car supported only by an emergency jack. This book does
not teach lifting, spring compression, hydraulic-brake repair, fuel-system
opening or airbag work. Get qualified help for these tasks.

<a id="engine"></a>
## Engine

[Find the engine in 3D](/?car=raize&part=engine).
Contract: `raize.parts[engine]`.

Three cylinders form a single row. Combustion pushes pistons; connecting rods
turn the crankshaft. The transverse installation puts the engine across the
front of the car, beside its transmission. Turbocharging helps a small
displacement deliver useful torque.

With the engine stopped and cool, observe accessible surfaces for fresh
staining. Note warning lamps, noise, temperature and when symptoms occur.
Those observations help a technician distinguish causes without replacing
parts by guesswork.

**Stop condition:** oil-pressure warning, overheating, strong fuel smell or
severe knocking. Stop safely, switch off and arrange assistance.

![Real 1KR-VET engine bay](/media/raize-engine.jpg)

Photo: オーバードライブ83 / Wikimedia Commons,
[CC BY-SA 4.0][by-sa]. [Original file][engine-photo].
This is a **2021 Indonesian GR Sport**, not the selected 2019 Japanese Z.
The engine family is relevant; trim and packaging may differ. Image displayed
without cropping; the website may scale it.

<a id="lubrication"></a>
## Oil & lubrication

[Explore oil circulation](/?car=raize&part=lubrication).
Contract: `raize.parts[lubrication]`.

The sump stores oil. A pump circulates it through a filter and galleries to
loaded surfaces. A lubricating film reduces metal-to-metal contact and
carries heat away. Turbocharger bearings also rely on appropriate lubrication.

Level, viscosity, performance approval and replacement interval are different
questions. Use the exact manual for each. A generic product description is not
proof of compatibility.

Follow the manual's dipstick procedure on level ground. Record level trends
and oil use; a falling level deserves investigation, not just repeated topping
up. Overfilling can also cause harm. Do not drive with an oil-pressure warning.

<a id="intake"></a>
## Intake & turbo

[Explore the breathing system](/?car=raize&part=intake).
Contract: `raize.parts[intake]`.

The air filter keeps abrasive particles out. Exhaust energy turns a turbine,
which drives a compressor on the intake side. Cooling the compressed air helps
manage its temperature before combustion.

Inspect accessible intake connections only when stopped. A loose hose, filter
problem, sensor fault or turbo issue can produce overlapping symptoms.
Describe whether hesitation happens cold, hot or under load.

Turbo and exhaust housings can remain hot after shutdown. Do not touch them,
disconnect hoses to test boost, or reach into a running engine bay.

<a id="cooling"></a>
## Cooling system

[Find the radiator](/?car=raize&part=cooling).
Contract: `raize.parts[cooling]`.

Coolant transports heat to the radiator. Air passing through the radiator,
assisted by a fan, transfers it to the surroundings. A thermostat controls flow
as the engine warms. A leak anywhere in the circuit can upset this balance.

Check the cold reservoir against its markings and the manual. Observe hoses
and joints for wetness or dried residue. Repeated coolant loss is not normal.
Do not mix unknown coolant types or use color alone to establish compatibility.

**Never open a hot pressure cap.** Scalding liquid can escape suddenly.
Treat a temperature warning as a reason to stop, not permission to keep testing.

<a id="transmission"></a>
## D-CVT

[Explore the transmission](/?car=raize&part=transmission).
Contract: `raize.parts[transmission]`.

Toyota's launch release explains that D-CVT adds split gears to a belt-drive
CVT and uses both belt and gear drive at high speeds to improve transmission
efficiency. This is a design description, not a servicing procedure.

D-CVT is not interchangeable with a Toyota hybrid power-split e-CVT. Do not
borrow fluid advice from a different transmission merely because it also says
"CVT." Checking temperature and fill procedures can be workshop-specific.

Record delayed engagement, shudder, noises and warning messages. Have leaks
identified professionally. No universal flush interval is prescribed here.

<a id="brakes"></a>
## Braking system

[Explore friction braking](/?car=raize&part=brakes).
Contract: `raize.parts[brakes]`.

Hydraulic pressure carries pedal force to the wheel brakes, where friction
converts motion to heat. ABS can modulate pressure near wheel lockup, but tire
grip still sets the limit. The diagram's disc is conceptual and does not
establish the brakes fitted at every corner.

Have friction-material thickness, fluid condition, hoses and leakage inspected
by a qualified technician. Report pulling, grinding, vibration or an abnormal
pedal. If braking is impaired, do not drive; arrange recovery.

<a id="wheels"></a>
## Wheels & tires

[Explore the contact patches](/?car=raize&part=wheels).
Contract: `raize.parts[wheels]`.

Tires support load and transmit acceleration, steering and braking forces.
Use the vehicle placard for cold inflation pressure and the appropriate load
condition. The tire's sidewall maximum is not the recommended vehicle pressure.

[NHTSA's tire guidance][tires] covers pressure, tread, age and damage. Look for
cuts, bulges, embedded objects and uneven wear. Learn whether your car has a
spare or a repair kit and check its condition too.

A damaged tire can be unsafe despite adequate tread depth. Do not drive on a
bulging tire. Do not infer wheel-nut torque or jack locations from this model.

<a id="suspension"></a>
## Suspension

[Explore the suspension](/?car=raize&part=suspension).
Contract: `raize.parts[suspension]`.

The [launch source][toyota] specifies MacPherson struts in front and a torsion
beam at the rear. Springs support the body; dampers control repeated movement.
Bushings and joints guide movement while limiting noise and vibration.

Persistent bouncing, clunks, steering pull or uneven tire wear merit inspection.
After an impact, have the parts inspected before assuming alignment alone is
the cure. Springs store dangerous energy; this book does not teach their removal.

<a id="electrical"></a>
## Battery & electrics

[Explore the low-voltage system](/?car=raize&part=electrical).
Contract: `raize.parts[electrical]`.

The battery supplies starting energy and supports electrical loads. The charging
system replenishes energy while the engine runs. Grounds, terminals, wiring,
fuses and sensors are all part of the system.

Have slow cranking investigated with battery and charging tests together.
Repeated discharge is not proof of a failed battery. Follow the actual manual
for jump-start connection points and sequence. Never bridge terminals with a
tool or disconnect a battery while the engine runs.

<a id="fuel"></a>
## Fuel system

[Explore fuel delivery](/?car=raize&part=fuel).
Contract: `raize.parts[fuel]`.

The tank, pump, lines and injectors store and meter fuel. Use the market-specific
manual and filler label for fuel requirements, not a specification copied from
a different country.

Check that the filler cap closes properly. Report fuel smell or visible wetness
promptly. If fuel leaks, switch off, avoid sparks and flames and seek help.
Opening fuel lines is outside this guide.

<a id="exhaust"></a>
## Exhaust

[Explore the exhaust path](/?car=raize&part=exhaust).
Contract: `raize.parts[exhaust]`.

The exhaust carries hot gases through the turbine, emissions-control equipment
and silencers. New loudness, rattles or cabin exhaust smell need investigation.
Removing emissions equipment is not maintenance.

Never run the car in an enclosed space. Carbon monoxide can be fatal, and
absence of smell does not prove safe air. Exhaust surfaces can burn after
shutdown.

<a id="body"></a>
## Body & access

[Explore body and access panels](/?car=raize&part=body).
Contract: `raize.parts[body]`.

The launch Raize uses the DNGA platform and a high-rigidity body. Structure
locates suspension, protects occupants and supports practical cabin space.
Check lamps, wipers, washers, mirrors and latch security.

The model can open its hood, doors and rear panel to expose concepts. Its hinge
locations and panel proportions are illustrative. Actual lift points and
structural repairs must come from matching service literature.

![Raize family exterior reference](/media/raize-exterior.jpg)

Photo: Andra Febrian / Wikimedia Commons, [CC BY-SA 4.0][by-sa].
[Original file][exterior-photo]. This photograph depicts a 2021 Indonesian
GR Sport. It is a family reference, not a trim-accurate photograph of the selected
Japanese launch car. No crop; display scaling only.

## Keep a useful record

Record the date, odometer, symptom conditions, work performed, products used
and supporting invoice. Use the correct manual's normal or severe-use schedule
as applicable. This book deliberately omits unverified replacement intervals,
fluid capacities, pressures, torque values and diagnostic trouble-code advice.

## References and rights

- [Toyota Japanese launch release][toyota]: vehicle design facts.
- [Toyota owner-manual library][manuals]: find the exact applicable manual.
- [NHTSA tire safety][tires]: general tire checks, not Raize-specific pressures.
- Photos retain their individual licenses; attribution appears beside each.
- Text and 3D geometry are original educational work, not reproduced manuals.
- No affiliation with Toyota or Daihatsu is implied.

[toyota]: https://global.toyota/en/newsroom/toyota/29251184.html
[manuals]: https://toyota.jp/ownersmanual/
[tires]: https://www.nhtsa.gov/vehicle-safety/tires
[by-sa]: https://creativecommons.org/licenses/by-sa/4.0/
[engine-photo]: https://commons.wikimedia.org/wiki/File:2021_Toyota_Raize_1.0_Turbo_S_GR_Sport_A250RA_1KR-VET_engine_(20210521).jpg
[exterior-photo]: https://commons.wikimedia.org/wiki/File:2021_Toyota_Raize_1.0T_GR_Sport_(Indonesia)_front_view.jpg
