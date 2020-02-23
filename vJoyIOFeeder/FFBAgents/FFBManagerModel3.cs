﻿//#define CONSOLE_DUMP

using System;
using System.Globalization;
using System.Threading;

// Don't forget to add this
using vJoyIOFeeder.Utils;

namespace vJoyIOFeeder.FFBAgents
{
    /// <summary>
    /// See :
    /// http://superusr.free.fr/model3.htm
    /// </summary>
    public abstract class FFBManagerModel3 :
        AFFBManager
    {
        /// <summary>
        /// Known commands Generic model3
        /// Only torque commands available with 16 levels.
        /// Be carefull, lemans and scud/daytona have reversed
        /// commands for left/right
        /// </summary>
        public enum GenericModel3CMD : int
        {
            SEQU = 0x00,
            NO_EFFECT = 0x10,

            TURNRIGHT = 0x50,
            TURNLEFT = 0x60,

            MOTOR_LEVEL50 = 0x70,
            MOTOR_LEVEL60 = 0x71,
            MOTOR_LEVEL70 = 0x72,
            MOTOR_LEVEL80 = 0x73,
            MOTOR_LEVEL90 = 0x74,
            MOTOR_LEVEL100 = 0x75,

            PING = 0xFF,
        }

        /// <summary>
        /// Maximum effect level
        /// </summary>
        protected int MAX_LEVEL = 0xF;

        /// <summary>
        /// True if torque emulation is used for unknown effects
        /// </summary>
        public bool UseTrqEmulationForMissing = true;
        /// <summary>
        /// True if short pulses of torque commands are used to resolve small
        /// values. Allows greater resolution of torque, but "cracks" can be
        /// feeled by the user
        /// </summary>
        public bool UsePulseSeq = true;

        protected int[,] PulseSequences = new int[,] {
            {1, 0, 0, 0 },
            {1, 0, 1, 0 },
            {1, 1, 1, 0 },
            {1, 1, 1, 1 },
        };

        public FFBManagerModel3(int refreshPeriod_ms) :
            base(refreshPeriod_ms)
        {
            this.WheelSign = -1.0;
        }


        /// <summary>
        /// When states are too complicated, a separate method is called.
        /// </summary>
        protected override void DeviceStateMachine()
        {
            switch (State) {
                case FFBStates.UNDEF:
                    TransitionTo(FFBStates.DEVICE_INIT);
                    break;
                case FFBStates.DEVICE_INIT:
                    State_INIT();
                    break;
                case FFBStates.DEVICE_RESET:
                    State_RESET();
                    break;
                case FFBStates.DEVICE_DISABLE:
                    State_DISABLE();
                    break;
                case FFBStates.DEVICE_READY:
                    State_READY();
                    break;
                case FFBStates.DEVICE_EFFECT_RUNNING:
                    ComputeTrqFromAllEffects();
                    break;
            }
        }

        protected override void ComputeTrqFromAllEffects()
        {
            // Inputs:
            // - R=angular reference
            // - P=angular position
            // - W=angular velocity (W=dot(P)=dP/dt)
            // - A=angular accel (A=dot(W)=dW/dt=d2P/dt2)
            // output:
            // OutputEffectCommand

            // Take a snapshot of all values - convert time base to period
            EnterBarrier();
            double R = RefPosition_u;
            double P = FiltPosition_u_0;
            double W = FiltSpeed_u_per_s_0;
            double A = FiltAccel_u_per_s2_0;
            // Release the lock
            ExitBarrier();

            // For model 3 hardware, effects are directly translated to commands
            // for the driveboard

            // If using torque emulated mode, then effect will fill "AllTrq" and
            // the value will be converted to a left/right torque command
            double AllTrq = 0.0;
            bool translTrq2Cmd = false;
            for (int i = 0; i<RunningEffects.Length; i++) {
                // Skip effect not running
                if (!RunningEffects[i].IsRunning) {
                    continue;
                }
                double Trq = 0.0;
                switch (RunningEffects[i].Type) {
                    case EffectTypes.NO_EFFECT:
                        OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                        break;
                    case EffectTypes.CONSTANT_TORQUE: {
                            Trq = TrqFromConstant(i);
                            // Set flag to convert it to constant torque cmd
                            translTrq2Cmd = true;
                        }
                        break;
                    case EffectTypes.RAMP: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromRamp(i);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.FRICTION: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromFriction(i, W);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.INERTIA: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromInertia(i, W, this.RawSpeed_u_per_s, A, 0.2, 0.1, 50.0);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.SPRING: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromSpring(i, R, P);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.DAMPER: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromDamper(i, W, A, 0.3, 0.5);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.SINE: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromSine(i);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                                // All done
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.SQUARE: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromSquare(i);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                                // All done
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.TRIANGLE: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromTriangle(i);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                                // All done
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.SAWTOOTHUP: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromSawtoothUp(i);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                                // All done
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;
                    case EffectTypes.SAWTOOTHDOWN: {
                            if (UseTrqEmulationForMissing) {
                                Trq = TrqFromSawtoothDown(i);
                                // Set flag to convert it to constant torque cmd
                                translTrq2Cmd = true;
                                // All done
                            } else {
                                // No effect
                                OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                            }
                        }
                        break;

                    default:
                        break;
                }
                AllTrq += Trq * RunningEffects[i].Gain;
            }

            // If using Trq value, then convert to constant torque effect
            if (translTrq2Cmd) {
                // Change sign of torque if inverted and apply gains
                AllTrq = TrqSign* Math.Sign(AllTrq) * Math.Pow(Math.Abs(AllTrq), PowLow) * DeviceGain * GlobalGain;
                // Scale in range
                AllTrq = Math.Max(Math.Min(AllTrq, 1.0), -1.0);
                // Save value
                OutputTorqueLevel = AllTrq;
                // Now convert to command
                TrqToCommand();
            }

            this.CheckForEffectsDone();
        }


        protected virtual void TrqToCommand(
            int CmdNoTorque = (int)GenericModel3CMD.NO_EFFECT,
            int CmdTurnLeft = (int)GenericModel3CMD.TURNLEFT,
            int CmdTurnRight = (int)GenericModel3CMD.TURNRIGHT)
        {
            // Trq is now in [-1; 1]
            double Trq = OutputTorqueLevel;

            // Dead-band for very small torque values
            if (Math.Abs(Trq)< (1.0/((MAX_LEVEL+1)<<3))) {

                // No effect
                OutputEffectCommand = CmdNoTorque;

            } else if (Trq<0) {

                // Turn right - negative torque
                // If using fast switching between levels
                if (UsePulseSeq) {

                    // Resolve to 4x more levels, then make pulses
                    int levels = (int)(-Trq*((MAX_LEVEL<<2)+3));
                    int reminder = levels&0b11; //0..3

                    int upcmd = CmdTurnRight + (levels>>2);
                    long downcmd = upcmd-1;
                    if (downcmd < CmdTurnRight)
                        downcmd = CmdNoTorque; //resolve to no torque

                    // Take the sub-period we are in
                    int subper = (int)(this.Timer.Tick&0b11);
                    var pulse = PulseSequences[reminder, subper];
                    if (pulse==1) {
                        OutputEffectCommand = upcmd;
                    } else {
                        OutputEffectCommand = downcmd;
                    }

                } else {

                    int strength = (int)(-Trq* MAX_LEVEL);
                    OutputEffectCommand = (int)CmdTurnRight + strength;

                }

            } else {
                // Turn left - positive
                // If using fast switching UseFastSwitching
                if (UsePulseSeq) {

                    // Resolve to 4x more levels, then make pulses
                    int levels = (int)(Trq*((MAX_LEVEL<<2)+3));
                    int reminder = levels&0b11; //0..3

                    int upcmd = CmdTurnLeft + (levels>>2);
                    int downcmd = upcmd-1;
                    if (downcmd < CmdTurnLeft)
                        downcmd = CmdNoTorque; //resolve to no torque

                    // Take the sub-period we are in
                    int subper = (int)(this.Timer.Tick&0b11);
                    var pulse = PulseSequences[reminder, subper];
                    if (pulse==1) {
                        OutputEffectCommand = upcmd;
                    } else {
                        OutputEffectCommand = downcmd;
                    }

                } else {

                    int strength = (int)(Trq* MAX_LEVEL);
                    OutputEffectCommand = CmdTurnLeft + strength;

                }

            }
        }

        protected override void State_INIT()
        {
            switch (Step) {
                case 0:
                    ResetAllEffects();
                    // Echo test
                    OutputEffectCommand = (long)GenericModel3CMD.PING;
                    TimeoutTimer.Restart();
                    GoToNextStep();
                    break;
                case 1:
                    if (TimeoutTimer.ElapsedMilliseconds>1000) {
                        // Play sequence ?
                        OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                        TimeoutTimer.Restart();
                        GoToNextStep();
                    }
                    break;
                case 7:
                    if (TimeoutTimer.ElapsedMilliseconds>100) {
                        // Maximum power set to 100%
                        OutputEffectCommand = (long)75;
                        GoToNextStep();
                    }
                    break;
                case 8:
                    TransitionTo(FFBStates.DEVICE_READY);
                    break;
            }
        }
        protected override void State_RESET()
        {
            switch (Step) {
                case 0:
                    ResetAllEffects();
                    TransitionTo(FFBStates.DEVICE_READY);
                    break;
            }
        }
        protected override void State_DISABLE()
        {
            switch (Step) {
                case 0:
                    OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                    break;
            }
        }
        protected override void State_READY()
        {
            switch (Step) {
                case 0:
                    OutputEffectCommand = (long)GenericModel3CMD.NO_EFFECT;
                    break;
            }
        }
    }
}


