using System;

namespace Game.Simulation
{
    /// <summary>
    /// Accumulates presentation time for a fixed 30 Hz simulation.
    /// </summary>
    public sealed class SimulationClock
    {
        /// <summary>The locked M2 simulation frequency.</summary>
        public const int TickRate = 30;

        /// <summary>The duration of one fixed tick in seconds.</summary>
        public const double TickDurationSeconds = 1d / TickRate;

        private const double AccumulatorEpsilon = 1e-12;
        private double accumulatorSeconds;

        /// <summary>Initializes a clock with a bounded catch-up budget.</summary>
        public SimulationClock(int maxCatchUpTicks = 4)
        {
            if (maxCatchUpTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCatchUpTicks));
            }

            MaxCatchUpTicks = maxCatchUpTicks;
        }

        /// <summary>Gets the maximum ticks executed by one advance call.</summary>
        public int MaxCatchUpTicks { get; }

        /// <summary>Gets whether elapsed presentation time is currently ignored.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>Gets the total number of completed fixed ticks.</summary>
        public long TickCount { get; private set; }

        /// <summary>Gets unconsumed elapsed time.</summary>
        public double AccumulatorSeconds => accumulatorSeconds;

        /// <summary>Gets the clamped interpolation alpha for the presentation layer.</summary>
        public double InterpolationAlpha
        {
            get
            {
                var alpha = accumulatorSeconds / TickDurationSeconds;
                return alpha > 1d ? 1d : alpha;
            }
        }

        /// <summary>Pauses fixed-tick accumulation without changing partial progress.</summary>
        public void Pause()
        {
            IsPaused = true;
        }

        /// <summary>Resumes fixed-tick accumulation.</summary>
        public void Resume()
        {
            IsPaused = false;
        }

        internal int Accumulate(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) ||
                double.IsInfinity(elapsedSeconds) ||
                elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (IsPaused)
            {
                return 0;
            }

            accumulatorSeconds += elapsedSeconds;
            var available = (int)((accumulatorSeconds + AccumulatorEpsilon) / TickDurationSeconds);
            return available > MaxCatchUpTicks ? MaxCatchUpTicks : available;
        }

        internal void CompleteAccumulatedTick()
        {
            accumulatorSeconds -= TickDurationSeconds;
            if (accumulatorSeconds < 0d && accumulatorSeconds > -AccumulatorEpsilon)
            {
                accumulatorSeconds = 0d;
            }

            TickCount++;
        }

        internal void CompleteSingleStep()
        {
            TickCount++;
        }
    }

    /// <summary>
    /// Drives a simulation world from presentation deltas using a fixed 30 Hz clock.
    /// </summary>
    public sealed class FixedTickRunner
    {
        private readonly SimulationWorld world;

        /// <summary>Initializes a runner with explicit world and optional clock.</summary>
        public FixedTickRunner(SimulationWorld world, SimulationClock clock = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            Clock = clock ?? new SimulationClock();
        }

        /// <summary>Gets the owned clock.</summary>
        public SimulationClock Clock { get; }

        /// <summary>
        /// Accumulates one presentation delta and executes at most the catch-up limit.
        /// </summary>
        /// <returns>The number of fixed ticks executed.</returns>
        public int Advance(double elapsedSeconds)
        {
            var availableTicks = Clock.Accumulate(elapsedSeconds);
            if (availableTicks > 0)
            {
                world.BeginTickBatch();
            }

            for (var tickIndex = 0; tickIndex < availableTicks; tickIndex++)
            {
                world.RunTick();
                Clock.CompleteAccumulatedTick();
            }

            return availableTicks;
        }

        /// <summary>Executes exactly one debug tick while paused.</summary>
        public void Step()
        {
            if (!Clock.IsPaused)
            {
                throw new InvalidOperationException("Single-step requires a paused clock.");
            }

            world.BeginTickBatch();
            world.RunTick();
            Clock.CompleteSingleStep();
        }
    }
}
