// RC bench rig — a fake flow meter (and fake sprayer) for an AOG_RC module.
//
// WHY
// On a dry bench an RC module has nothing to measure, so most of the rate stack
// cannot be exercised: no pulses means no measured rate, no quantity totals, no
// catch-test calibration and — the one that matters — no way to tune the valve
// PID before you are in the paddock. This Nano supplies the missing half.
//
// MODES
//   OFF     no pulses.
//   MANUAL  a flow you dial in (pot or serial). Proves the chain end to end:
//           pulses -> module -> AgOpenWeb rate readout -> quantity -> job record.
//   LOOP    a VIRTUAL SPRAYER: reads the module's valve PWM and returns the flow
//           that valve would give, with a first-order lag for the plumbing. The
//           module's PID then has something real to control, so Kp/Ki/deadband
//           can be tuned on the bench and watched on the readout.
//
// WIRING (Nano <-> RC module) — the module is a 12V board, so NEITHER side
// connects directly. Sensor inputs are optocoupled and valve outputs are 12V.
//
//   POWER — take the Nano's 5V from the board's flow-sensor supply. Grounds are
//   then common by construction, which the PWM divider needs anyway.
//
//   PULSE OUT — the flow input is built for a 5V sensor, so a 5V Nano drives it
//   at the level it expects. Measure the terminal to ground, unconnected:
//     reads ~0V  (sourcing, wants driving):  D9 --[220R]--> FLOW terminal
//     reads ~5V  (sinking, has a pull-up):   D9 --[1k]--> B of an NPN
//                                            (2N2222/BC547), E -> GND,
//                                            C -> FLOW terminal
//   Either way one cycle = one counted edge: the ESP32 sees the opto output on
//   INPUT_PULLUP and counts RISING.
//
//   PWM SENSE — the valve output swings to 12V, which would destroy an analog
//   input. Divide it down and filter it in one go:
//       valve output --[10k]--+--> A1
//                             |
//                            [3.3k]  and  [10uF] to GND
//   That gives ~2.98V at 12V in (set with 'v'), and ~25ms smoothing so the duty
//   cycle arrives as a steady voltage. If the output is low-side switched it
//   reads inverted (high with no command) — flip it with 'i1'.
//
//   BIN SIM  — same NPN arrangement as the pulse output, from D4.
//   A0  <-- wiper of a 10k pot (ends to 5V and GND)   manual flow dial
//   D2  <-- momentary button to GND                    cycle mode
//   GND <-> module GND    REQUIRED — the divider and the transistor emitter
//                         both reference the module's ground.
//
// THE MATHS
// A module counts pulses and divides by its meter calibration, so to imitate a
// flow of R units/min at calibration C pulses/unit:
//     Hz = R * C / 60          e.g. 25 L/min at cal 180 -> 75 Hz
// Keep the result inside the module's own pulse window (defaults gate roughly
// 1..1500 Hz) or it will discard the readings.
//
// SERIAL 38400 — '?' lists commands. (Was 115200; this board survived a 12V
// mishap with a heat-shifted clock, and 115200 is the worst-margin baud on a
// 16 MHz AVR — 38400 has ~10x the timing margin and matches the RC module's
// own console rate.)

#include <Arduino.h>

// ---- pins ----
static const uint8_t PIN_PULSE   = 9;    // OC1A — hardware-timed, no jitter
static const uint8_t PIN_PWM_IN  = A1;   // filtered valve PWM (open side in 2-wire)
static const uint8_t PIN_PWM_IN2 = A2;   // 2-wire only: the close side (same divider)
static const uint8_t PIN_POT     = A0;
static const uint8_t PIN_BUTTON  = 2;
static const uint8_t PIN_BIN     = 4;
static const uint8_t PIN_LED     = LED_BUILTIN;

// ---- modes ----
enum Mode : uint8_t { MODE_OFF = 0, MODE_MANUAL = 1, MODE_LOOP = 2 };
static Mode mode = MODE_MANUAL;

