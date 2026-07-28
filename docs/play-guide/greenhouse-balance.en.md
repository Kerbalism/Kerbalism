# Greenhouse Continuous Production and Mass Break-Even Analysis

Related: Kerbalism issue [#1073](https://github.com/Kerbalism/Kerbalism/issues/1073); design discussion in Kerbalism4 [#7](https://github.com/Kerbalism/Kerbalism4/issues/7).

---

## 1. Abstract

This document records the greenhouse production model change and the associated mass-balance retuning for the Default profile (with SSPX throughput scaled to preserve its output multipliers).

Two outcomes are specified:

1. **Production model:** batch harvest gameplay is replaced by continuous Food production subject to environmental and resource constraints.
2. **Mass economy:** the dry-structure payback proxy is reduced from approximately **150 Kerbin years** to approximately **5 Kerbin years** at baseline configuration. Complete launch-mass results depend on loaded feedstock, storage hardware, recycling, and ISRU assumptions.

---

## 2. Scope and assumptions

### 2.1 Comparison models

No single number describes every vessel design, so this report separates four accounting cases:

| Case | Stored-Food side | Greenhouse side |
|---|---|---|
| Dry-structure proxy | Food resource mass only | Greenhouse dry mass only |
| Default wet greenhouse | Food resource mass only | Dry mass plus resources loaded by the part config |
| Representative boxed Food | Food, B9 tank mass, and amortized container base mass | Default wet greenhouse |
| Full logistics | Storage hardware and all launched consumables | Greenhouse, buffer storage, recycler/ISRU hardware, power, and unsourced feedstock |

The headline **5.05-year** value is the first case. It excludes all greenhouse input flows and all storage hardware and must not be interpreted as complete launch mass.

The representative container case uses the 678-unit Kerbalism large supply box in B9 Food mode: 0.036 t base mass plus 0.000106275 t tank mass per Food unit. Other container selections produce different values.

Oxygen by-product and scrubber displacement are not credited. Conversely, omitting feedstock and recycler hardware favors the greenhouse, so the dry proxy is not a globally conservative bound.

### 2.2 Dry-structure payback definition

```text
T_BE = m_dry / m_dot_Food
```

| Symbol | Meaning |
|---|---|
| `T_BE` | Dry-structure payback duration (Kerbin days; convert to years ÷ 426) |
| `m_dry` | Greenhouse part dry mass (tonnes) |
| `m_dot_Food` | Food mass production rate at full duty cycle (tonnes / Kerbin day) |

Kerbin calendar: **426 days / year**, **21600 s / day**.

Food mass density used for conversion: **0.000281 t / unit** (CRP-consistent value used in profile commentary).

Crew Food demand reference: eating rule `0.1312141885` units / meal × 2 meals / Kerbin day ≈ **0.2624 units / Kerbin-day / Kerbal**.

### 2.3 Continuous Food rate

```text
n_dot_Food = crop_size * crop_rate    # resource units / second
m_dot_Food = n_dot_Food * 21600 * 0.000281    # tonnes / Kerbin day
```

At baseline, configured input/output rates were scaled with Food throughput. Efficiency upgrades intentionally change these ratios: Efficiency1 raises Food without raising material inputs, while Efficiency2 combines higher Food output with `input_rate_mult = 0.85`. Oxygen output remains at the configured baseline rate.

---

## 3. Legacy baseline (pre-change)

| Parameter | Value |
|---|---|
| Dry mass `m_dry` | 2.5 t |
| `crop_size` × `crop_rate` | ≈ 0.138 Food / Kerbin day |
| Equivalent crew support | ≈ 0.53 Kerbal |
| `m_dot_Food` | ≈ 3.9e-5 t / Kerbin day |
| `T_BE` | ≈ **151 Kerbin years** |

Interpretation: even under the dry-structure proxy, stored Food dominated for mission durations typical of stock career / exploration play. Functional production existed, but the mass payback horizon rendered the part economically inactive for most use cases.

---

## 4. Implemented changes

### 4.1 Production model

| Aspect | Specification |
|---|---|
| Output | Continuous Food (plus configured by-products, e.g. Oxygen) while the module is active and constraints are satisfied |
| Harvest UI | Removed (Harvest / Emergency Harvest / auto-harvest) |
| Crew requirement | None for production |
| Pause conditions | Insufficient lighting, pressure, or radiation margin; missing inputs; Food storage full (non-dumpable); insufficient EC when artificial lighting is required |
| Resume | Automatic when blocking conditions clear |
| Contract landmark | `space_harvest` set on first in-space Food production |

### 4.2 Balance parameters (stock `kerbalism-greenhouse`, Default profile)

| Parameter | Legacy | Retuned |
|---|---|---|
| Dry mass | 2.5 t | **1.5 t** |
| Food throughput scale vs legacy Prototype rates | 1× | **18×** |
| NH₃ / H₂O / WasteAtmosphere / CO₂ / O₂ rates | Legacy | **18×** (stoichiometry vs Food unchanged) |
| Loaded NH₃ / H₂O / CO₂ capacities | Legacy | **18×** |
| Loaded N₂ capacity | 10,000 units | **10,000 units** (unchanged) |
| Lamp `ec_rate` | 2.5 EC/s | **2.5 EC/s** (unchanged) |

SSPX greenhouse modules retain their **throughput** multipliers versus stock: Food rates, matching inputs/outputs, and non-N₂ onboard capacities were scaled 18×. SSPX part dry masses were not retuned, so their mass-payback durations are not equal to the stock greenhouse.

### 4.3 Career part upgrades

| Upgrade ID | Tech node | Multipliers |
|---|---|---|
| `Greenhouse-Efficiency1` | `fieldScience` | `food_rate_mult` 1.25, `ec_rate_mult` 0.8 |
| `Greenhouse-Efficiency2` | `experimentalScience` | `food_rate_mult` 1.5, `input_rate_mult` 0.85, `ec_rate_mult` 0.65 |

Late-tier values are absolute (not stacked multiplicatively on mid-tier).

The wildcard module-upgrade patch runs in ModuleManager `:FINAL`, after stock and support patches have added their `Greenhouse` modules. It applies to every `Greenhouse` module on a part.

---

## 5. Results

### 5.1 Throughput and dry-structure payback

| Configuration | ≈ Crew Food support | Dry-structure `T_BE` |
|---|---|---|
| Retuned baseline | 9.45 | **5.05094 years** |
| Efficiency1 (`food_rate_mult` 1.25) | 11.82 | **4.04075 years** |
| Efficiency2 (`food_rate_mult` 1.5) | 14.18 | **3.36729 years** |
| Legacy reference | 0.525 | **151.528 years** |

The baseline dry proxy is exactly 30× shorter than the legacy proxy.

### 5.2 Launch-mass accounting cases

The current stock greenhouse loads 4,896 NH₃, 81,000 CO₂, 10,000 N₂, and 198 H₂O. Using CRP densities, these resources total **0.372306 t**, for a default launch wet mass of **1.872306 t**.

| Accounting case | Baseline result | Interpretation |
|---|---|---|
| 1.5 t dry greenhouse vs bare Food | 5.05094 y | Stable balance proxy; excludes real tankage and feedstock |
| 1.872306 t wet greenhouse vs bare Food | 6.30460 y | Includes greenhouse contents but not Food containers; intentionally asymmetric |
| Wet greenhouse vs amortized 678-unit boxed Food | ≈ 4.02309 y | Illustrative symmetric-storage comparison; container choice dependent |
| All continuous inputs launched without recycling/ISRU | No finite break-even | Input mass rate exceeds displaced Food mass rate |

For the representative boxed-Food case, whole containers produce a step function: six full boxes are lighter than the wet greenhouse, while the seventh is heavier, placing the discrete transition at approximately **3.85 years**. A continuous greenhouse also requires positive Food buffer capacity; its buffer hardware is vessel-dependent and is not included above.

### 5.3 Food-only mission-duration reference

The following values are Food resource mass only and are retained as a throughput reference, not a complete vessel comparison:

| Duration `T` | Food mass produced |
|---|---|
| 1 Kerbin year | ≈ 0.30 t |
| 3 Kerbin years | ≈ 0.89 t |
| 5 Kerbin years | ≈ 1.48 t |
| 6 Kerbin years | ≈ 1.78 t |
| 10 Kerbin years | ≈ 2.97 t |

### 5.4 Operational implications

- Under the dry proxy, stored Food is lighter below 5.05 years and greenhouse dry structure is lighter above it.
- Default loaded resources move the greenhouse-only side to 6.30 years, while Food-container structure moves the stored-supply side in the opposite direction.
- If all NH₃/H₂O/CO₂ demand must be launched continuously, there is no finite mass payback. Recycling, crew WasteAtmosphere, resupply, or ISRU is a required assumption for multi-year full-duty operation.
- O₂ production and scrubber replacement may improve system-level results but require a vessel-wide life-support analysis.

---

## 6. Runtime constraints

Full-rate production requires concurrent satisfaction of:

1. Lighting ≥ `light_tolerance` (natural and/or artificial; artificial lighting consumes EC at `ec_rate × ec_rate_mult`)
2. Habitat pressure ≥ `pressure_tolerance` (if configured)
3. Post-shielding habitat radiation below `radiation_tolerance` (if configured)
4. Configured inputs available (including WasteAtmosphere / CO₂ combination rules)
5. Positive free capacity for Food

Failure of any constraint pauses the process; recovery is automatic. Unmanned vessels are supported.

---

## 7. Design rationale

1. **Process alignment:** continuous converters match other Kerbalism life-support modules and remove harvest micromanagement ([#1073](https://github.com/Kerbalism/Kerbalism/issues/1073)).
2. **Economic activation:** a ~150-year dry proxy left the part unused; the ~5-year retuned dry proxy places it in long-duration station / colony / ISRU regimes discussed in [#7](https://github.com/Kerbalism/Kerbalism4/issues/7).
3. **Intentional short-mission preference for stored Food:** break-even is not driven to near-zero; sub-threshold missions retain the lighter stored-Food solution.
4. **Throughput vs dry mass trade:** increasing Food rate (~0.5 → ~9.5 Kerbal equivalent) is the primary lever for `T_BE`; dry mass reduction (2.5 → 1.5 t) is secondary. The module is sized as mid-tier life support, not a low-output experimental bay.

---

## 8. Limitations and compatibility notes

| Topic | Note |
|---|---|
| Feedstock accounting | The 5.05-year proxy omits all continuous input flows. With all inputs launched, no finite break-even exists. |
| Carbon supply | A matched 9.45-Kerbal crew supplies only about 39% of configured WasteAtmosphere/CO₂ demand; additional carbon recovery or supply is required in non-breathable environments. |
| Tankage | The default greenhouse carries 0.372306 t of resources. Stored Food also requires container structure; the 4.02-year example is container-specific. |
| Co-benefits | O2 production / scrubber displacement omitted from `T_BE` (conservative). |
| Save migration | Active greenhouses transition to continuous production without harvest; rates follow updated configs; dry mass follows current part definitions. |
| Third-party parts | SSPX throughput multipliers are preserved, but part masses are unchanged and therefore payback varies by part. |
| Part upgrades | Efficiency multipliers apply via stock `PARTUPGRADE` / module `UPGRADES` attached in `:FINAL`. |

---

## 9. Numerical summary

```text
Legacy dry proxy:       2.5 t / 0.0164986 t Food per year = 151.528 years
Retuned dry proxy:      1.5 t / 0.2969745 t Food per year = 5.05094 years
Efficiency2 dry proxy:  1.5 t / 0.4454618 t Food per year = 3.36729 years
Default wet / bare Food:                                    6.30460 years
Default wet / representative amortized boxed Food:         ≈4.02309 years
All continuous feedstock launched:                         no finite break-even
```
