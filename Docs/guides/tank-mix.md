# Tank mix calculator

## What this is

Working out a spray mix means the same arithmetic every time: area × water rate,
how many tanks that is, and how much of each chemical goes into every fill. The
Tank Mix calculator does that arithmetic and shows its working, so you can
sanity-check the numbers standing at the induction hopper. The mix is saved with
the job, and your chemical list (with each product's usual rate) is remembered
by the app.

## You will need

- A field open (for the automatic area buttons), or just type the hectares in.
- Your water (carrier) rate and sprayer tank size.

## Using it

1. Open **Field Tools → Tank Mix**.
2. **Area** — tap **Field** for the whole field, **Left** for what's not yet
   worked (field minus painted coverage — handy at a mid-job refill), or type
   hectares directly.
3. **Carrier** — your water rate in L/ha (e.g. 150).
4. **Tank** — sprayer tank size in litres. If Rate Control has a product set
   up, its tank size is used automatically; you can still change it here.
5. **Buffer** — optional extra litres of water on top of the area requirement
   (priming, double-sprayed headlands, a don't-run-dry margin). Chemicals dosed
   per 100 L of water scale with the buffer; chemicals dosed per hectare do
   not — the output notes this.
6. **Chemicals** — tap **+ Add chemical**, give it a name, a rate, and pick the
   rate basis: **L/ha, mL/ha, kg/ha, g/ha, mL/100L, L/100L**. Products you've
   used before appear as one-tap buttons with their remembered rate.

## Reading the output

    27.4 ha × 150 L/ha = 4110 L water
    3 fills from a 1500 L tank (10.0 ha per full tank)

    Glyphosate — 4 L/ha
      total: 110 L
      per full tank: 40.0 L
      last fill (1110 L): 29.6 L

Full tanks are identical, so they're one line; the part-filled last tank gets
its own amounts.

## Refill points on the map

With a planned route active (Route Planner), tap **Show refill points on
route**. The map marks amber dots where each tank will run dry along the
planned path, using your carrier rate and tank size — so you know before you
start whether the last fill runs out by the gate or in the far corner.

## Notes

- The calculator plans quantities; it does not read the label for you. Always
  follow the product label for rates, mixing order and compatibility.
- Everything is metric (hectares, litres, kilograms).