// ---- settings (serial-adjustable) ----
static float meterCal   = 180.0f;  // pulses per unit — MATCH the module's setting
static float manualFlow = 20.0f;   // units/min in MANUAL
static float maxFlow    = 60.0f;   // units/min at full PWM in LOOP; also pot full scale
static float tauSec     = 1.5f;    // plumbing lag in LOOP
// Volts seen at A1 when the valve is commanded FULL. With the documented
// 10k/3.3k divider on a 12V output that is ~2.98V. Measure yours and set it
// with 'v' — this is the scale the whole closed loop hangs off.
static float pwmFullV   = 2.98f;
// Low-side switched outputs read backwards (full volts = no command).
static bool  pwmInvert  = false;
// The pot is OPT-IN ('p' arms it): with no pot fitted A0 floats, and a floating
// A0 both drifts past the movement threshold and survives every reset — after
// any bench bump of the USB lead the rig came back up running the phantom
// knob's flow instead of the serial-commanded one.
static bool  usePot     = false;
static bool  potLocked  = true;
// The mode button is DISARMED until 'k1': on a serial-driven bench nothing is
// wired to D2, and a dangling jumper or a meter probe near the pin registers as
// presses — observed cycling the rig into LOOP mid-test. Arm it only when a
// real button is fitted.
static bool  buttonArmed = false;
static bool  binEmpty   = false;
// 2-wire motorised valve ('w1'): the H-bridge REVERSES POLARITY across M1A/M1B
// to run the valve open or closed, and it STAYS where it stops — an integrator,
// not a proportional actuator. Tuning a PID against the wrong plant shape gives
// numbers that are wrong on the real machine, which is the whole reason the
// bench rig exists. Needs the second divider: M1B -> 10k -> A2 (+3.3k/10uF).
static bool  twoWire    = false;
static float travelSec  = 6.0f;    // 'r': full-travel time of the valve
static float valvePos   = 0.0f;    // 0..1 modelled position
static bool  binActiveHigh = false; // match the module's invert-bin flag

// ---- state ----
static float flowNow = 0.0f;       // units/min actually being emitted
static float pwmDuty = 0.0f;       // 0..1 sensed
static float achievedHz = 0.0f;
static int   potLast = -1000;

// Timer1 in CTC toggles OC1A, so the pulse train is generated in hardware and
// keeps exact frequency no matter what the loop is doing.
static void setPulseHz(float hz)
{
  // Only reprogram on a real change. Rewriting the timer every loop and zeroing
  // TCNT1 would restart the waveform ~50x a second, truncating pulses — and the
  // module counts pulses, so the flow it measured would read low.
  static float lastHz = -1.0f;
  static uint8_t lastCs = 0xFF;
  if (fabs(hz - lastHz) < max(0.2f, hz * 0.002f)) return;
  lastHz = hz;

  if (hz < 0.5f) {                       // below the module's window: just stop
    TCCR1A = 0; TCCR1B = 0;
    lastCs = 0xFF;
    digitalWrite(PIN_PULSE, LOW);
    achievedHz = 0;
    return;
  }
  static const uint16_t presc[] = {1, 8, 64, 256, 1024};
  static const uint8_t  csBits[] = {1, 2, 3, 4, 5};
  for (uint8_t i = 0; i < 5; i++) {
    // toggle-on-compare halves the rate, hence the 2
    uint32_t ocr = (uint32_t)(F_CPU / (2.0f * presc[i] * hz)) - 1;
    if (ocr <= 65535UL) {
      noInterrupts();
      TCCR1A = _BV(COM1A0);              // toggle OC1A on compare
      TCCR1B = _BV(WGM12) | csBits[i];   // CTC, this prescaler
      OCR1A  = (uint16_t)ocr;
      // Restart the count only when the prescaler changed; otherwise let the
      // running waveform continue into the new period.
      if (csBits[i] != lastCs) { TCNT1 = 0; lastCs = csBits[i]; }
      interrupts();
      achievedHz = (float)F_CPU / (2.0f * presc[i] * (ocr + 1));
      return;
    }
  }
  TCCR1A = 0; TCCR1B = 0;                // asked for something impossibly slow
  lastCs = 0xFF;
  achievedHz = 0;
}

static float flowToHz(float unitsPerMin) { return unitsPerMin * meterCal / 60.0f; }

// Sense the valve command. The RC filter turns the module's PWM into a level;
// scale by the module's logic voltage so 100% duty reads as 1.0.
static float readDutyRaw(uint8_t pin)
{
  int raw = analogRead(pin);
  float d = raw * (5.0f / 1023.0f) / pwmFullV;
  return d < 0 ? 0 : (d > 1 ? 1 : d);
}

static float readDuty()
{
  float d = readDutyRaw(PIN_PWM_IN);
  if (pwmInvert) d = 1.0f - d;
  return d;
}

static void printStatus()
{
  Serial.print(F("mode="));
  Serial.print(mode == MODE_OFF ? F("OFF") : mode == MODE_MANUAL ? F("MANUAL") : F("LOOP"));
  Serial.print(F("  flow=")); Serial.print(flowNow, 2);
  Serial.print(F(" u/min  hz=")); Serial.print(achievedHz, 1);
  Serial.print(F("  cal=")); Serial.print(meterCal, 1);
  if (mode == MODE_LOOP) {
    Serial.print(F("  duty=")); Serial.print(pwmDuty * 100.0f, 0); Serial.print('%');
    if (twoWire) { Serial.print(F("  pos=")); Serial.print(valvePos * 100.0f, 0); Serial.print('%'); }
    if (pwmInvert) Serial.print(F(" (inv)"));
    Serial.print(F("  max=")); Serial.print(maxFlow, 1);
    Serial.print(F("  tau=")); Serial.print(tauSec, 1); Serial.print('s');
  }
  if (binEmpty) Serial.print(F("  BIN-EMPTY"));
  Serial.println();
}

static void printHelp()
{
  Serial.println(F("RC bench rig — fake flow meter for an AOG_RC module"));
  Serial.println(F("  m0/m1/m2  mode: off / manual / closed-loop"));
  Serial.println(F("  c<val>    meter calibration, pulses per unit (match the module)"));
  Serial.println(F("  f<val>    manual flow, units/min (takes over from the pot)"));
  Serial.println(F("  p         hand control back to the pot"));
  Serial.println(F("  x<val>    max flow at full PWM (also the pot's full scale)"));
  Serial.println(F("  t<val>    plumbing lag, seconds"));
  Serial.println(F("  v<val>    volts at A1 when the valve is FULL (10k/3.3k on 12V = 2.98)"));
  Serial.println(F("  i0/i1     PWM sense normal / inverted (low-side switched output)"));
  Serial.println(F("  b0/b1     bin-empty simulation off / on"));
  Serial.println(F("  k0/k1     mode button disarmed / armed (default off)"));
  Serial.println(F("  w0/w1     LOOP plant: proportional / 2-wire motorised (integrating)"));
  Serial.println(F("  r<sec>    2-wire valve full-travel time"));
  Serial.println(F("  s         status    ?  this help"));
}

void setup()
{
  pinMode(PIN_PULSE, OUTPUT);
  pinMode(PIN_BIN, OUTPUT);
  pinMode(PIN_BUTTON, INPUT_PULLUP);
  pinMode(PIN_LED, OUTPUT);
  digitalWrite(PIN_BIN, binActiveHigh ? LOW : HIGH);   // "not empty"
  Serial.begin(38400);
  Serial.setTimeout(50);                 // snappy: no 1s stall after each command
  delay(200);
  printHelp();
  printStatus();
}

void loop()
{
  static uint32_t lastMs = 0, lastPrint = 0, lastBlink = 0;
  uint32_t now = millis();
  float dt = (now - lastMs) / 1000.0f;
  if (dt < 0.02f) return;               // 50 Hz is plenty
  lastMs = now;

  // mode button, debounced by the loop rate (armed with 'k1' only)
  static bool btnPrev = true;
  bool btn = digitalRead(PIN_BUTTON);
  if (buttonArmed && btnPrev && !btn) {
    mode = (Mode)((mode + 1) % 3);
    printStatus();
  }
  btnPrev = btn;

  // pot — only takes over once actually moved, so a serial-set flow is not
  // immediately overridden by a stationary knob. A FLOATING A0 (no pot wired,
  // the usual dry-bench case) drifts more than the movement threshold every few
  // reads and was re-arming itself over serial control constantly — so 'f'
  // locks the pot out entirely until 'p' asks for it back.
  int pot = analogRead(PIN_POT);
  if (!potLocked && abs(pot - potLast) > 8) { potLast = pot; usePot = true; }
  if (usePot) manualFlow = (pot / 1023.0f) * maxFlow;

  float wanted = 0.0f;
  if (mode == MODE_MANUAL) {
    wanted = manualFlow;
    flowNow = wanted;                    // dialled flow responds immediately
  } else if (mode == MODE_LOOP) {
    if (twoWire) {
      // Integrate: polarity one way opens, the other closes, idle holds.
      // 'i1' swaps which divider is which without rewiring.
      float dOpen  = readDutyRaw(pwmInvert ? PIN_PWM_IN2 : PIN_PWM_IN);
      float dClose = readDutyRaw(pwmInvert ? PIN_PWM_IN : PIN_PWM_IN2);
      pwmDuty = dOpen - dClose;                      // signed, for the status line
      valvePos += (dOpen - dClose) * (dt / travelSec);
      if (valvePos < 0) valvePos = 0; else if (valvePos > 1) valvePos = 1;
      wanted = valvePos * maxFlow;
    } else {
      pwmDuty = readDuty();
      wanted = pwmDuty * maxFlow;
    }
    // first-order lag: a valve and its plumbing do not step instantly, and a
    // PID tuned against an instant response is not tuned for the real machine
    float a = (tauSec <= 0.01f) ? 1.0f : (dt / (tauSec + dt));
    flowNow += (wanted - flowNow) * a;
  } else {
    flowNow = 0.0f;
  }

  setPulseHz(flowToHz(flowNow));

  digitalWrite(PIN_BIN, binEmpty ? (binActiveHigh ? HIGH : LOW)
                                 : (binActiveHigh ? LOW : HIGH));

  // LED: off when idle, blink faster with more flow — a glance says it is alive
  if (flowNow <= 0.01f) {
    digitalWrite(PIN_LED, LOW);
  } else {
    uint32_t period = (uint32_t)(1000.0f / (1.0f + flowNow / 10.0f));
    if (now - lastBlink > period) { lastBlink = now; digitalWrite(PIN_LED, !digitalRead(PIN_LED)); }
  }

  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\r' || c == '\n') continue;
    // Only parse a number for commands that take one: parseFloat() otherwise
    // blocks for the whole serial timeout and can swallow the next command.
    bool takesValue = (strchr("mcfxtvbikwr", c) != NULL);
    float v = takesValue ? Serial.parseFloat() : 0.0f;
    switch (c) {
      case 'm': mode = (Mode)constrain((int)v, 0, 2); break;
      case 'c': if (v > 0) meterCal = v; break;
      case 'f': manualFlow = v; usePot = false; potLocked = true; break;
      case 'p': usePot = true; potLocked = false; break;
      case 'x': if (v > 0) maxFlow = v; break;
      case 't': tauSec = v < 0 ? 0 : v; break;
      case 'v': if (v > 0.2f) pwmFullV = v; break;
      case 'i': pwmInvert = (v >= 1); break;
      case 'b': binEmpty = (v >= 1); break;
      case 'k': buttonArmed = (v >= 1); break;
      case 'w': twoWire = (v >= 1); valvePos = 0; break;
      case 'r': if (v > 0.2f) travelSec = v; break;
      case 's': break;
      case '?': printHelp(); break;
      default: continue;
    }
    printStatus();
  }

  if (now - lastPrint > 1000) { lastPrint = now; printStatus(); }
}
